using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Groups;

/// <summary>
/// Repository interface for Group entity
/// </summary>
public interface IGroupRepository : IRepository<Group, Guid>
{
    /// <summary>
    /// Gets a group with all related entities (rules and members)
    /// </summary>
    Task<Group?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all groups with their details for a tenant
    /// </summary>
    Task<List<Group>> GetListWithDetailsAsync(
        Guid academicYearId,
        string? filter = null,
        int skipCount = 0,
        int maxResultCount = int.MaxValue,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of groups matching the filter
    /// </summary>
    Task<long> GetCountAsync(
        Guid academicYearId,
        string? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a group with the given name exists
    /// </summary>
    Task<bool> ExistsByNameAsync(
        Guid academicYearId,
        string name,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default);
}
