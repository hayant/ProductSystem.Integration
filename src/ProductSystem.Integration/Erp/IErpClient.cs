namespace ProductSystem.Integration.Erp;

// The ERP abstraction. Everything ERP-specific — auth, serialization, quirks —
// lives behind this interface. The sync logic only sees clean domain-shaped data.
// For tests or local dev, you swap in the FakeErpClient. For production,
// you'd write an HttpErpClient that talks to the real system.
public interface IErpClient
{
    Task<IReadOnlyList<ErpProduct>> FetchChangedProductsAsync(DateTime since, CancellationToken ct = default);
}
