namespace ProductSystem.Integration.Api;

// Mirrors the API's CreateProductRequest contract.
// Intentionally re-declared here — the Integration service must not depend on
// ProductSystem.Shared. The HTTP contract is the only coupling between services.
public record CreateProductRequest(string Sku, string Name, decimal Price);
