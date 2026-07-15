# Cache Cow: Design System (DESIGN.md)

Version 1.0 | Status: Draft for review
Scope: consumer storefront, regional market variants (US, ES, MX, DE, JP, IN), B2B API surface, wholesale/grocery portal, internal operations dashboard.

---

## 1. Brand foundation

**Name rationale.** Cache Cow works on three layers: cash cow (revenue), cache (frozen inventory staged in regional cold storage, served like a CDN edge node), and cow (the product, or in India, the beloved mascot). Every design decision should reinforce at least one of these layers without belaboring any of them.

**Brand personality.** A serious smokehouse run by people who think in systems. The food photography, typography, and craft cues are 100 percent sincere BBQ. The technology metaphor lives in the details: monospace order numbers, cache-status stock badges, smoke drawn as broadcast signals. The joke is discovered, never announced.

**Positioning line (internal, not a slogan):** Regional BBQ, served from the nearest cold cache.

**Tagline:** SMOKED · CACHED · DELIVERED

---

## 2. Logo system

Delivered assets: `cache-cow-logo.svg` (horizontal lockup), `cache-cow-icon.svg` (square app/favicon mark).

### 2.1 Construction

The mark is a geometric cow head in Char on Butcher Paper. Three brand-specific elements:

1. **The blaze.** The white patch on the cow's forehead is a database cylinder (stacked-disk icon). This is the single "insider" element of the mark. It reads as a normal blaze at small sizes and as a database at large sizes. Never explain it in marketing copy.
2. **The smoke signal.** Three Ember arcs rising from the head, drawn as a broadcast fan. This is smoke and it is a transmission. The arc fan is a reusable motif across the whole system (see 5.1).
3. **The wordmark.** CACHE COW set in Alfa Slab One, converted to outlines. The tag line is IBM Plex Mono Bold, letterspaced, in Ember.

### 2.2 Variants

| Variant | Use |
|---|---|
| Horizontal lockup (icon + wordmark + tag) | Site header desktop, documents, invoices, packaging front |
| Horizontal lockup, no tag | Anywhere under 200px wide |
| Icon on Butcher tile | Favicon, app icon, social avatar, packing tape |
| Icon, transparent, single color (Char or Paper) | Embossing, laser-cut, freezer-bag print, single-color contexts |
| Wordmark only | Legal, footer |

### 2.3 Rules

- Clearspace: minimum padding around the lockup equals the height of the letter C in CACHE.
- Minimum sizes: lockup 140px wide, icon 24px. Below 32px, the icon drops the blaze separator strokes (they close up).
- The icon is identical in every market, including India. The cow is loved everywhere. Only the menu changes.
- Do not recolor the arcs to green, do not add gradients, do not place the Char lockup on photography without a Paper or Butcher plate behind it, do not set the wordmark in any other typeface.
- The tag line always appears in English in the lockup asset. Translated taglines are set as separate text, not baked into the logo.

---

## 3. Color

Palette is drawn from the pit: soot, bark, pink butcher paper, live coals. One green exists in the system and it means "good status" (stock, veg, success). No blue anywhere in the consumer brand; blue is reserved for nothing, which keeps the brand instantly distinguishable from generic food-delivery UI.

### 3.1 Core tokens

| Token | Hex | Role |
|---|---|---|
| `color.char.900` | `#221812` | Primary text, logo, footer background |
| `color.bark.700` | `#4A3226` | Secondary text, icons |
| `color.smoke.400` | `#A79A8F` | Borders, dividers, disabled states |
| `color.butcher.300` | `#F0C39B` | Brand surface: hero bands, cards, icon tile |
| `color.paper.100` | `#FCF7F0` | Default page background, long-form reading |
| `color.ember.500` | `#E04E1B` | Primary action, price highlights, sale, the smoke arcs |
| `color.ember.700` | `#B23A12` | Action hover/pressed, ember text on light surfaces when 4.5:1 is required |
| `color.cache.500` | `#1FA860` | In-stock status, veg indicator, success |
| `color.pit.950` | `#16110E` | Dashboard and API docs background |
| `color.pitpaper.100` | `#E9DED4` | Dashboard primary text |

### 3.2 Contrast pairs (verified)

- Char on Paper: 15.8:1. Char on Butcher: 10.4:1. Both pass AA and AAA for all text.
- Ember on Paper: 3.7:1. Large text and graphics only. Body text is never Ember; use `ember.700` (5.9:1 on Paper) when colored body-size text is unavoidable.
- Cache green on Pit: 6.7:1. Passes AA for dashboard text and badges.
- Buttons: Paper text on Ember fill passes at button sizes; verify every new pair at implementation with automated contrast checks in CI.

### 3.3 Regional color notes

- **India (FSSAI compliance).** Vegetarian products must carry the green-in-square veg symbol per FSSAI labeling rules. Since the IN catalog is 100 percent vegetarian, the veg mark appears on every PDP and on packaging renders. The brown non-veg mark must never appear in the IN locale. Use the regulation mark, not a stylized leaf, on anything that represents packaging or a menu of record.
- **Other markets.** Vegetarian SKUs carry a simplified `cache.500` leaf-dot badge for cross-market consistency. It is a UI affordance there, not a regulatory mark.
- Do not use red/green as the only differentiator anywhere; every status color pairs with a text label or icon shape (see accessibility, section 13).

---

## 4. Typography

### 4.1 Roles (Latin markets: EN, ES, DE)

| Role | Face | Usage |
|---|---|---|
| Display | Alfa Slab One | H1, hero, section titles, big prices on promo. Restraint: one display moment per viewport. |
| UI and body | Archivo (variable) | Everything else. Body 400 at 17px/1.6, UI labels 500, subheads 700. |
| Data | IBM Plex Mono | Prices, SKUs, order and invoice numbers, weights, dates in tables, API docs, cache-status badges. Bold for badges, Regular for table data. Always tabular by nature, which keeps invoices and dashboards aligned. |

### 4.2 Per-script stacks

Alfa Slab One is Latin-only. Hierarchy must survive script switches without it.

| Locale | Display | Body/UI | Notes |
|---|---|---|---|
| en-US, es-*, de-DE | Alfa Slab One | Archivo | de-DE: enable `hyphens: auto` with correct `lang` attr; German compounds will otherwise break layouts. Avoid all-caps nav labels in German (length). |
| ja-JP | Noto Sans JP 900 | Noto Sans JP 400/500 | Display treatment: weight 900 plus a 6px Ember slab underline bar to substitute for the slab serif's visual weight. Line-height 1.75 body. No italics. |
| hi-IN | Noto Sans Devanagari 700 | Noto Sans Devanagari 400 | Same slab-bar display treatment. en-IN pages use the Latin stack. Taller line-height (1.7 minimum) for matra clearance. |

Spanish runs roughly 20 to 25 percent longer than English and German compounds run wider still: every component must be designed at 130 percent of English string length before it is considered done.

### 4.3 Scale

Modular scale 1.250 from a 17px base: 17 / 21 / 27 / 34 / 42 / 53 / 66. Display H1 caps at 66px desktop, 42px mobile. Plex Mono data sits one step below its surrounding Archivo text.

### 4.4 Numerals and prices

All prices render through `Intl.NumberFormat` with the locale of record, in Plex Mono:

- en-US: `$149.00`
- es-ES: `149,00 €` (es-MX: `$149.00 MXN` where cross-currency ambiguity exists)
- de-DE: `149,00 €`
- ja-JP: `￥14,900` (no decimals)
- hi-IN / en-IN: `₹12,49,000.00` (lakh/crore grouping comes from the locale, never hand-formatted)

---

## 5. Signature motifs

### 5.1 Smoke as signal

Smoke is always drawn as the broadcast arc fan from the logo: concentric round-capped arcs, Ember on light surfaces, Paper on Char. It appears in exactly four places: the logo, the order tracker (progress arcs fill as the order advances), section dividers on the home page, and the 404 page ("Signal lost"). It does not appear on every card and every button. Scarcity keeps it a signature instead of wallpaper.

### 5.2 Cache-status stock language

Inventory state is expressed in cache vocabulary, always paired with a plain-language line so the joke never blocks comprehension:

| Badge | Plain line | Meaning |
|---|---|---|
| `CACHE HIT` (cache.500) | Ships today from your regional cold store | In stock in the user's geo region |
| `WARMING` (ember.500) | Restocking, preorder available | Inbound to regional store |
| `CACHE MISS` (smoke.400) | Not available in your region yet | No regional stock; offer nearest substitute |

In the IN locale, beef SKUs are absent from the catalog entirely. They never render as CACHE MISS; a state that implies future availability of beef in India is both wrong and offensive.

### 5.3 Clearance naming

Clearance and short-dated stock runs under the name **Eviction Specials** (cache eviction). It gets the Ember treatment and a countdown in Plex Mono.

### 5.4 Pun budget

Hard rule: at most one cache/tech pun visible per viewport, and zero inside checkout, payment, error recovery, legal, and allergen/nutrition content. Comedy never touches money movement or safety information.

---

## 6. Layout and grid

- 12-column grid, 1200px max content width, 24px gutters desktop, 16px mobile.
- Spacing scale: 4 / 8 / 12 / 16 / 24 / 32 / 48 / 64 / 96.
- Breakpoints: 480 / 768 / 1024 / 1280.
- Page anatomy: Paper background as default; Butcher bands for hero and featured sections; Char footer. Cards are Paper on Butcher or Butcher on Paper, 12px radius, 1px Smoke border, no drop shadows heavier than `0 1px 2px rgb(34 24 18 / 8%)`. The brand's warmth comes from color and type, not from floating panels.
- Hero: the home hero is a full-bleed Butcher band with the product (photography, see 8.6), one Alfa Slab headline, the regional menu CTA, and the arc-fan divider at its base. No carousel. Carousels bury the second slide in every market equally.

---

## 7. Core components

**Product card.** Photo (4:5), name (Archivo 700), cut/weight in Plex Mono, price in Plex Mono, cache-status badge, veg indicator where applicable. The entire card is one link; the add-to-cart button is a separate action inside it.

**Region and language switcher.** Two independent controls in the header: market (drives catalog, currency, compliance) and language (drives strings). Never infer one from the other silently; geolocation proposes, the user disposes, and the choice persists. A user in Berlin may shop the DE catalog in English.

**Price display.** Always locale-formatted (4.4), always includes tax-inclusion note per market convention (DE/ES/JP inclusive, US exclusive plus estimated tax at checkout, IN inclusive with GST line at invoice).

**Order tracker.** Five stages: Smoked, Frozen, Packed, In transit, Delivered. Progress renders as arc-fan segments filling in Ember, each stage labeled in plain text with a timestamp in Plex Mono.

**Veg indicator.** See 3.3. In IN, the FSSAI mark. Elsewhere, the leaf-dot badge plus the word "Vegetarian".

**Chef card.** Portrait, name, pit specialty, market flag(s). Chefs are shared across markets; their bios localize.

**Cow card (mascot system).** Illustrated in the logo's geometric style: flat Char shapes on Butcher, each cow differentiated by blaze shape (one cow's blaze is the database cylinder, one is a lightning bolt, one is a heart). Name, "role" (Head of Grazing, Chief Cud Officer), and a one-line bio. These illustrations are the only place the mascot style is permitted.

**Cut card (butcher diagram system).** Line-art side-profile steer diagram with numbered cut regions, Char linework on Paper, Plex Mono numbering. Clicking a region filters the menu to that cut. Visual language is deliberately technical (a diagram, not a character) and shares zero DNA with the cow-mascot illustrations.

**Sale/promo treatment.** Ember plate, Paper text, Plex Mono countdown. Never animate price text.

**Store locator (grocery).** Map plus list of retail partners stocking Cache Cow freezer product, filtered by the active market region.

---

## 8. Regional design rules

### 8.1 The India inversion (the most important rule in this document)

India is a fully vegetarian market and the cow is culturally revered. The brand handles this by inversion, not by omission:

- The IN catalog contains zero meat SKUs. Meat products, meat photography, and the Cuts page do not render in the IN market at any URL. This is a market-level content gate, enforced server-side by market, not a hidden nav link.
- "Meet our Cows" is promoted to primary navigation in IN and framed sincerely: the herd as mascots and brand family. The same page exists in other markets under Our Story.
- **Separation rule:** herd-mascot content and butchery content never appear in the same view in any market. The Cows page never links to meat PDPs. The Cuts page never uses mascot illustrations. This keeps the brand coherent instead of grotesque.
- IN marketing tone leans on the paneer, jackfruit, mushroom, and legume smoke program as the headline story, not as a substitute for something absent.

### 8.2 United States (en-US)

Full catalog. Brisket-forward photography. Imperial units primary, metric secondary on spec sheets. Price without tax until checkout.

### 8.3 Spanish markets (es-ES, es-MX)

Both markets launch (per REQUIREMENTS.md CC-MKT-001); currency (EUR vs MXN), tax display, and some vocabulary (e.g., "carne asada" registers differently) diverge between Spain and Mexico. All Spanish strings are tagged by region variant (es-ES, es-MX). Text expansion budget applies everywhere.

### 8.4 Germany (de-DE)

Full catalog. Formal address ("Sie") throughout commerce and legal, informal permitted only in social content. Impressum, Widerrufsrecht, and detailed allergen/nutrition links are first-class footer items, not buried. German consumers expect exact net weights and unit prices (per kg) next to every price: mandatory on product cards, in Plex Mono.

### 8.5 Japan (ja-JP)

Full catalog, premium framing (gift-grade wagyu-adjacent positioning is plausible here). Gifting matters: packaging renders, noshi-style gift option at checkout, precise delivery-window selection (Japanese logistics norms). Dense information presentation is expected and trusted; do not over-whitespace the JP product pages. Display treatment per 4.2.

### 8.6 Photography direction (all markets)

Real smoke, real bark, char and pink smoke ring visible, shot on dark steel and butcher paper. No stock-photo gloss, no watermarked grill-flames clip art. Vegetarian dishes are shot with identical seriousness and identical lighting as meat: the IN market must never look like it received the B-roll.

---

## 9. Voice and microcopy

- Plain verbs, sentence case, active voice. "Save changes", not "Submit".
- The brand is funny in headlines and empty states, and completely straight in checkout, errors, legal, allergens, and anything involving money or safety (see 5.4).
- Errors state what happened and what to do next. No apologies, no mascots in error states.
- Empty cart: "Your cache is empty. Warm it up." with a menu CTA. That is the entire permitted joke in the cart.
- Translated copy is written per market by native speakers, not translated puns. A cache pun that needs a footnote in Japanese is cut in Japanese.

---

## 10. Page inventory (consumer)

| Page | Notes |
|---|---|
| Home | Hero (6), regional bestsellers, how-it-works (smoked, cached, delivered as three steps), store locator teaser, Eviction Specials band when active |
| Menu | Geo-priced catalog, cut and veg filters, cache-status on every card |
| Product detail | Gallery, weight/serving calc, reheat instructions (per-format: oven/sous-vide/steam), nutrition and allergens, regional price |
| Checkout | Straight voice, locale tax/units, delivery windows (JP), address formats per market |
| Store locator | Grocery partners carrying frozen retail SKUs |
| Meet our Chefs | Shared roster, localized bios |
| Meet our Cows | Mascot herd (8.1); primary nav in IN |
| Meet our Cuts | Interactive butcher diagram; not rendered in IN |
| Contact, FAQ, shipping policy, legal | Standard; DE legal requirements per 8.4 |

## 11. B2B and API surface

- **Wholesale portal** (grocery buyers): utilitarian variant of the consumer system on Paper, case quantities, pallet configs, lead times in Plex Mono, PO upload, invoice history.
- **API documentation**: Pit background, Plex Mono code, Archivo prose, cache-green success responses and ember error responses in examples. Small single-color lockup only. The docs look like documentation, not like the storefront. Endpoint naming gift: stock lookups are literally cache lookups; the docs may note this once, in one sentence, and never again.

## 12. Internal dashboard (sales, orders, invoices, employees)

- **Theme:** Pit background, Pitpaper text, cache-green for good states, Ember for alerts, Smoke for neutral. One accent family per meaning; charts never use decorative palettes.
- **Type:** Archivo for UI, Plex Mono for every number. Table numerals right-aligned, units in column headers not cells.
- **Density:** compact by default (staff tools optimize for scan speed), 40px rows, sticky headers, keyboard-first filtering.
- **Modules:** sales overview (by market region, mapped to the same region model as consumer pricing), orders, invoices (print stylesheet in the consumer light theme so paper invoices are Paper, not Pit), inventory by regional cold store (the literal cache view: hit rates per SKU per region is a real, useful metric and the one dashboard moment where the brand metaphor and the operational truth are the same thing), employee management (role-based views; see 14).
- Charts: bar and line only by default, Char/Ember/Cache/Smoke series colors, direct labeling over legends where series count is 3 or fewer.

## 13. Accessibility

- WCAG 2.2 AA floor across all surfaces, including the dashboard.
- Verified contrast pairs per 3.2; CI contrast checks on token combinations.
- Status is never color-only: every badge carries text (5.2), the veg mark is a shape plus label.
- Full keyboard operability, visible focus (2px Ember outline on light, Cache on Pit), logical tab order through the butcher-diagram interactive with a list fallback.
- `prefers-reduced-motion` disables the arc-fill animation and all reveals; content renders in final state.
- Language and direction set correctly per locale (`lang`, and the architecture must not assume LTR forever; Devanagari is LTR but future markets may not be).
- Icons and the interactive cuts diagram carry ARIA labels; the diagram exposes each region as a named button.

## 14. Secure-by-design front-end defaults

Design decisions with direct security consequences, stated here so they survive into implementation:

- No raw-HTML injection sinks anywhere (`bypassSecurityTrust*`, unsanitized `[innerHTML]`), including CMS-driven marketing bands. Rich content renders through a strict allowlist renderer.
- Translation strings are data, not markup: ICU MessageFormat with interpolated values escaped by default. Localization files are an injection surface (five languages means five supply chains for strings); no HTML in string files.
- All `href`/`src` values pass URL validation with a `#` fallback; partner-store links in the locator are validated against a registered-domain allowlist.
- CSP-friendly by construction: no inline handlers, no inline styles required by any component in this document.
- All API and form boundary data validated with schemas (Zod) before rendering; price and inventory values render only from typed, validated responses.
- The dashboard is a separate origin from the storefront, with role-based views designed in (employee management screens expose only what the role requires).

## 15. Delivered and planned assets

Delivered now: `cache-cow-logo.svg` (lockup, outlined type, no font dependencies), `cache-cow-icon.svg` (240 tile).

To produce next: single-color icon variants (Char, Paper), favicon/ICO set, social avatar exports, the cow-mascot illustration set (7.x), the butcher-diagram base art, packaging dieline mock, and a tokens file (`tokens.json`) generated from section 3 and 4 for design-tool and code consumption.

Fonts: Alfa Slab One (OFL), Archivo (OFL), IBM Plex Mono (OFL), Noto Sans JP (OFL), Noto Sans Devanagari (OFL). All open-license, self-hostable, subset per locale for performance; no third-party font CDN at runtime.
