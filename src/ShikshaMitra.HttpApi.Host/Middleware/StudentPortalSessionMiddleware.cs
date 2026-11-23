using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using ShikshaMitra.Students.Portal;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Users;

namespace ShikshaMitra.Middleware;

public sealed class StudentPortalSessionMiddleware(StudentAppAccessEnforcement access, ICurrentUser user)
	: IMiddleware, ITransientDependency
{
	public async Task InvokeAsync(HttpContext context, RequestDelegate next)
	{
		var revision = context.User.FindFirst(StudentAppAccessEnforcement.SessionClaim)?.Value;
		if (context.Request.Path.StartsWithSegments("/api") && user.Id is { } id &&
			context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() == null &&
			(revision != null || user.Roles.Length == 0 || user.IsInRole("Parent") || user.IsInRole("Student")) &&
			!await access.IsCurrentSessionAsync(id, revision))
		{
			context.Response.StatusCode = StatusCodes.Status401Unauthorized;
			context.Response.Headers["X-ShikshaMitra-Portal-Session"] = "restricted";
			await context.Response.WriteAsJsonAsync(new { errorCode = "Students.Portal.SessionRestricted" });
			return;
		}

		await next(context);
	}
}
