using Microsoft.AspNetCore.Hosting;
using ShikshaMitra;
using ShikshaMitra.Finance.FeeStructureAssignment;

var repo = new DirectoryInfo(Directory.GetCurrentDirectory());
while (repo != null && !File.Exists(Path.Combine(repo.FullName, "src", "ShikshaMitra.HttpApi.Host", "ShikshaMitra.HttpApi.Host.csproj"))) repo = repo.Parent;
if (repo == null) throw new InvalidOperationException("Run the bridge from the backend repository.");
var hostRoot = Path.Combine(repo.FullName, "src", "ShikshaMitra.HttpApi.Host");
Environment.SetEnvironmentVariable("ASPNETCORE_TEST_CONTENTROOT_SHIKSHAMITRA_HTTPAPI_HOST", hostRoot);
var artifacts = Environment.GetEnvironmentVariable("COLLECTION_LATEFEE_ARTIFACTS") ?? Path.Combine(repo.FullName, ".artifacts", "collection-latefee");
Directory.CreateDirectory(artifacts);
Directory.SetCurrentDirectory(artifacts);
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:4395");
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
var app = builder.Build();
await using var factory = new CollectionLateFeeBrowserFactory { HostContentRoot = hostRoot };
using var client = factory.CreateClient();
var scenarioTests = new StudentFeeAssignmentApi_Tests(factory);
var seedContext = await scenarioTests.SeedCommonLateFeeBrowserScenarioAsync();
// The API test factory owns one in-memory SQLite connection. Serialize outer
// requests so concurrent UI lookups cannot configure that connection together.
using var requestGate = new SemaphoreSlim(1, 1);
app.MapGet("/fixture/health", () => Results.Ok(new { mode = "disposable-sqlite" }));
app.MapGet("/fixture/context", () => Results.Ok(seedContext));
app.MapPost("/fixture/verify/{policyId:guid}", async (Guid policyId, HttpContext context) =>
{
    await requestGate.WaitAsync(context.RequestAborted);
    try { return Results.Ok(await scenarioTests.VerifyCommonLateFeeBrowserScenarioAsync(policyId)); }
    finally { requestGate.Release(); }
});
app.MapGet("/api/auth/session", () => Results.Json<object?>(null));
app.MapFallback(async context =>
{
    if (!context.Request.Path.StartsWithSegments("/api/finance/collection-policies") &&
        !context.Request.Path.StartsWithSegments("/api/finance-routing-options"))
    {
        context.Response.StatusCode = 404;
        return;
    }
    using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), context.Request.Path + context.Request.QueryString);
    if (context.Request.ContentLength > 0)
    {
        request.Content = new StreamContent(context.Request.Body);
        request.Content.Headers.TryAddWithoutValidation("Content-Type", context.Request.ContentType);
    }
    await requestGate.WaitAsync(context.RequestAborted);
    try
    {
        using var response = await client.SendAsync(request, context.RequestAborted);
        context.Response.StatusCode = (int)response.StatusCode;
        foreach (var header in response.Headers.Concat(response.Content.Headers))
            if (!header.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                context.Response.Headers[header.Key] = header.Value.ToArray();
        await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
    }
    finally { requestGate.Release(); }
});
Console.WriteLine("Collection late-fee bridge: synthetic authentication and disposable in-memory SQLite.");
await app.RunAsync();

sealed class CollectionLateFeeBrowserFactory : FinanceReceiptApiTestFactory
{
    public required string HostContentRoot { get; init; }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseContentRoot(HostContentRoot);
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection([
            new("Logging:LogLevel:Default", "Warning")]));
    }
}
