using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.Infrastructure.Auth;

/// <summary>
/// Obtains a token via the OAuth2 Resource Owner Password grant.
/// Used for smoke tests against a running host where real auth is required.
/// Credentials are resolved from environment variables first, falling back to config.
/// </summary>
public sealed class PasswordGrantTokenProvider : ITestTokenProvider, IDisposable
{
	private readonly HttpClient _httpClient;
	private readonly string _authority;
	private readonly string _clientId;
	private readonly string _scope;
	private readonly string _userName;
	private readonly string _password;

	private string? _cachedToken;
	private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;

	public PasswordGrantTokenProvider(
		string authority,
		string clientId,
		string scope,
		string userName,
		string password,
		HttpMessageHandler? handler = null)
	{
		_authority = authority.TrimEnd('/');
		_clientId = clientId;
		_scope = scope;
		_userName = Environment.GetEnvironmentVariable("APITEST_USERNAME") ?? userName;
		_password = Environment.GetEnvironmentVariable("APITEST_PASSWORD") ?? password;

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
			["grant_type"] = "password",
			["client_id"] = _clientId,
			["scope"] = _scope,
			["username"] = _userName,
			["password"] = _password,
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
