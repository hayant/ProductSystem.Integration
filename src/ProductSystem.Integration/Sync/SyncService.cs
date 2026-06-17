using Microsoft.Extensions.Logging;
using ProductSystem.Integration.Api;
using ProductSystem.Integration.Erp;

namespace ProductSystem.Integration.Sync;

// Orchestrates the inbound phase of the daily sync.
// Reads from the ERP, writes to the Products API over HTTP.
// Duplicates are skipped idempotently (API returns 409 on existing SKUs).
//
// In production this would also:
//  - Persist the watermark to a sync_state store instead of taking it as a parameter
//  - Upsert instead of skip-if-exists
//  - Do outbound sync too
//  - Track per-run metrics in a sync_runs table
public class SyncService
{
    private readonly IErpClient _erp;
    private readonly IProductsApiClient _api;
    private readonly ILogger<SyncService> _logger;
    private readonly int _batchSize;

    public SyncService(IErpClient erp, IProductsApiClient api, ILogger<SyncService> logger, int batchSize = 500)
    {
        _erp = erp;
        _api = api;
        _logger = logger;
        // Guard against a misconfigured non-positive batch size.
        _batchSize = batchSize > 0 ? batchSize : 500;
    }

    public async Task<SyncResult> RunInboundAsync(DateTime since, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting inbound sync, watermark: {Since}, batch size: {BatchSize}", since, _batchSize);
        var changed = await _erp.FetchChangedProductsAsync(since, ct);
        _logger.LogInformation("ERP returned {Count} changed products", changed.Count);

        var created = 0;
        var skipped = 0;
        var failed = 0;

        // Send the changeset in batches rather than one request per product: N changed products
        // become ceil(N / batchSize) HTTP round-trips. A 409-style duplicate within a batch is an
        // idempotent skip, and a failed batch is counted but doesn't abort the run.
        foreach (var chunk in changed.Chunk(_batchSize))
        {
            var batch = chunk
                .Select(p => new CreateProductRequest(p.Sku, p.Name, p.Price))
                .ToList();

            var outcome = await _api.CreateBatchAsync(batch, ct);
            created += outcome.Created;
            skipped += outcome.Duplicate;  // already synced on a previous run — idempotent no-op
            failed += outcome.Failed;      // invalid records + any whole-batch transport/server failure
        }

        _logger.LogInformation(
            "Inbound sync complete. Created: {Created}, Skipped: {Skipped}, Failed: {Failed}",
            created, skipped, failed);

        return new SyncResult(created, skipped, failed);
    }
}

public record SyncResult(int Created, int Skipped, int Failed);
