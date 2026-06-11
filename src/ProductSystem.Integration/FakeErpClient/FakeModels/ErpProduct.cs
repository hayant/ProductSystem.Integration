namespace ProductSystem.Integration.Erp;

// ErpProduct is distinct from Product — it's the ERP's view, which may differ from ours.
// Mapping happens in the sync service, making that translation an explicit boundary.
public record ErpProduct(string ExternalId, string Sku, string Name, decimal Price, DateTime ModifiedAt);
