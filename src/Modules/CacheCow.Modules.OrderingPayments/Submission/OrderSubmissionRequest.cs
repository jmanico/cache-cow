using CacheCow.SharedKernel;

namespace CacheCow.Modules.OrderingPayments.Submission;

/// <summary>
/// The complete client contribution to an order submission: SKU identity and
/// quantity per line — nothing else. By construction there is no field for a
/// price, discount, tax, or total, so a client-supplied monetary value is
/// unrepresentable rather than "ignored" (CC-PRC-005; SECURITY.md, Input
/// validation rule 3). Buyer identity, transacting market, timestamps, and
/// every monetary amount are server-derived inside
/// <see cref="OrderSubmissionService"/>.
///
/// The cart/checkout-session model (server-side cart vs. client-held cart
/// submitted whole) is an open question (issue 036, Open Questions); this type
/// is the submission-time contract either model feeds.
/// </summary>
public sealed record OrderSubmissionRequest(IReadOnlyList<SubmittedCartLine> Lines);

/// <summary>
/// One submitted cart line: SKU + quantity only. Quantity is
/// attacker-influenced input (CC-PRC-003) and is bounds-checked by
/// <see cref="OrderSubmissionService"/> against <see cref="OrderSubmissionOptions"/>.
/// </summary>
public sealed record SubmittedCartLine(SkuId Sku, int Quantity);
