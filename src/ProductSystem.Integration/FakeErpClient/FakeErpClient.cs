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

    private readonly int _syntheticCount;

    // syntheticCount > 0 makes the client emit that many generated products instead of the fixed
    // demo catalog — used to exercise batching at scale without a real ERP.
    public FakeErpClient(int syntheticCount = 0) => _syntheticCount = syntheticCount;

    public Task<IReadOnlyList<ErpProduct>> FetchChangedProductsAsync(DateTime since, CancellationToken ct = default)
    {
        // In reality this would be an HTTP call with a `since` query parameter.
        // Here we filter the fixed catalog to simulate the watermark behaviour.
        IReadOnlyList<ErpProduct> changed = _syntheticCount > 0
            ? GenerateSynthetic(_syntheticCount)
            : Catalog.Where(p => p.ModifiedAt > since).ToList();
        return Task.FromResult(changed);
    }

    // Deterministic synthetic catalog with unique, padded SKUs (SYN-000001, ...). All are dated
    // one hour ago so they fall inside any reasonable watermark.
    private static List<ErpProduct> GenerateSynthetic(int count)
    {
        var modifiedAt = DateTime.UtcNow.AddHours(-1);
        var products = new List<ErpProduct>(count);
        for (var i = 1; i <= count; i++)
        {
            products.Add(new ErpProduct(
                ExternalId: $"ERP-SYN-{i:D6}",
                Sku: $"SYN-{i:D6}",
                Name: $"Synthetic product {i}",
                Price: 9.99m + (i % 1000),
                ModifiedAt: modifiedAt));
        }
        return products;
    }
}
