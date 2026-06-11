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

    public SyncService(IErpClient erp, IProductsApiClient api, ILogger<SyncService> logger)
    {
        _erp = erp;
        _api = api;
        _logger = logger;
    }

    public async Task<SyncResult> RunInboundAsync(DateTime since, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting inbound sync, watermark: {Since}", since);
        var changed = await _erp.FetchChangedProductsAsync(since, ct);
        _logger.LogInformation("ERP returned {Count} changed products", changed.Count);

        var created = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var erpProduct in changed)
        {
            var request = new CreateProductRequest(erpProduct.Sku, erpProduct.Name, erpProduct.Price);
            var outcome = await _api.CreateAsync(request, ct);

            switch (outcome)
            {
                case CreateProductOutcome.Created:
                    created++;
                    break;
                case CreateProductOutcome.Duplicate:
                    // Already synced on a previous run — idempotent no-op.
                    skipped++;
                    break;
                case CreateProductOutcome.Failed:
                    // The API client has already logged the detail.
                    // One bad record doesn't block the rest; in production this
                    // would also write to a sync_failures table for review.
                    failed++;
                    break;
            }
        }

        _logger.LogInformation(
            "Inbound sync complete. Created: {Created}, Skipped: {Skipped}, Failed: {Failed}",
            created, skipped, failed);

        return new SyncResult(created, skipped, failed);
    }
}

public record SyncResult(int Created, int Skipped, int Failed);
