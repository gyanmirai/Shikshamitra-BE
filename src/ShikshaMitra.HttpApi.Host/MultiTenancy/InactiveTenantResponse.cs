using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Volo.Abp;
using Volo.Abp.AspNetCore.MultiTenancy;

namespace ShikshaMitra.MultiTenancy;

public static class InactiveTenantResponse
{
	public static void Configure(AbpAspNetCoreMultiTenancyOptions options)
	{
		var fallback = options.MultiTenancyMiddlewareErrorPageBuilder;
		options.MultiTenancyMiddlewareErrorPageBuilder = async (context, exception) =>
		{
			if (exception is BusinessException { Code: "Volo.AbpIo.MultiTenancy:010001" }
				&& context.Request.Path.StartsWithSegments("/connect/token"))
			{
				context.Response.StatusCode = StatusCodes.Status400BadRequest;
				await context.Response.WriteAsJsonAsync(new { error = "invalid_grant", error_description = "School code was not found. Please check it and try again.", errorCode = "TENANT_NOT_FOUND" });
				return true;
			}
			if (exception is not BusinessException { Code: "Volo.AbpIo.MultiTenancy:010002" })
				return await fallback(context, exception);
			const string message = "This school is inactive. Contact the platform administrator.";
			if (context.Request.Path.StartsWithSegments("/connect/token"))
			{
				context.Response.StatusCode = StatusCodes.Status400BadRequest;
				await context.Response.WriteAsJsonAsync(new { error = "invalid_grant", error_description = message, errorCode = "TENANT_INACTIVE" });
				return true;
			}
			if (context.Request.Path.StartsWithSegments("/api"))
			{
				context.Response.StatusCode = StatusCodes.Status403Forbidden;
				await context.Response.WriteAsJsonAsync(new { title = message, status = 403, errorCode = "TENANT_INACTIVE" });
				return true;
			}
			return await fallback(context, exception);
		};
	}
}
