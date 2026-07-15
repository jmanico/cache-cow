/**
 * ICU MessageFormat subset: parser, formatter, serializer, placeholder
 * extraction (issue 064, client half; CC-I18N-002).
 *
 * This module is the SINGLE source of truth for the message grammar
 * (ARCHITECTURE.md, Dependency rule 7): the Angular runtime formatter,
 * the Node CI validator (clients/i18n/check-messages.mjs), and the
 * pseudo-localization generator (clients/i18n/pseudo-localize.mjs) all import
 * it — Node 24 runs it directly via native type stripping, so there is no
 * hand-maintained parallel grammar definition anywhere.
 *
 * Supported grammar (documented subset of ICU MessageFormat):
 *   - literal text
 *   - simple arguments:            {name}
 *   - plural arguments:            {name, plural, one {...} =0 {...} other {...}}
 *   - select arguments:            {name, select, a {...} other {...}}
 *   - `#` inside plural branches (the plural operand, locale-number-formatted)
 * Not supported (rejected, fail closed — SECURITY.md, Input validation rule 1):
 *   ICU apostrophe quoting, selectordinal, date/time/number skeletons, and
 *   any other argument type. `{` / `}` are always syntax characters.
 *
 * Security posture (SECURITY.md, Input validation rules 5 and 7):
 *   - Messages and interpolated values are ALWAYS plain text. Formatting
 *     returns a string that callers bind only through Angular text
 *     interpolation — never through [innerHTML] or bypassSecurityTrust*.
 *     There is deliberately no opt-out to raw HTML in this pipeline.
 *   - Invalid messages throw; they are never "sanitized into acceptance".
 *
 * Erasable-TypeScript-only file (no enums/decorators/namespaces) so Node can
 * strip types natively. Keep it dependency-free.
 */

export type IcuNode =
  | { readonly kind: 'text'; readonly value: string }
  | { readonly kind: 'arg'; readonly name: string }
  | { readonly kind: 'pound' }
  | {
      readonly kind: 'plural' | 'select';
      readonly name: string;
      readonly options: ReadonlyMap<string, readonly IcuNode[]>;
    };

/** Values interpolated into a message. Numbers are locale-formatted (CC-I18N-003). */
export type IcuValues = Readonly<Record<string, string | number>>;

export class IcuSyntaxError extends Error {
  readonly position: number;

  constructor(message: string, position: number) {
    super(`${message} (at offset ${position})`);
    this.name = 'IcuSyntaxError';
    this.position = position;
  }
}

const ARG_NAME_RE = /^[a-zA-Z][a-zA-Z0-9_]*$/;
const SELECTOR_RE = /^(=\d+|[a-zA-Z][a-zA-Z0-9_]*)$/;

interface ParseState {
  readonly src: string;
  pos: number;
}

/** Parse a message into an AST. Throws IcuSyntaxError on any invalid input. */
export function parseIcu(message: string): readonly IcuNode[] {
  if (typeof message !== 'string') {
    throw new IcuSyntaxError('Message must be a string', 0);
  }
  const state: ParseState = { src: message, pos: 0 };
  const nodes = parseSequence(state, false);
  if (state.pos !== message.length) {
    throw new IcuSyntaxError(`Unbalanced '}'`, state.pos);
  }
  return nodes;
}

function parseSequence(state: ParseState, inPlural: boolean): IcuNode[] {
  const nodes: IcuNode[] = [];
  let text = '';
  const flush = () => {
    if (text.length > 0) {
      nodes.push({ kind: 'text', value: text });
      text = '';
    }
  };
  while (state.pos < state.src.length) {
    const ch = state.src[state.pos];
    if (ch === '{') {
      flush();
      nodes.push(parseArgument(state, inPlural));
    } else if (ch === '}') {
      // Caller (an option body, or the top level which then errors) handles it.
      break;
    } else if (ch === '#' && inPlural) {
      flush();
      nodes.push({ kind: 'pound' });
      state.pos++;
    } else {
      text += ch;
      state.pos++;
    }
  }
  flush();
  return nodes;
}

function parseArgument(state: ParseState, inPlural: boolean): IcuNode {
  const start = state.pos;
  state.pos++; // consume '{'
  const name = readUntil(state, [',', '}']).trim();
  if (!ARG_NAME_RE.test(name)) {
    throw new IcuSyntaxError(`Invalid argument name '${truncateForError(name)}'`, start);
  }
  if (state.src[state.pos] === '}') {
    state.pos++;
    return { kind: 'arg', name };
  }
  state.pos++; // consume ','
  const type = readUntil(state, [',', '}']).trim();
  if (type !== 'plural' && type !== 'select') {
    throw new IcuSyntaxError(
      `Unsupported argument type '${truncateForError(type)}' for '${name}' (only plural/select)`,
      start,
    );
  }
  if (state.src[state.pos] !== ',') {
    throw new IcuSyntaxError(`Expected ',' after '${type}' in argument '${name}'`, state.pos);
  }
  state.pos++; // consume ','
  const options = new Map<string, readonly IcuNode[]>();
  for (;;) {
    skipWhitespace(state);
    if (state.pos >= state.src.length) {
      throw new IcuSyntaxError(`Unterminated '${type}' argument '${name}'`, start);
    }
    if (state.src[state.pos] === '}') {
      state.pos++;
      break;
    }
    const selector = readUntil(state, ['{']).trim();
    if (!SELECTOR_RE.test(selector)) {
      throw new IcuSyntaxError(
        `Invalid ${type} selector '${truncateForError(selector)}' in argument '${name}'`,
        state.pos,
      );
    }
    if (state.src[state.pos] !== '{') {
      throw new IcuSyntaxError(`Expected '{' after selector '${selector}'`, state.pos);
    }
    if (options.has(selector)) {
      throw new IcuSyntaxError(`Duplicate ${type} selector '${selector}' in argument '${name}'`, state.pos);
    }
    state.pos++; // consume '{'
    const body = parseSequence(state, type === 'plural' ? true : inPlural);
    if (state.src[state.pos] !== '}') {
      throw new IcuSyntaxError(`Unterminated option '${selector}' in argument '${name}'`, state.pos);
    }
    state.pos++; // consume '}'
    options.set(selector, body);
  }
  if (!options.has('other')) {
    // Fail closed at parse time: every plural/select must carry 'other'.
    throw new IcuSyntaxError(`Missing required 'other' option in ${type} argument '${name}'`, start);
  }
  return { kind: type, name, options };
}

function readUntil(state: ParseState, stops: readonly string[]): string {
  let out = '';
  while (state.pos < state.src.length && !stops.includes(state.src[state.pos])) {
    if (state.src[state.pos] === '{' && !stops.includes('{')) {
      throw new IcuSyntaxError(`Unexpected '{'`, state.pos);
    }
    out += state.src[state.pos];
    state.pos++;
  }
  if (state.pos >= state.src.length) {
    throw new IcuSyntaxError(`Unterminated argument`, state.pos);
  }
  return out;
}

function skipWhitespace(state: ParseState): void {
  while (state.pos < state.src.length && /\s/.test(state.src[state.pos])) {
    state.pos++;
  }
}

function truncateForError(value: string): string {
  // Error text must not echo long raw invalid content (SECURITY.md, Logging rule 5).
  return value.length > 24 ? `${value.slice(0, 24)}…` : value;
}

/**
 * Format a message for a locale. Escape-by-default: the result is a plain
 * string; interpolated values are stringified, never interpreted as markup.
 * Throws on syntax errors or missing values (fail closed, never raw fallthrough).
 */
export function formatIcu(message: string, locale: string, values: IcuValues = {}): string {
  return formatNodes(parseIcu(message), locale, values, null);
}

function formatNodes(
  nodes: readonly IcuNode[],
  locale: string,
  values: IcuValues,
  pluralOperand: number | null,
): string {
  let out = '';
  for (const node of nodes) {
    switch (node.kind) {
      case 'text':
        out += node.value;
        break;
      case 'pound':
        if (pluralOperand === null) {
          throw new IcuSyntaxError(`'#' outside plural`, 0);
        }
        out += new Intl.NumberFormat(locale).format(pluralOperand);
        break;
      case 'arg': {
        const value = requireValue(values, node.name);
        out += typeof value === 'number' ? new Intl.NumberFormat(locale).format(value) : String(value);
        break;
      }
      case 'plural': {
        const raw = requireValue(values, node.name);
        const operand = typeof raw === 'number' ? raw : Number(raw);
        if (!Number.isFinite(operand)) {
          throw new IcuSyntaxError(`Non-numeric value for plural argument '${node.name}'`, 0);
        }
        const exact = node.options.get(`=${operand}`);
        const category = new Intl.PluralRules(locale).select(operand);
        const branch = exact ?? node.options.get(category) ?? node.options.get('other');
        // 'other' is guaranteed by the parser.
        out += formatNodes(branch as readonly IcuNode[], locale, values, operand);
        break;
      }
      case 'select': {
        const value = String(requireValue(values, node.name));
        const branch = node.options.get(value) ?? node.options.get('other');
        out += formatNodes(branch as readonly IcuNode[], locale, values, pluralOperand);
        break;
      }
    }
  }
  return out;
}

function requireValue(values: IcuValues, name: string): string | number {
  const value = values[name];
  if (value === undefined || value === null) {
    throw new IcuSyntaxError(`Missing value for placeholder '${name}'`, 0);
  }
  return value;
}

/**
 * Stable placeholder signature of a message, for cross-locale consistency
 * validation (issue 064 AC-04): name, type, and (for plural/select) the
 * option-key set, recursively including nested placeholders.
 */
export function placeholderSignature(message: string): readonly string[] {
  const found = new Set<string>();
  collectSignatures(parseIcu(message), found);
  return [...found].sort();
}

function collectSignatures(nodes: readonly IcuNode[], into: Set<string>): void {
  for (const node of nodes) {
    if (node.kind === 'arg') {
      into.add(`${node.name}:arg`);
    } else if (node.kind === 'plural' || node.kind === 'select') {
      into.add(`${node.name}:${node.kind}(${[...node.options.keys()].sort().join('|')})`);
      for (const body of node.options.values()) {
        collectSignatures(body, into);
      }
    }
  }
}

/**
 * Serialize an AST back to ICU source. Used by the pseudo-localization
 * generator so transformed literals round-trip through the same grammar.
 */
export function serializeIcu(nodes: readonly IcuNode[]): string {
  let out = '';
  for (const node of nodes) {
    switch (node.kind) {
      case 'text':
        out += node.value;
        break;
      case 'pound':
        out += '#';
        break;
      case 'arg':
        out += `{${node.name}}`;
        break;
      case 'plural':
      case 'select': {
        const options = [...node.options.entries()]
          .map(([selector, body]) => `${selector} {${serializeIcu(body)}}`)
          .join(' ');
        out += `{${node.name}, ${node.kind}, ${options}}`;
        break;
      }
    }
  }
  return out;
}

/**
 * Transform only the literal text of a message (placeholders and ICU
 * structure preserved). Used by pseudo-localization (issue 065).
 */
export function mapMessageText(message: string, transform: (text: string) => string): string {
  return serializeIcu(mapNodes(parseIcu(message), transform));
}

function mapNodes(nodes: readonly IcuNode[], transform: (text: string) => string): IcuNode[] {
  return nodes.map((node) => {
    if (node.kind === 'text') {
      return { kind: 'text', value: transform(node.value) };
    }
    if (node.kind === 'plural' || node.kind === 'select') {
      const options = new Map<string, readonly IcuNode[]>();
      for (const [selector, body] of node.options) {
        options.set(selector, mapNodes(body, transform));
      }
      return { kind: node.kind, name: node.name, options };
    }
    return node;
  });
}
