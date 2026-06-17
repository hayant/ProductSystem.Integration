using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace ProductSystem.Integration.Api;

// Typed HttpClient — the BaseAddress and X-Api-Key header are configured
// once at registration time (see Program.cs), so callers just say "create this".
//
// We translate the relevant HTTP status codes into a small CreateProductOutcome
// enum. Everything outside the happy path / duplicate path is logged and
// surfaced as Failed — the sync run reports the count and moves on.
public class ProductsApiClient : IProductsApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<ProductsApiClient> _logger;

    public ProductsApiClient(HttpClient http, ILogger<ProductsApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<CreateProductOutcome> CreateAsync(CreateProductRequest request, CancellationToken ct = default)
    {
        try
        {
            // BaseAddress is set to ".../api/v1/" — note the trailing slash so this
            // relative URI resolves correctly.
            using var response = await _http.PostAsJsonAsync("products", request, ct);

            if (response.IsSuccessStatusCode)
            {
                return CreateProductOutcome.Created;
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                // 409 from the API means duplicate SKU — idempotent no-op for the sync.
                return CreateProductOutcome.Duplicate;
            }

            // Read the structured error body when available — the API returns
            // { error, message } for 400 and { error, message, sku } for 409.
            var body = await SafeReadBody(response, ct);
            _logger.LogWarning(
                "Create product failed for SKU {Sku}: HTTP {Status} {Body}",
                request.Sku, (int)response.StatusCode, body);

            return CreateProductOutcome.Failed;
        }
        catch (HttpRequestException ex)
        {
            // Network-level failure (API down, DNS, connection reset).
            _logger.LogError(ex, "HTTP error calling Products API for SKU {Sku}", request.Sku);
            return CreateProductOutcome.Failed;
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // HttpClient throws TaskCanceledException on timeout (not just cancellation).
            _logger.LogError(ex, "Timeout calling Products API for SKU {Sku}", request.Sku);
            return CreateProductOutcome.Failed;
        }
    }

    public async Task<BatchCreateOutcome> CreateBatchAsync(IReadOnlyList<CreateProductRequest> batch, CancellationToken ct = default)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync("products/batch", batch, ct);

            if (response.IsSuccessStatusCode)
            {
                // The API answers 200 with per-item outcomes even when some records were
                // duplicates/invalid. invalid records are folded into Failed alongside any
                // transport error, mirroring the single-create mapping.
                var body = await response.Content.ReadFromJsonAsync<BatchCreateResponse>(ct);
                if (body is null)
                {
                    _logger.LogWarning("Batch create returned {Status} with an unreadable body for {Count} products",
                        (int)response.StatusCode, batch.Count);
                    return new BatchCreateOutcome(0, 0, batch.Count);
                }

                return new BatchCreateOutcome(body.Created, body.Duplicate, body.Invalid);
            }

            // A non-2xx means the whole batch request failed (e.g. malformed body, auth, server
            // error) — count every product in it as Failed; the next run retries.
            var errorBody = await SafeReadBody(response, ct);
            _logger.LogWarning(
                "Batch create failed for {Count} products: HTTP {Status} {Body}",
                batch.Count, (int)response.StatusCode, errorBody);
            return new BatchCreateOutcome(0, 0, batch.Count);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling batch Products API for {Count} products", batch.Count);
            return new BatchCreateOutcome(0, 0, batch.Count);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // HttpClient throws TaskCanceledException on timeout (not just cancellation).
            _logger.LogError(ex, "Timeout calling batch Products API for {Count} products", batch.Count);
            return new BatchCreateOutcome(0, 0, batch.Count);
        }
    }

    private static async Task<string> SafeReadBody(HttpResponseMessage response, CancellationToken ct)
    {
        try { return await response.Content.ReadAsStringAsync(ct); }
        catch { return "<unreadable>"; }
    }

    // Mirrors the API's batch response shape — re-declared here because the worker must not
    // reference ProductSystem.Shared. Only the aggregate counts are needed by the sync.
    private record BatchCreateResponse(int Created, int Duplicate, int Invalid);
}
