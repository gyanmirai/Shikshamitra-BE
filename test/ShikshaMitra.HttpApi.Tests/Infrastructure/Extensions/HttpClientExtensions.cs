using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Infrastructure.Auth;

namespace ShikshaMitra.Infrastructure.Extensions;

/// <summary>
/// Convenience extensions for API test HttpClient usage.
/// </summary>
public static class HttpClientExtensions
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	/// <summary>
	/// Attaches a bearer token from the given provider to the client's default headers.
	/// </summary>
	public static async Task<HttpClient> WithTokenAsync(
		this HttpClient client,
		ITestTokenProvider tokenProvider,
		CancellationToken ct = default)
	{
		var token = await tokenProvider.GetTokenAsync(ct);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
		return client;
	}

	/// <summary>
	/// Sends a GET request and deserializes the JSON response body.
	/// Throws on non-success status codes.
	/// </summary>
	public static async Task<T> GetAsync<T>(
		this HttpClient client,
		string requestUri,
		CancellationToken ct = default)
	{
		var response = await client.GetAsync(requestUri, ct);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct)
			?? throw new InvalidOperationException($"Null deserialization from GET {requestUri}");
	}

	/// <summary>
	/// Sends a POST request with JSON body and deserializes the response.
	/// Returns the response message for status code assertions.
	/// </summary>
	public static async Task<(HttpResponseMessage Response, T? Body)> PostAsync<T>(
		this HttpClient client,
		string requestUri,
		object content,
		CancellationToken ct = default)
	{
		var response = await client.PostAsJsonAsync(requestUri, content, JsonOptions, ct);
		T? body = default;
		if (response.IsSuccessStatusCode)
		{
			body = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
		}
		return (response, body);
	}

	/// <summary>
	/// Sends a PUT request with JSON body and returns the response message.
	/// </summary>
	public static async Task<HttpResponseMessage> PutAsync(
		this HttpClient client,
		string requestUri,
		object content,
		CancellationToken ct = default)
	{
		return await client.PutAsJsonAsync(requestUri, content, JsonOptions, ct);
	}

	/// <summary>
	/// Sends a DELETE request and returns the response message.
	/// </summary>
	public static async Task<HttpResponseMessage> DeleteAsync(
		this HttpClient client,
		string requestUri,
		CancellationToken ct = default)
	{
		return await client.DeleteAsync(requestUri, ct);
	}

	/// <summary>
	/// Reads the response body as a deserialized object regardless of status code.
	/// Useful for inspecting error responses.
	/// </summary>
	public static async Task<T?> ReadBodyAsync<T>(
		this HttpResponseMessage response,
		CancellationToken ct = default)
	{
		return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
	}
}
