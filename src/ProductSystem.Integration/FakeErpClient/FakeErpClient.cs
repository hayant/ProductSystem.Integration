using ProductSystem.Integration.Erp;

namespace ProductSystem.Integration.FakeErpClient;

// A stand-in ERP client backed by a fixed in-memory catalog.
// Swapped in for tests and local dev; production would use an HttpErpClient.
public class FakeErpClient : IErpClient
{
    private static readonly List<ErpProduct> Catalog = new()
    {
        new("ERP-1001", "WIDGET-A",  "Blue widget",         19.99m, DateTime.UtcNow.AddDays(-2)),
        new("ERP-1002", "WIDGET-B",  "Red widget",          24.50m, DateTime.UtcNow.AddDays(-1)),
        new("ERP-1003", "GADGET-X",  "Deluxe gadget",       99.00m, DateTime.UtcNow.AddHours(-6)),
        new("ERP-1004", "SPROCKET-7", "Titanium sprocket", 149.95m, DateTime.UtcNow.AddMinutes(-30)),
    };

    public Task<IReadOnlyList<ErpProduct>> FetchChangedProductsAsync(DateTime since, CancellationToken ct = default)
    {
        // In reality this would be an HTTP call with a `since` query parameter.
        // Here we filter the fixed catalog to simulate the watermark behaviour.
        IReadOnlyList<ErpProduct> changed = Catalog
            .Where(p => p.ModifiedAt > since)
            .ToList();
        return Task.FromResult(changed);
    }
}
