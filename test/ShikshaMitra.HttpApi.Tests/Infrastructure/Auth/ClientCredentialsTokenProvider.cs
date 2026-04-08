using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.Infrastructure.Auth;

/// <summary>
/// Obtains a token via the OAuth2 Client Credentials grant.
/// Used for machine-to-machine service-level API testing.
/// Credentials are resolved from environment variables first, falling back to config.
/// </summary>
public sealed class ClientCredentialsTokenProvider : ITestTokenProvider, IDisposable
{
	private readonly HttpClient _httpClient;
	private readonly string _authority;
	private readonly string _clientId;
	private readonly string _clientSecret;
	private readonly string _scope;

	private string? _cachedToken;
	private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;

	public ClientCredentialsTokenProvider(
		string authority,
		string clientId,
		string clientSecret,
		string scope,
		HttpMessageHandler? handler = null)
	{
		_authority = authority.TrimEnd('/');
		_clientId = Environment.GetEnvironmentVariable("APITEST_CLIENT_ID") ?? clientId;
		_clientSecret = Environment.GetEnvironmentVariable("APITEST_CLIENT_SECRET") ?? clientSecret;
		_scope = scope;

		_httpClient = handler is not null ? new HttpClient(handler) : new HttpClient();
	}

	public async Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
	{
		if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiry)
		{
			return _cachedToken;
		}

		var tokenEndpoint = $"{_authority}/connect/token";

		var request = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["grant_type"] = "client_credentials",
			["client_id"] = _clientId,
			["client_secret"] = _clientSecret,
			["scope"] = _scope,
		});

		var response = await _httpClient.PostAsync(tokenEndpoint, request, cancellationToken);
		response.EnsureSuccessStatusCode();

		var json = await response.Content.ReadAsStringAsync(cancellationToken);
		using var doc = JsonDocument.Parse(json);

		_cachedToken = doc.RootElement.GetProperty("access_token").GetString()
			?? throw new InvalidOperationException("Token response missing access_token");

		var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var exp)
			? exp.GetInt32()
			: 3600;

		_tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(expiresIn - 60);

		return _cachedToken;
	}

	public void Dispose() => _httpClient.Dispose();
}
