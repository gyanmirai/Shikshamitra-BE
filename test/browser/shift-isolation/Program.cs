using System.Net;
using System.Net.Http.Json;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.Infrastructure.Auth;

await using var factory = new BrowserFactory();
var a = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
var b = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
await factory.SeedTenantAsync(a, "Synthetic DDVS");
await factory.SeedTenantAsync(b, "Synthetic Swaminarayan");
using var client = factory.CreateClient();
foreach (var (tenant, names) in new[] {
	(a.ToString(), new[] { "Mayur shift", "Full Day Shift", "Afternoon Shift", "Morning Shift" }),
	("host", new[] { "Legacy unassigned shift" }) })
{
	foreach (var name in names)
	{
		using var request = new HttpRequestMessage(HttpMethod.Post, "/api/app/shift-management");
		request.Headers.Add(TestAuthHandler.TestTenantIdHeader, tenant);
		request.Content = JsonContent.Create(new { shiftName = name, startTime = "08:00:00", endTime = "16:00:00" });
		using var response = await client.SendAsync(request);
		response.EnsureSuccessStatusCode();
	}
}
using var listener = new HttpListener();
listener.Prefixes.Add("http://127.0.0.1:4391/");
listener.Start();
Console.WriteLine("Shift browser bridge ready: disposable SQLite, real ABP HTTP endpoints, synthetic authentication.");
while (true)
{
	var context = await listener.GetContextAsync();
	try
	{
		var tenant = context.Request.Headers["Authorization"]?.Replace("Bearer ", "");
		if (tenant != a.ToString() && tenant != b.ToString())
		{
			context.Response.StatusCode = 401;
			context.Response.Close();
			continue;
		}
		using var request = new HttpRequestMessage(new HttpMethod(context.Request.HttpMethod), context.Request.RawUrl);
		request.Headers.Add(TestAuthHandler.TestTenantIdHeader, tenant);
		if (context.Request.HasEntityBody)
		{
			using var reader = new StreamReader(context.Request.InputStream);
			request.Content = new StringContent(await reader.ReadToEndAsync(), System.Text.Encoding.UTF8, "application/json");
		}
		using var response = await client.SendAsync(request);
		context.Response.StatusCode = (int)response.StatusCode;
		context.Response.ContentType = "application/json";
		await context.Response.OutputStream.WriteAsync(await response.Content.ReadAsByteArrayAsync());
		context.Response.Close();
	}
	catch (Exception exception)
	{
		Console.WriteLine(exception.Message);
		context.Response.StatusCode = 500;
		context.Response.Close();
	}
}

class BrowserFactory : ShiftTenantTestFactory
{
	protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		Microsoft.AspNetCore.Hosting.HostingAbstractionsWebHostBuilderExtensions.UseContentRoot(
			builder, Path.GetFullPath("src/ShikshaMitra.HttpApi.Host"));
	}
}
