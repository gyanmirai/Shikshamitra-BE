using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.Security.UserScope;

public sealed record ScopeReportingEmployee(Guid Id, Guid? UserId, Guid? ReportingManagerId);

/// <summary>Tenant-filtered projections for authorization, without employee profile graphs.</summary>
public interface IUserScopeResolutionRepository
{
    Task<List<string>?> GetRoleNamesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<ScopeReportingEmployee>> GetReportingHierarchyAsync(CancellationToken cancellationToken = default);
    Task<List<UserScopeAssignment>> GetAssignmentsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);
}
