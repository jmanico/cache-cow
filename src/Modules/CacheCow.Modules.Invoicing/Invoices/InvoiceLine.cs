using CacheCow.SharedKernel;

namespace CacheCow.Modules.Invoicing.Invoices;

/// <summary>
/// One immutable invoice line. Money is integer minor units with
/// overflow-checked arithmetic throughout — quantities are attacker-influenced
/// (CC-PRC-003). The legal description is validated against presentation-only
/// promotion naming: "Eviction Specials" is storefront vocabulary and MUST NOT
/// leak into invoice line-item legal descriptions (CC-PRC-007; issue 046,
/// AC-05).
/// </summary>
public sealed class InvoiceLine
{
    public InvoiceLine(string legalDescription, SkuId skuId, long quantity, Money unitPrice)
    {
        if (string.IsNullOrWhiteSpace(legalDescription))
        {
            throw new InvoiceValidationException("An invoice line requires a legal description (CC-INV-001).");
        }

        if (legalDescription.Contains("eviction special", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvoiceValidationException(
                "\"Eviction Specials\" is presentation-only clearance naming and must not appear in a "
                + "line-item legal description (CC-PRC-007).");
        }

        if (quantity < 1)
        {
            throw new InvoiceValidationException("An invoice line quantity must be at least 1.");
        }

        LegalDescription = legalDescription;
        SkuId = skuId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        LineTotal = unitPrice * quantity; // overflow-checked, fails closed (CC-PRC-003)
    }

    public string LegalDescription { get; }

    public SkuId SkuId { get; }

    public long Quantity { get; }

    public Money UnitPrice { get; }

    /// <summary>Quantity × unit price, computed once at construction (CC-PRC-003).</summary>
    public Money LineTotal { get; }
}
