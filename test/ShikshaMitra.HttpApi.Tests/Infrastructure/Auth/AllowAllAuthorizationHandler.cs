using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace ShikshaMitra.Infrastructure.Auth;

/// <summary>
/// Authorization handler that satisfies ALL requirements unconditionally.
/// Used in in-process API tests where we bypass permission checks and
/// instead test authorization behavior in dedicated tests and smoke suites.
/// </summary>
public class AllowAllAuthorizationHandler : IAuthorizationHandler
{
	public Task HandleAsync(AuthorizationHandlerContext context)
	{
		foreach (var requirement in context.PendingRequirements.ToList())
		{
			context.Succeed(requirement);
		}
		return Task.CompletedTask;
	}
}
