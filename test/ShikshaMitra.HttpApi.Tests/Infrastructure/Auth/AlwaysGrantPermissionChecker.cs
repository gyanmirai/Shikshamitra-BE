using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Infrastructure.Auth;

/// <summary>
/// Replaces ABP's default permission checker in API tests so that
/// all permission-based [Authorize(permission)] attributes pass.
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IPermissionChecker))]
public class AlwaysGrantPermissionChecker(IHttpContextAccessor httpContextAccessor)
	: IPermissionChecker, ITransientDependency
{
	public const string TestPermissionsHeader = "X-Test-Permissions";

	public Task<bool> IsGrantedAsync(string name) => Task.FromResult(IsGranted(name));

	public Task<bool> IsGrantedAsync(ClaimsPrincipal? claimsPrincipal, string name) =>
		Task.FromResult(IsGranted(name));

	public Task<MultiplePermissionGrantResult> IsGrantedAsync(string[] names)
	{
		var result = new MultiplePermissionGrantResult();
		foreach (var name in names)
		{
			result.Result[name] = IsGranted(name) ? PermissionGrantResult.Granted : PermissionGrantResult.Prohibited;
		}
		return Task.FromResult(result);
	}

	public Task<MultiplePermissionGrantResult> IsGrantedAsync(ClaimsPrincipal? claimsPrincipal, string[] names)
	{
		return IsGrantedAsync(names);
	}

	private bool IsGranted(string name)
	{
		var headers = httpContextAccessor.HttpContext?.Request.Headers;
		if (headers == null || !headers.ContainsKey(TestPermissionsHeader))
		{
			return true;
		}

		var permissions = headers[TestPermissionsHeader]
			.ToString()
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

		return permissions.Contains(name, StringComparer.OrdinalIgnoreCase);
	}
}
