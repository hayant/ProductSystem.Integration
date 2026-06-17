namespace ProductSystem.Integration.Api;

// The Integration service's view of the Products API.
// Same role IProductService used to play, just over HTTP instead of in-process.
public interface IProductsApiClient
{
    Task<CreateProductOutcome> CreateAsync(CreateProductRequest request, CancellationToken ct = default);

    // Batch create — one HTTP round-trip for many products. Returns aggregate counts; the API
    // reports per-item outcomes so a duplicate SKU within the batch is an idempotent skip, not a
    // failure of the whole call.
    Task<BatchCreateOutcome> CreateBatchAsync(IReadOnlyList<CreateProductRequest> batch, CancellationToken ct = default);
}
