namespace ProductSystem.Integration.Api;

// The Integration service's view of the Products API.
// Same role IProductService used to play, just over HTTP instead of in-process.
public interface IProductsApiClient
{
    Task<CreateProductOutcome> CreateAsync(CreateProductRequest request, CancellationToken ct = default);
}
