using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ShikshaMitra.Infrastructure.Auth;

namespace ShikshaMitra;

/// <summary>
/// Base class for hosted smoke tests that run against a real server
/// with real OpenIddict token authentication. Requires the target host
/// to be running. Tests inheriting this class should be marked with
/// [TestTier(TestTraits.Tiers.Smoke)].
/// </summary>
public abstract class ShikshaMitraSmokeTestBase : IDisposable
{
	protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	protected HttpClient Client { get; }
	protected ITestTokenProvider TokenProvider { get; }

	private readonly string _baseUrl;

	protected ShikshaMitraSmokeTestBase()
	{
		var config = new ConfigurationBuilder()
			.AddJsonFile("appsettings.json", optional: true)
			.AddEnvironmentVariables("APITEST_")
			.Build();

		var smoke = config.GetSection("ApiTest:Smoke");
		_baseUrl = Environment.GetEnvironmentVariable("APITEST_BASE_URL")
			?? smoke["BaseUrl"]
			?? "https://localhost:44332";

		var authority = Environment.GetEnvironmentVariable("APITEST_AUTHORITY")
			?? smoke["Authority"]
			?? _baseUrl;

		var handler = new HttpClientHandler
		{
			ServerCertificateCustomValidationCallback =
				HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
		};

		TokenProvider = new PasswordGrantTokenProvider(
			authority,
			smoke["ClientId"] ?? "ShikshaMitra_App",
			smoke["Scope"] ?? "ShikshaMitra",
			smoke["UserName"] ?? "admin",
			smoke["UserPassword"] ?? "1q2w3E*",
			handler);

		Client = new HttpClient(handler) { BaseAddress = new Uri(_baseUrl) };
	}

	/// <summary>
	/// Ensures the client has a valid bearer token attached.
	/// Call at the start of each test or in a setup method.
	/// </summary>
	protected async Task AuthenticateAsync()
	{
		var token = await TokenProvider.GetTokenAsync();
		Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
	}

	/// <summary>
	/// POST + deserialize shortcut with automatic auth.
	/// </summary>
	protected async Task<TResponse> PostAndReadAsync<TResponse>(string url, object body)
	{
		await AuthenticateAsync();
		var response = await Client.PostAsJsonAsync(url, body, JsonOptions);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions)
			?? throw new InvalidOperationException($"Null body from POST {url}");
	}

	/// <summary>
	/// GET + deserialize shortcut with automatic auth.
	/// </summary>
	protected async Task<TResponse> GetAndReadAsync<TResponse>(string url)
	{
		await AuthenticateAsync();
		var response = await Client.GetAsync(url);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions)
			?? throw new InvalidOperationException($"Null body from GET {url}");
	}

	public void Dispose()
	{
		Client.Dispose();
		(TokenProvider as IDisposable)?.Dispose();
		GC.SuppressFinalize(this);
	}
}
