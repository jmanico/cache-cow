/**
 * Runtime response validation helpers for the dashboard data seams
 * (issues 082/084/085).
 *
 * SECURITY.md, Input validation rule 1: every input crossing a trust
 * boundary is attacker-controlled; the client renders values ONLY from
 * typed, validated responses. Module parsers take `unknown`, verify every
 * field, and THROW on any violation — invalid input is rejected, never
 * sanitized into acceptance. Callers fail closed to a generic error state
 * (SECURITY.md, Logging rules 1–2 and 7).
 *
 * This mirrors the storefront's catalog.validate.ts discipline WITHOUT
 * importing it: the dashboard shares no modules with storefront or portal
 * (SECURITY.md, HTTP boundary rule 8; ARCHITECTURE.md, Dependency rule 4).
 */

/**
 * Raised when a response fails the typed schema. Message is generic — it
 * names the field/rule, never echoes raw payload content (SECURITY.md,
 * Logging rules 1 and 5).
 */
export class ResponseValidationError extends Error {
  constructor(rule: string) {
    super(`Response failed schema validation: ${rule}`);
    this.name = 'ResponseValidationError';
  }
}

export function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

export function requireRecord(value: unknown, field: string): Record<string, unknown> {
  if (!isRecord(value)) {
    throw new ResponseValidationError(`${field} must be an object`);
  }
  return value;
}

export function requireString(value: unknown, field: string): string {
  if (typeof value !== 'string' || value.length === 0) {
    throw new ResponseValidationError(`${field} must be a non-empty string`);
  }
  return value;
}

export function requireEnum<T extends string>(
  value: unknown,
  allowed: readonly T[],
  field: string,
): T {
  if (typeof value !== 'string' || !(allowed as readonly string[]).includes(value)) {
    throw new ResponseValidationError(`${field} must be one of the declared values`);
  }
  return value as T;
}

export function requireBoolean(value: unknown, field: string): boolean {
  if (typeof value !== 'boolean') {
    throw new ResponseValidationError(`${field} must be a boolean`);
  }
  return value;
}

export function requireArray(value: unknown, field: string): readonly unknown[] {
  if (!Array.isArray(value)) {
    throw new ResponseValidationError(`${field} must be an array`);
  }
  return value;
}

/**
 * Non-negative safe integer. Money uses this for integer minor units
 * (CC-PRC-003): a float, NaN, negative, or unsafe-magnitude amount is a
 * malformed response, not something to round. The client performs no
 * monetary arithmetic — shape validation only (ARCHITECTURE.md,
 * Dependency rule 2).
 */
export function requireNonNegativeInt(value: unknown, field: string): number {
  if (typeof value !== 'number' || !Number.isSafeInteger(value) || value < 0) {
    throw new ResponseValidationError(`${field} must be a non-negative safe integer`);
  }
  return value;
}

export function requireIntInRange(
  value: unknown,
  min: number,
  max: number,
  field: string,
): number {
  const int = requireNonNegativeInt(value, field);
  if (int < min || int > max) {
    throw new ResponseValidationError(`${field} must be between ${min} and ${max}`);
  }
  return int;
}

/** An ISO-8601 timestamp/date string that the Date parser accepts. */
export function requireIsoDateTime(value: unknown, field: string): string {
  const text = requireString(value, field);
  if (Number.isNaN(Date.parse(text))) {
    throw new ResponseValidationError(`${field} must be an ISO-8601 date/time string`);
  }
  return text;
}

/** ISO 4217 currency code (shape only; the server owns money semantics). */
export function requireCurrency(value: unknown, field: string): string {
  const text = requireString(value, field);
  if (!/^[A-Z]{3}$/.test(text)) {
    throw new ResponseValidationError(`${field} must be an ISO 4217 code`);
  }
  return text;
}
