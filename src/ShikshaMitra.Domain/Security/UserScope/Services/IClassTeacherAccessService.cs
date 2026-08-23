using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShikshaMitra.Security.UserScope;

/// <summary>
/// Resolves whether a login derives Academic operational access from an active
/// class-teacher assignment. This is intentionally separate from general user
/// scope: manually assigned scope must not turn into class-teacher permissions.
/// </summary>
public interface IClassTeacherAccessService
{
	Task<bool> HasAssignmentAsync(Guid userId);

	Task InvalidateAsync(IEnumerable<Guid> userIds);
}
