using System.Net;
using System.Net.Http.Json;
using ShikshaMitra.TenantSettings;

await using var factory = new BrowserFactory();
using var client = factory.CreateClient();
var stageId = await factory.SeedStageAsync();
foreach (var name in new[] { "Class 1", "Class 3" })
{
	using var response = await client.PostAsJsonAsync("/api/app/standard", new {
		name, ageLowerLimit = 6, educationMediumStageId = stageId });
	response.EnsureSuccessStatusCode();
}
using var listener = new HttpListener();
listener.Prefixes.Add("http://127.0.0.1:4392/");
listener.Start();
Console.WriteLine("Academic Setup browser bridge ready: disposable SQLite, real ABP HTTP, synthetic authentication.");
while (true)
{
	var context = await listener.GetContextAsync();
	try
	{
		if (context.Request.RawUrl == "/fixture/context")
		{
			context.Response.ContentType = "application/json";
			await System.Text.Json.JsonSerializer.SerializeAsync(context.Response.OutputStream, new { stageId });
			context.Response.Close();
			continue;
		}
		using var request = new HttpRequestMessage(new HttpMethod(context.Request.HttpMethod), context.Request.RawUrl);
		if (context.Request.HasEntityBody)
		{
			using var reader = new StreamReader(context.Request.InputStream);
			request.Content = new StringContent(await reader.ReadToEndAsync(), System.Text.Encoding.UTF8, "application/json");
		}
		using var response = await client.SendAsync(request);
		context.Response.StatusCode = (int)response.StatusCode;
		context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
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

class BrowserFactory : AcademicSetupValidationFactory
{
	protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		Microsoft.AspNetCore.Hosting.HostingAbstractionsWebHostBuilderExtensions.UseContentRoot(
			builder, Path.GetFullPath("src/ShikshaMitra.HttpApi.Host"));
	}
}
