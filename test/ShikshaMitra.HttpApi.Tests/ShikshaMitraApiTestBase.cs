using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace ShikshaMitra;

/// <summary>
/// Base class for in-process API tests. Provides a pre-configured <see cref="HttpClient"/>
/// that targets the test server with test authentication enabled.
/// </summary>
public abstract class ShikshaMitraApiTestBase : IClassFixture<ShikshaMitraApiTestFactory>
{
	protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	protected ShikshaMitraApiTestFactory Factory { get; }
	protected HttpClient Client { get; }

	protected ShikshaMitraApiTestBase(ShikshaMitraApiTestFactory factory)
	{
		Factory = factory;
		Client = factory.CreateClient();
	}

	/// <summary>
	/// Creates an HttpClient that sends requests without authentication.
	/// Useful for testing 401/403 behavior.
	/// </summary>
	protected HttpClient CreateAnonymousClient()
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add("X-Skip-Auth", "true");
		return client;
	}

	/// <summary>
	/// POST + deserialize shortcut that asserts a success status code.
	/// </summary>
	protected async Task<TResponse> PostAndReadAsync<TResponse>(string url, object body)
	{
		var response = await Client.PostAsJsonAsync(url, body, JsonOptions);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions)
			?? throw new InvalidOperationException($"Null body from POST {url}");
	}

	/// <summary>
	/// GET + deserialize shortcut that asserts a success status code.
	/// </summary>
	protected async Task<TResponse> GetAndReadAsync<TResponse>(string url)
	{
		var response = await Client.GetAsync(url);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions)
			?? throw new InvalidOperationException($"Null body from GET {url}");
	}
}
