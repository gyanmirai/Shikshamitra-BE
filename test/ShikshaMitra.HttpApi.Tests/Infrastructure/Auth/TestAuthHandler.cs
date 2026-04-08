using System.Collections.Generic;
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
	public const string TestTenantId = "";

	protected override Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		if (Context.Request.Headers.ContainsKey("X-Skip-Auth"))
		{
			return Task.FromResult(AuthenticateResult.NoResult());
		}

		var claims = new List<Claim>
		{
			new(AbpClaimTypes.UserId, TestUserId),
			new(AbpClaimTypes.UserName, TestUserName),
			new(AbpClaimTypes.Email, TestEmail),
			new(ClaimTypes.Name, TestUserName),
			new(ClaimTypes.NameIdentifier, TestUserId),
		};

		if (!string.IsNullOrEmpty(TestTenantId))
		{
			claims.Add(new Claim(AbpClaimTypes.TenantId, TestTenantId));
		}

		var identity = new ClaimsIdentity(claims, SchemeName);
		var principal = new ClaimsPrincipal(identity);
		var ticket = new AuthenticationTicket(principal, SchemeName);

		return Task.FromResult(AuthenticateResult.Success(ticket));
	}
}
