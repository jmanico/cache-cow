/**
 * Content response-validation seam tests (issues 073/074/077; SECURITY.md,
 * Input validation rule 1): malformed content payloads are REJECTED at the
 * client HTTP boundary — never sanitized into acceptance, never partially
 * rendered. Requirement tags per REQUIREMENTS.md §17: CC-SEC-001, CC-SEC-002,
 * CC-CNT-001, CC-CNT-002, CC-CNT-005.
 */

import {
  ContentValidationError,
  parseChefRoster,
  parseCowHerd,
  parseLegalDoc,
  parseLegalDocList,
} from './content.validate';

function validChef(): Record<string, unknown> {
  return {
    id: 'marisol-vega',
    name: 'Marisol Vega',
    specialty: 'Brisket and fire management',
    bio: 'Fifteen years at Texas pits.',
    markets: ['US', 'MX'],
  };
}

function validCow(): Record<string, unknown> {
  return {
    id: 'daisy',
    name: 'Daisy',
    role: 'Head of Grazing',
    bio: 'Daisy keeps the herd moving.',
    blaze: 'database',
  };
}

function validDoc(): Record<string, unknown> {
  return {
    id: 'privacy',
    title: 'Privacy policy',
    version: '1.0.0',
    effectiveDate: '2026-07-15',
    locale: 'en-US',
    sections: [{ heading: 'Privacy policy', paragraphs: ['Placeholder.'] }],
  };
}

describe('parseChefRoster (typed content boundary)', () => {
  it('accepts a valid roster', () => {
    const roster = parseChefRoster({ locale: 'en-US', roster: [validChef()] });
    expect(roster.locale).toBe('en-US');
    expect(roster.roster[0]?.id).toBe('marisol-vega');
  });

  it('rejects an unknown locale', () => {
    expect(() => parseChefRoster({ locale: 'fr-FR', roster: [] })).toThrow(ContentValidationError);
  });

  it('rejects a bio carrying markup — malformed, not sanitized (CC-SEC-002)', () => {
    const chef = validChef();
    chef['bio'] = 'Nice chef <script>alert(1)</script>';
    expect(() => parseChefRoster({ locale: 'en-US', roster: [chef] })).toThrow(
      ContentValidationError,
    );
  });

  it('rejects an unknown market on a chef', () => {
    const chef = validChef();
    chef['markets'] = ['US', 'XX'];
    expect(() => parseChefRoster({ locale: 'en-US', roster: [chef] })).toThrow(
      ContentValidationError,
    );
  });

  it('rejects a chef missing a required field', () => {
    const chef = validChef();
    delete chef['specialty'];
    expect(() => parseChefRoster({ locale: 'en-US', roster: [chef] })).toThrow(
      ContentValidationError,
    );
  });

  it('never echoes payload content in the error message (SECURITY.md, Logging rule 5)', () => {
    const chef = validChef();
    chef['bio'] = '<img src=x onerror=alert(1)>';
    try {
      parseChefRoster({ locale: 'en-US', roster: [chef] });
      expect.unreachable('should have thrown');
    } catch (error) {
      expect((error as Error).message).not.toContain('onerror');
      expect((error as Error).message).toContain('plain text');
    }
  });
});

describe('parseCowHerd (typed content boundary)', () => {
  it('accepts a valid herd', () => {
    const herd = parseCowHerd({ locale: 'en-US', herd: [validCow()] });
    expect(herd.herd[0]?.blaze).toBe('database');
  });

  it('rejects an undeclared blaze', () => {
    const cow = validCow();
    cow['blaze'] = 'star';
    expect(() => parseCowHerd({ locale: 'en-US', herd: [cow] })).toThrow(ContentValidationError);
  });

  it('rejects a role carrying markup (CC-SEC-002)', () => {
    const cow = validCow();
    cow['role'] = '<b>Head of Grazing</b>';
    expect(() => parseCowHerd({ locale: 'en-US', herd: [cow] })).toThrow(ContentValidationError);
  });
});

describe('parseLegalDocList (per-market legal content set, CC-CNT-005)', () => {
  it('accepts the DE set including the statutory DE-only documents', () => {
    const list = parseLegalDocList({
      market: 'DE',
      docs: [
        { id: 'privacy', title: 'Datenschutzerklärung' },
        { id: 'impressum', title: 'Impressum' },
        { id: 'widerruf', title: 'Widerrufsbelehrung' },
      ],
    });
    expect(list.docs.map((d) => d.id)).toEqual(['privacy', 'impressum', 'widerruf']);
  });

  it('rejects DE-only documents in a non-DE market set (fail-closed policy mirror)', () => {
    expect(() =>
      parseLegalDocList({
        market: 'US',
        docs: [
          { id: 'privacy', title: 'Privacy policy' },
          { id: 'impressum', title: 'Impressum' },
        ],
      }),
    ).toThrow(ContentValidationError);
  });

  it('rejects duplicate documents in a set', () => {
    expect(() =>
      parseLegalDocList({
        market: 'US',
        docs: [
          { id: 'privacy', title: 'Privacy policy' },
          { id: 'privacy', title: 'Privacy policy' },
        ],
      }),
    ).toThrow(ContentValidationError);
  });

  it('rejects an undeclared document id', () => {
    expect(() =>
      parseLegalDocList({ market: 'US', docs: [{ id: 'cookies', title: 'Cookies' }] }),
    ).toThrow(ContentValidationError);
  });
});

describe('parseLegalDoc (versioned document, CC-CNT-005)', () => {
  it('accepts a valid versioned document', () => {
    const doc = parseLegalDoc(validDoc());
    expect(doc.version).toBe('1.0.0');
    expect(doc.effectiveDate).toBe('2026-07-15');
    expect(doc.sections[0]?.paragraphs[0]).toBe('Placeholder.');
  });

  it('rejects a document with no version — "versioned" is not optional', () => {
    const doc = validDoc();
    delete doc['version'];
    expect(() => parseLegalDoc(doc)).toThrow(ContentValidationError);
  });

  it('rejects a non-ISO effective date', () => {
    const doc = validDoc();
    doc['effectiveDate'] = '15/07/2026';
    expect(() => parseLegalDoc(doc)).toThrow(ContentValidationError);
  });

  it('rejects a document with no sections', () => {
    const doc = validDoc();
    doc['sections'] = [];
    expect(() => parseLegalDoc(doc)).toThrow(ContentValidationError);
  });

  it('rejects legal body markup — malformed, not sanitized (CC-SEC-002)', () => {
    const doc = validDoc();
    doc['sections'] = [{ heading: 'Terms', paragraphs: ['<script>alert(1)</script>'] }];
    expect(() => parseLegalDoc(doc)).toThrow(ContentValidationError);
  });
});
