using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShikshaMitra.Security.UserScope;

/// <summary>
/// Resolves whether a login manages one or more employees through the HR
/// reporting hierarchy. This grants read-only derived Academic access only.
/// </summary>
public interface IReportingManagerAccessService
{
	Task<bool> HasReporteesAsync(Guid userId);

	Task<IReadOnlyCollection<Guid>> GetReporteeEmployeeIdsAsync(Guid userId);

	Task InvalidateAsync(IEnumerable<Guid> userIds);
}
