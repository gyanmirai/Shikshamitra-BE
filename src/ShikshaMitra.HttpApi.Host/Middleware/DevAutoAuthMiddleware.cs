using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Common.Middleware;

/// <summary>
/// Development-only middleware that automatically injects a Bearer token and tenant header
/// into requests that have no Authorization header. This eliminates the need to manually
/// obtain and pass tokens during local development.
/// </summary>
public class DevAutoAuthMiddleware : ITransientDependency
{
    private static string? _cachedToken;
    private static DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;
    private static readonly SemaphoreSlim _lock = new(1, 1);

    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DevAutoAuthMiddleware> _logger;

    public DevAutoAuthMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<DevAutoAuthMiddleware> logger)
    {
        _next = next;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;

        // Skip: already has a token, or is an auth endpoint itself
        if (request.Headers.ContainsKey("Authorization") ||
            request.Path.StartsWithSegments("/connect"))
        {
            await _next(context);
            return;
        }

        var token = await GetTokenAsync();

        if (token is not null)
        {
            var tenant = _configuration["DevAutoAuth:Tenant"] ?? "ddvs";
            request.Headers.Authorization = $"Bearer {token}";
            request.Headers["__tenant"] = tenant;
        }

        await _next(context);
    }

    private async Task<string?> GetTokenAsync()
    {
        // Fast path: cached token is still valid
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiry)
            return _cachedToken;

        await _lock.WaitAsync();
        try
        {
            // Double-check after acquiring the lock
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiry)
                return _cachedToken;

            var authority = _configuration["AuthServer:Authority"] ?? "https://localhost:44332";
            var username = _configuration["DevAutoAuth:Username"] ?? "ddvs@gmail.com";
            var password = _configuration["DevAutoAuth:Password"] ?? "Temp@123";
            var clientId = _configuration["DevAutoAuth:ClientId"] ?? "ShikshaMitra_App";
            var tenant = _configuration["DevAutoAuth:Tenant"] ?? "ddvs";

            var formData = new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = username,
                ["password"] = password,
                ["client_id"] = clientId,
                ["scope"] = "openid profile email roles ShikshaMitra",
            };

            using var client = _httpClientFactory.CreateClient("DevAutoAuth");

            // __tenant must be an HTTP header so OpenIddict resolves the correct tenant DB
            var request = new HttpRequestMessage(HttpMethod.Post, $"{authority.TrimEnd('/')}/connect/token")
            {
                Content = new FormUrlEncodedContent(formData)
            };
            request.Headers.Add("__tenant", tenant);

            var response = await client.SendAsync(request);

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            _cachedToken = root.GetProperty("access_token").GetString();
            var expiresIn = root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;

            // Evict 60 s before actual expiry to avoid edge cases
            _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(expiresIn - 60);

            _logger.LogInformation("[DevAutoAuth] Token obtained for {Username} (expires in {ExpiresIn}s)", username, expiresIn);

            return _cachedToken;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[DevAutoAuth] Failed to obtain token — requests will proceed without authentication");
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }
}
