/**
 * Mock content fixture (issues 073/074/077) — a stand-in for the SERVER's
 * Content & Localization responses until the Contentful integration and
 * sanitizing allowlist renderer land (issue 072) and the Market & Gating
 * Policy service owns the per-market legal content set (issue 023).
 *
 * This module SIMULATES THE SERVER: localization happens "server-side" here
 * (per-locale strings picked before the payload crosses the seam), the legal
 * content set is per-market data (DE additionally carries Impressum and
 * Widerrufsbelehrung — CC-CNT-005), and everything crossing the seam is
 * plain text (the real pipeline sanitizes via issue 072's allowlist renderer
 * BEFORE the client ever sees it).
 *
 * All copy is PLACEHOLDER. Real editorial content (chef roster/bios, herd
 * names/roles/bios per DESIGN.md §9 native-written copy) is an open content
 * decision, and the LEGAL TEXTS below are explicitly NOT the accepted
 * drafted texts: legal wording is supplied through legal review (accepted
 * 2026-07-15, ARCHITECTURE.md decision record) and MUST NOT be authored
 * here — placeholders only state that the accepted text will be rendered
 * unchanged. Zero puns in legal content (DESIGN.md §5.4).
 */

import { Locale, Market } from '../core/transacting-context';
import { LegalDocId } from './content.types';

/** Server-side localization helper: one string per launch locale. */
type LocalizedText = Readonly<Record<Locale, string>>;

// --- Chefs (issue 073; CC-CNT-001): SHARED roster, localized bios -----------

interface MockChef {
  readonly id: string;
  readonly name: string;
  readonly specialty: LocalizedText;
  readonly bio: LocalizedText;
  readonly markets: readonly Market[];
}

const CHEFS: readonly MockChef[] = [
  {
    id: 'marisol-vega',
    name: 'Marisol Vega',
    specialty: {
      'en-US': 'Brisket and fire management',
      'en-IN': 'Brisket and fire management',
      'es-ES': 'Pecho y control del fuego',
      'es-MX': 'Brisket y control del fuego',
      'de-DE': 'Brust und Feuerführung',
      'ja-JP': 'ブリスケットと火加減',
      'hi-IN': 'ब्रिस्केट और आँच पर नियंत्रण',
    },
    bio: {
      'en-US': 'Fifteen years at Texas pits taught Marisol that patience is the only real recipe.',
      'en-IN': 'Fifteen years at Texas pits taught Marisol that patience is the only real recipe.',
      'es-ES': 'Quince años junto a los ahumadores de Texas enseñaron a Marisol que la paciencia es la única receta verdadera.',
      'es-MX': 'Quince años en los ahumadores de Texas le enseñaron a Marisol que la paciencia es la única receta que importa.',
      'de-DE': 'Fünfzehn Jahre an texanischen Smokern haben Marisol gelehrt: Geduld ist das einzige echte Rezept.',
      'ja-JP': 'テキサスのピットで15年。マリソルは「本当のレシピは忍耐だけ」と学びました。',
      'hi-IN': 'टेक्सास के पिट पर पंद्रह साल बिताकर मारिसोल ने सीखा कि धैर्य ही असली नुस्ख़ा है।',
    },
    markets: ['US', 'MX'],
  },
  {
    id: 'jonas-brandt',
    name: 'Jonas Brandt',
    specialty: {
      'en-US': 'Sausage and cold smoke',
      'en-IN': 'Sausage and cold smoke',
      'es-ES': 'Embutidos y ahumado en frío',
      'es-MX': 'Embutidos y ahumado en frío',
      'de-DE': 'Wurst und Kalträuchern',
      'ja-JP': 'ソーセージと冷燻',
      'hi-IN': 'सॉसेज और कोल्ड स्मोक',
    },
    bio: {
      'en-US': 'Jonas grinds, stuffs, and smokes every test batch himself before a link earns the label.',
      'en-IN': 'Jonas grinds, stuffs, and smokes every test batch himself before a link earns the label.',
      'es-ES': 'Jonas pica, embute y ahúma cada lote de prueba antes de que una salchicha merezca la etiqueta.',
      'es-MX': 'Jonas muele, embute y ahúma cada lote de prueba antes de que una salchicha gane la etiqueta.',
      'de-DE': 'Jonas wolft, füllt und räuchert jede Testcharge selbst, bevor eine Wurst das Etikett bekommt.',
      'ja-JP': 'ヨナスは試作のたびに自ら挽き、詰め、燻します。ラベルに値するまで妥協しません。',
      'hi-IN': 'योनास हर टेस्ट बैच को ख़ुद पीसते, भरते और स्मोक करते हैं, तभी किसी सॉसेज को लेबल मिलता है।',
    },
    markets: ['DE', 'ES'],
  },
  {
    id: 'aiko-tanaka',
    name: 'Aiko Tanaka',
    specialty: {
      'en-US': 'Vegetable and paneer smoke program',
      'en-IN': 'Vegetable and paneer smoke program',
      'es-ES': 'Programa de ahumado vegetal y paneer',
      'es-MX': 'Programa de ahumado vegetal y paneer',
      'de-DE': 'Gemüse- und Paneer-Räucherprogramm',
      'ja-JP': '野菜とパニールの燻製プログラム',
      'hi-IN': 'सब्ज़ी और पनीर स्मोक कार्यक्रम',
    },
    bio: {
      'en-US': 'Aiko gives paneer, jackfruit, and mushrooms the same slow smoke the pit gives everything else.',
      'en-IN': 'Aiko gives paneer, jackfruit, and mushrooms the same slow smoke the pit gives everything else.',
      'es-ES': 'Aiko da al paneer, la yaca y las setas el mismo ahumado lento que recibe todo lo demás.',
      'es-MX': 'Aiko le da al paneer, al jackfruit y a los hongos el mismo ahumado lento que recibe todo lo demás.',
      'de-DE': 'Aiko gönnt Paneer, Jackfrucht und Pilzen dieselbe langsame Räucherung wie allem anderen am Smoker.',
      'ja-JP': '愛子はパニールもジャックフルーツもきのこも、他のすべてと同じ低温燻製でじっくり仕上げます。',
      'hi-IN': 'आइको पनीर, कटहल और मशरूम को भी वही धीमा स्मोक देती हैं जो बाक़ी हर चीज़ को मिलता है।',
    },
    markets: ['JP', 'IN'],
  },
];

/** Mock server response for the shared chef roster, localized for `locale`.
 * The roster is identical in every market (CC-CNT-001) — deliberately no
 * market parameter. Returned as `unknown` for seam validation. */
export function mockChefRosterResponse(locale: Locale): unknown {
  return {
    locale,
    roster: CHEFS.map((chef) => ({
      id: chef.id,
      name: chef.name,
      specialty: chef.specialty[locale],
      bio: chef.bio[locale],
      markets: chef.markets,
    })),
  };
}

// --- Cows (issue 074; CC-CNT-002): mascot herd -------------------------------

interface MockCow {
  readonly id: string;
  readonly name: string;
  readonly role: LocalizedText;
  readonly bio: LocalizedText;
  readonly blaze: 'database' | 'lightning' | 'heart';
}

/** Three of the seven-illustration mascot set (DESIGN.md §15); the herd
 * roster and per-locale treatment of pun roles are open content decisions
 * (DESIGN.md §9: untranslatable puns are cut, so non-English roles are
 * plain descriptions). */
const COWS: readonly MockCow[] = [
  {
    id: 'daisy',
    name: 'Daisy',
    role: {
      'en-US': 'Head of Grazing',
      'en-IN': 'Head of Grazing',
      'es-ES': 'Jefa de pastoreo',
      'es-MX': 'Jefa de pastoreo',
      'de-DE': 'Leiterin Weidebetrieb',
      'ja-JP': '放牧主任',
      'hi-IN': 'चराई प्रमुख',
    },
    bio: {
      'en-US': 'Daisy keeps the herd moving to the greenest grass and knows every paddock by heart.',
      'en-IN': 'Daisy keeps the herd moving to the greenest grass and knows every paddock by heart.',
      'es-ES': 'Daisy guía a la manada hacia la hierba más verde y se sabe cada prado de memoria.',
      'es-MX': 'Daisy guía a la manada hacia el pasto más verde y se sabe cada potrero de memoria.',
      'de-DE': 'Daisy führt die Herde stets zum grünsten Gras und kennt jede Weide auswendig.',
      'ja-JP': 'デイジーは群れをいちばん青い草へ導き、どの放牧地も知り尽くしています。',
      'hi-IN': 'डेज़ी झुंड को सबसे हरी घास तक ले जाती है और हर चरागाह को ज़ुबानी जानती है।',
    },
    blaze: 'database',
  },
  {
    id: 'bolt',
    name: 'Bolt',
    role: {
      'en-US': 'Pasture Operations Lead',
      'en-IN': 'Pasture Operations Lead',
      'es-ES': 'Responsable de operaciones de pastura',
      'es-MX': 'Responsable de operaciones de pastura',
      'de-DE': 'Leiter der Weideflächen',
      'ja-JP': '牧草地オペレーション担当',
      'hi-IN': 'चरागाह संचालन प्रमुख',
    },
    bio: {
      'en-US': 'Bolt is the fastest cow on the hill and insists the record stands.',
      'en-IN': 'Bolt is the fastest cow on the hill and insists the record stands.',
      'es-ES': 'Bolt es la vaca más rápida de la colina e insiste en que el récord sigue en pie.',
      'es-MX': 'Bolt es la vaca más rápida de la loma e insiste en que el récord sigue vigente.',
      'de-DE': 'Bolt ist die schnellste Kuh am Hang und besteht darauf, dass der Rekord gilt.',
      'ja-JP': 'ボルトは丘でいちばん速い牛。その記録はいまも破られていないと本人は主張します。',
      'hi-IN': 'बोल्ट पहाड़ी की सबसे तेज़ गाय है और उसका रिकॉर्ड आज भी क़ायम है।',
    },
    blaze: 'lightning',
  },
  {
    id: 'clover',
    name: 'Clover',
    role: {
      // "Chief Cud Officer" is DESIGN.md §7's example role; the pun stays
      // English-only — other locales get plain descriptions (DESIGN.md §9).
      'en-US': 'Chief Cud Officer',
      'en-IN': 'Chief Cud Officer',
      'es-ES': 'Directora de la rumia',
      'es-MX': 'Directora de la rumia',
      'de-DE': 'Chefin fürs Wiederkäuen',
      'ja-JP': '反芻担当役員',
      'hi-IN': 'जुगाली प्रमुख',
    },
    bio: {
      'en-US': 'Clover naps in the shade and personally approves every batch of hay.',
      'en-IN': 'Clover naps in the shade and personally approves every batch of hay.',
      'es-ES': 'Clover duerme la siesta a la sombra y aprueba personalmente cada lote de heno.',
      'es-MX': 'Clover toma la siesta a la sombra y aprueba personalmente cada lote de heno.',
      'de-DE': 'Clover döst im Schatten und nimmt jede Heuladung persönlich ab.',
      'ja-JP': 'クローバーは木陰でうたた寝しつつ、干し草をひと束ずつ自ら検品します。',
      'hi-IN': 'क्लोवर छाँव में झपकी लेती है और घास की हर खेप को ख़ुद मंज़ूरी देती है।',
    },
    blaze: 'heart',
  },
];

/** Mock server response for the mascot herd, localized for `locale`. The
 * herd is identical in every market (DESIGN.md §2.3: the cow is loved
 * everywhere — only the menu changes). Returned as `unknown`. */
export function mockCowHerdResponse(locale: Locale): unknown {
  return {
    locale,
    herd: COWS.map((cow) => ({
      id: cow.id,
      name: cow.name,
      role: cow.role[locale],
      bio: cow.bio[locale],
      blaze: cow.blaze,
    })),
  };
}

// --- Legal (issue 077; CC-CNT-005, CC-FUL-003) -------------------------------

const LEGAL_TITLES: Readonly<Record<LegalDocId, LocalizedText>> = {
  privacy: {
    'en-US': 'Privacy policy',
    'en-IN': 'Privacy policy',
    'es-ES': 'Política de privacidad',
    'es-MX': 'Política de privacidad',
    'de-DE': 'Datenschutzerklärung',
    'ja-JP': 'プライバシーポリシー',
    'hi-IN': 'गोपनीयता नीति',
  },
  terms: {
    'en-US': 'Terms of sale',
    'en-IN': 'Terms of sale',
    'es-ES': 'Términos y condiciones',
    'es-MX': 'Términos y condiciones',
    'de-DE': 'Allgemeine Geschäftsbedingungen',
    'ja-JP': '利用規約',
    'hi-IN': 'नियम और शर्तें',
  },
  'shipping-returns': {
    'en-US': 'Shipping and returns',
    'en-IN': 'Shipping and returns',
    'es-ES': 'Envíos y devoluciones',
    'es-MX': 'Envíos y devoluciones',
    'de-DE': 'Versand und Rückgabe',
    'ja-JP': '配送と返品',
    'hi-IN': 'शिपिंग और वापसी',
  },
  // Statutory DE documents keep their German names in every locale.
  impressum: {
    'en-US': 'Impressum',
    'en-IN': 'Impressum',
    'es-ES': 'Impressum',
    'es-MX': 'Impressum',
    'de-DE': 'Impressum',
    'ja-JP': 'Impressum',
    'hi-IN': 'Impressum',
  },
  widerruf: {
    'en-US': 'Widerrufsbelehrung',
    'en-IN': 'Widerrufsbelehrung',
    'es-ES': 'Widerrufsbelehrung',
    'es-MX': 'Widerrufsbelehrung',
    'de-DE': 'Widerrufsbelehrung',
    'ja-JP': 'Widerrufsbelehrung',
    'hi-IN': 'Widerrufsbelehrung',
  },
};

/** PLACEHOLDER body paragraph. NOT legal text: the accepted drafted texts
 * (2026-07-15) are supplied via legal review and rendered unchanged once the
 * content source is wired (open question in issue 077). No puns (§5.4). */
const LEGAL_PLACEHOLDER: LocalizedText = {
  'en-US': 'Placeholder. The ratified legal text for this document, accepted 2026-07-15, is supplied through legal review and will be rendered here unchanged as a new version.',
  'en-IN': 'Placeholder. The ratified legal text for this document, accepted 2026-07-15, is supplied through legal review and will be rendered here unchanged as a new version.',
  'es-ES': 'Texto provisional. El texto legal ratificado de este documento, aceptado el 15-07-2026, procede de la revisión jurídica y se publicará aquí sin cambios como una nueva versión.',
  'es-MX': 'Texto provisional. El texto legal ratificado de este documento, aceptado el 15-07-2026, proviene de la revisión jurídica y se publicará aquí sin cambios como una nueva versión.',
  'de-DE': 'Platzhalter. Der am 15.07.2026 angenommene Rechtstext dieses Dokuments stammt aus der juristischen Prüfung und wird hier unverändert als neue Version veröffentlicht.',
  'ja-JP': '仮のテキストです。2026年7月15日に承認されたこの文書の法定文面は法務レビューを経て提供され、新しい版としてそのままここに掲載されます。',
  'hi-IN': 'अस्थायी पाठ। 15-07-2026 को स्वीकृत इस दस्तावेज़ का विधिक पाठ क़ानूनी समीक्षा से प्राप्त होगा और नए संस्करण के रूप में यहाँ अपरिवर्तित प्रकाशित किया जाएगा।',
};

/** Widerruf-specific placeholder note: the ACCEPTED text (not this note)
 * governs the perishable-frozen-food exemption from the 14-day withdrawal
 * right (CC-FUL-003); this note only records that scope, drafting nothing. */
const WIDERRUF_SCOPE_NOTE: LocalizedText = {
  'en-US': 'The accepted text includes the statutory exemption of perishable frozen food from the standard 14-day right of withdrawal.',
  'en-IN': 'The accepted text includes the statutory exemption of perishable frozen food from the standard 14-day right of withdrawal.',
  'es-ES': 'El texto aceptado incluye la excepción legal de los alimentos congelados perecederos al derecho de desistimiento estándar de 14 días.',
  'es-MX': 'El texto aceptado incluye la excepción legal de los alimentos congelados perecederos al derecho de desistimiento estándar de 14 días.',
  'de-DE': 'Der angenommene Text umfasst die gesetzliche Ausnahme verderblicher Tiefkühlware vom regulären 14-tägigen Widerrufsrecht.',
  'ja-JP': '承認済みの文面には、傷みやすい冷凍食品が標準の14日間の撤回権の法定適用外であることが含まれます。',
  'hi-IN': 'स्वीकृत पाठ में जल्दी ख़राब होने वाले फ़्रोज़न खाद्य को मानक 14-दिनी विदड्रॉअल अधिकार से मिली वैधानिक छूट शामिल है।',
};

/** The per-market legal content set (CC-CNT-005) — policy data, the mock
 * analogue of the Market & Gating Policy configuration (issue 023). */
const MARKET_DOC_SETS: Readonly<Record<Market, readonly LegalDocId[]>> = {
  US: ['privacy', 'terms', 'shipping-returns'],
  ES: ['privacy', 'terms', 'shipping-returns'],
  MX: ['privacy', 'terms', 'shipping-returns'],
  DE: ['privacy', 'terms', 'shipping-returns', 'impressum', 'widerruf'],
  JP: ['privacy', 'terms', 'shipping-returns'],
  IN: ['privacy', 'terms', 'shipping-returns'],
};

/** Versioned-immutable stand-in: one issued version per document
 * (corrections would appear as NEW versions, never mutations). */
const LEGAL_VERSION = '1.0.0';
const LEGAL_EFFECTIVE_DATE = '2026-07-15';

/** Mock server response for the transacting market's legal content set,
 * titles localized for `locale`. Returned as `unknown`. */
export function mockLegalDocListResponse(market: Market, locale: Locale): unknown {
  return {
    market,
    docs: MARKET_DOC_SETS[market].map((id) => ({ id, title: LEGAL_TITLES[id][locale] })),
  };
}

/** Mock server response for one versioned legal document, or null when the
 * document is not in the transacting market's legal content set (the real
 * server answers HTTP 404 — CC-CNT-005 failure behavior; the client shows
 * the 404 page). Returned as `unknown`. */
export function mockLegalDocResponse(market: Market, locale: Locale, docId: string): unknown | null {
  const set = MARKET_DOC_SETS[market] as readonly string[];
  if (!set.includes(docId)) {
    return null;
  }
  const id = docId as LegalDocId;
  const sections: { heading: string; paragraphs: string[] }[] = [
    { heading: LEGAL_TITLES[id][locale], paragraphs: [LEGAL_PLACEHOLDER[locale]] },
  ];
  if (id === 'widerruf') {
    sections.push({
      heading: LEGAL_TITLES[id]['de-DE'],
      paragraphs: [WIDERRUF_SCOPE_NOTE[locale]],
    });
  }
  return {
    id,
    title: LEGAL_TITLES[id][locale],
    version: LEGAL_VERSION,
    effectiveDate: LEGAL_EFFECTIVE_DATE,
    locale,
    sections,
  };
}
