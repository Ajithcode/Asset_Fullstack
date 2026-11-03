using MongoDB.Bson.Serialization.Attributes;

namespace A360.Asset.Domain.Entities;

/// <summary>Step 4 of asset registration: purchase information, invoice documents and warranty & support information.</summary>
[BsonIgnoreExtraElements]
public sealed class AssetPurchaseWarranty
{
    [BsonElement("purchase")]
    public AssetPurchase Purchase { get; set; } = new();

    // Files uploaded through the Media service; only their details and URLs are kept here.
    [BsonElement("documents")]
    public List<AssetDocument> Documents { get; set; } = [];

    [BsonElement("warranty")]
    public AssetWarranty Warranty { get; set; } = new();
}

[BsonIgnoreExtraElements]
public sealed class AssetPurchase
{
    [BsonElement("purchase_order_number")]
    public string? PurchaseOrderNumber { get; set; }

    [BsonElement("invoice_number")]
    public string? InvoiceNumber { get; set; }

    [BsonElement("supplier_vendor")]
    public string? SupplierVendor { get; set; }

    [BsonElement("manufacturer")]
    public string? Manufacturer { get; set; }

    [BsonElement("purchase_date")]
    public DateTime? PurchaseDate { get; set; }

    [BsonElement("delivery_date")]
    public DateTime? DeliveryDate { get; set; }

    [BsonElement("purchase_cost")]
    public decimal? PurchaseCost { get; set; }

    [BsonElement("quantity")]
    public decimal? Quantity { get; set; }

    [BsonElement("quantity_unit")]
    public string? QuantityUnit { get; set; }

    [BsonElement("currency")]
    public string? Currency { get; set; }

    [BsonElement("tax_percent")]
    public decimal? TaxPercent { get; set; }

    // Calculated on the server: purchase cost plus tax.
    [BsonElement("total_cost")]
    public decimal? TotalCost { get; set; }

    [BsonElement("payment_terms")]
    public string? PaymentTerms { get; set; }

    [BsonElement("procurement_method")]
    public string? ProcurementMethod { get; set; }

    [BsonElement("project_reference")]
    public string? ProjectReference { get; set; }

    [BsonElement("remarks")]
    public string? Remarks { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class AssetWarranty
{
    [BsonElement("warranty_type")]
    public string? WarrantyType { get; set; }

    [BsonElement("warranty_start_date")]
    public DateTime? WarrantyStartDate { get; set; }

    // Calculated from the start date and period when both are given; otherwise as entered.
    [BsonElement("warranty_end_date")]
    public DateTime? WarrantyEndDate { get; set; }

    [BsonElement("warranty_period")]
    public int? WarrantyPeriod { get; set; }

    [BsonElement("warranty_period_unit")]
    public string? WarrantyPeriodUnit { get; set; }

    [BsonElement("amc_included")]
    public bool AmcIncluded { get; set; }

    [BsonElement("support_coverage")]
    public string? SupportCoverage { get; set; }

    [BsonElement("service_provider")]
    public string? ServiceProvider { get; set; }

    [BsonElement("warranty_reference")]
    public string? WarrantyReference { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class AssetDocument
{
    [BsonElement("file_name")]
    public string FileName { get; set; } = string.Empty;

    [BsonElement("url")]
    public string Url { get; set; } = string.Empty;

    [BsonElement("content_type")]
    public string? ContentType { get; set; }

    [BsonElement("file_size")]
    public long? FileSize { get; set; }

    [BsonElement("uploaded_at")]
    public DateTime? UploadedAt { get; set; }
}
