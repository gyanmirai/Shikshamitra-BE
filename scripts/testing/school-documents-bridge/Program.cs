using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.TenantSettings.SchoolDocuments;

var repo = new DirectoryInfo(Directory.GetCurrentDirectory());
while (repo != null && !File.Exists(Path.Combine(repo.FullName, "src", "ShikshaMitra.HttpApi.Host", "ShikshaMitra.HttpApi.Host.csproj"))) repo = repo.Parent;
if (repo == null) throw new InvalidOperationException("Run the bridge from the backend repository.");
var hostRoot = Path.Combine(repo.FullName, "src", "ShikshaMitra.HttpApi.Host");
var artifacts = Path.Combine(repo.FullName, ".artifacts", "school-documents");
Directory.CreateDirectory(artifacts);
Directory.SetCurrentDirectory(artifacts);
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:4394");
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
var app = builder.Build();
await using var factory = new SchoolDocumentsTestFactory
{
	HostContentRoot = hostRoot
};
var tenantId = Guid.NewGuid();
await factory.SeedTenantAsync(tenantId);
using var client = factory.CreateClient();
client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, tenantId.ToString());
var failNextSave = false;
var failNextPreview = false;
app.MapPost("/fixture/fail-save", () => { failNextSave = true; return Results.Ok(); });
app.MapPost("/fixture/fail-preview", () => { failNextPreview = true; return Results.Ok(); });
app.MapGet("/fixture/health", () => Results.Ok(new { mode = "disposable-sqlite", tenantId }));
app.MapGet("/api/auth/session", () => Results.Json<object?>(null));
app.MapFallback(async context =>
{
	if (!context.Request.Path.StartsWithSegments("/api/institute-setup/school-documents"))
	{
		context.Response.StatusCode = 404;
		return;
	}
	if ((failNextSave && context.Request.Method is "POST" or "PUT") ||
		(failNextPreview && context.Request.Path.Value!.Contains("/files/", StringComparison.Ordinal)))
	{
		failNextSave = false;
		failNextPreview = false;
		context.Response.StatusCode = 503;
		await context.Response.WriteAsJsonAsync(new { detail = "Synthetic temporary failure. Retry the request." });
		return;
	}
	using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), context.Request.Path + context.Request.QueryString);
	if (context.Request.ContentLength > 0)
	{
		request.Content = new StreamContent(context.Request.Body);
		request.Content.Headers.TryAddWithoutValidation("Content-Type", context.Request.ContentType);
	}
	var requestedAccess = context.Request.Headers["X-Fixture-Access"].ToString();
	if (requestedAccess == "view") request.Headers.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, SchoolDocumentPermissions.View);
	using var response = await client.SendAsync(request, context.RequestAborted);
	context.Response.StatusCode = (int)response.StatusCode;
	foreach (var header in response.Headers.Concat(response.Content.Headers))
		if (!header.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
			context.Response.Headers[header.Key] = header.Value.ToArray();
	await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
});
Console.WriteLine("School Documents bridge: synthetic authentication, isolated in-memory SQLite, no school database.");
await app.RunAsync();
