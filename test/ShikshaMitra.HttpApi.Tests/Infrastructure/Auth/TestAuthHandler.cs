using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.Security.Claims;

namespace ShikshaMitra.Infrastructure.Auth;

/// <summary>
/// In-process test authentication handler that bypasses OpenIddict.
/// Creates an authenticated principal with configurable claims.
/// </summary>
public class TestAuthHandler(
	IOptionsMonitor<AuthenticationSchemeOptions> options,
	ILoggerFactory logger,
	UrlEncoder encoder)
	: AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
	public const string SchemeName = "TestScheme";
	public const string TestUserId = "2e701e62-0953-4dd3-910b-dc6cc93ccb0d";
	public const string TestUserName = "admin";
	public const string TestEmail = "admin@abp.io";
	public const string TestTenantId = "11111111-1111-1111-1111-111111111111";
	public const string TestUserIdHeader = "X-Test-UserId";
	public const string TestEmailHeader = "X-Test-Email";
	public const string TestRolesHeader = "X-Test-Roles";
	public const string TestTenantIdHeader = "X-Test-TenantId";

	protected override Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		if (Context.Request.Headers.ContainsKey("X-Skip-Auth"))
		{
			return Task.FromResult(AuthenticateResult.NoResult());
		}

		var userId = TestUserId;
		if (Context.Request.Headers.TryGetValue(TestUserIdHeader, out var userIdOverride) &&
		    Guid.TryParse(userIdOverride.ToString(), out _))
		{
			userId = userIdOverride.ToString();
		}

		var email = TestEmail;
		if (Context.Request.Headers.TryGetValue(TestEmailHeader, out var emailOverride) &&
		    !string.IsNullOrWhiteSpace(emailOverride.ToString()))
		{
			email = emailOverride.ToString();
		}

		var claims = new List<Claim>
		{
			new(AbpClaimTypes.UserId, userId),
			new(AbpClaimTypes.UserName, TestUserName),
			new(AbpClaimTypes.Email, email),
			new(ClaimTypes.Name, TestUserName),
			new(ClaimTypes.NameIdentifier, userId),
		};

		var roles = new List<string> { "admin" };
		if (Context.Request.Headers.TryGetValue(TestRolesHeader, out var rolesOverride))
		{
			roles = rolesOverride
				.ToString()
				.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.ToList();
		}

		foreach (var role in roles)
		{
			claims.Add(new Claim(AbpClaimTypes.Role, role));
			claims.Add(new Claim(ClaimTypes.Role, role));
		}

		var tenantId = TestTenantId;
		if (Context.Request.Headers.TryGetValue(TestTenantIdHeader, out var tenantIdOverride))
		{
			tenantId = tenantIdOverride.ToString();
		}

		if (!string.IsNullOrWhiteSpace(tenantId) &&
		    !tenantId.Equals("host", StringComparison.OrdinalIgnoreCase))
		{
			claims.Add(new Claim(AbpClaimTypes.TenantId, tenantId));
		}

		var identity = new ClaimsIdentity(claims, SchemeName);
		var principal = new ClaimsPrincipal(identity);
		var ticket = new AuthenticationTicket(principal, SchemeName);

		return Task.FromResult(AuthenticateResult.Success(ticket));
	}
}
