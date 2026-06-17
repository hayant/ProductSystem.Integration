using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProductSystem.Integration.Api;
using ProductSystem.Integration.Erp;
using ProductSystem.Integration.FakeErpClient;
using ProductSystem.Integration.Sync;

// For the smoke test the integration runs once and exits — easier to reason about
// than a long-running scheduler. In production you'd host this as a
// BackgroundService with a cron-like trigger (Quartz.NET, Hangfire, or a
// Kubernetes CronJob invoking this binary).
var host = Host.CreateApplicationBuilder(args);

// Typed HttpClient. BaseAddress and the X-Api-Key header are configured once
// here so the client code stays clean. IHttpClientFactory manages handler
// lifetimes correctly (avoids socket exhaustion AND stale-DNS issues).
var apiConfig = host.Configuration.GetSection("ProductSystemApi");
var baseUrl = apiConfig["BaseUrl"]
    ?? throw new InvalidOperationException("ProductSystemApi:BaseUrl is not configured.");
var apiKey = apiConfig["ApiKey"]
    ?? throw new InvalidOperationException("ProductSystemApi:ApiKey is not configured.");

host.Services.AddHttpClient<IProductsApiClient, ProductsApiClient>(client =>
{
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
    client.Timeout = TimeSpan.FromSeconds(10);
});

// FakeErpClient can emit a large synthetic changeset (Erp:SyntheticProductCount) for scale
// testing; 0 keeps the fixed demo catalog.
var syntheticCount = host.Configuration.GetValue("Erp:SyntheticProductCount", 0);
host.Services.AddSingleton<IErpClient>(_ => new FakeErpClient(syntheticCount));

// Batch size for the inbound sync — read from config so it's tunable per environment.
var batchSize = host.Configuration.GetValue("Sync:BatchSize", 500);
host.Services.AddScoped(sp => new SyncService(
    sp.GetRequiredService<IErpClient>(),
    sp.GetRequiredService<IProductsApiClient>(),
    sp.GetRequiredService<ILogger<SyncService>>(),
    batchSize));

var app = host.Build();

using var scope = app.Services.CreateScope();
var sync = scope.ServiceProvider.GetRequiredService<SyncService>();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

// A real implementation would load this from a sync_state table.
var watermarkDays = host.Configuration.GetValue("Sync:WatermarkDays", 7);
var watermark = DateTime.UtcNow.AddDays(-watermarkDays);

var result = await sync.RunInboundAsync(watermark);

logger.LogInformation(
    "Integration finished. Created={Created} Skipped={Skipped} Failed={Failed}",
    result.Created, result.Skipped, result.Failed);
