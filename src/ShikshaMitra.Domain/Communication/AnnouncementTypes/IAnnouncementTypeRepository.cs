using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Communication.AnnouncementTypes;

/// <summary>
/// Repository interface for AnnouncementType entity
/// </summary>
public interface IAnnouncementTypeRepository : IRepository<AnnouncementType, Guid>
{
    /// <summary>
    /// Get announcement types by academic medium ID with optional search
    /// </summary>
    Task<List<AnnouncementType>> GetByAcademicMediumAsync(
        Guid? academicMediumId,
        string? search = null,
        int skipCount = 0,
        int maxResultCount = 10);

    /// <summary>
    /// Get count of announcement types by academic medium ID with optional search
    /// </summary>
    Task<long> GetCountByAcademicMediumAsync(
        Guid? academicMediumId,
        string? search = null);

    /// <summary>
    /// Check if announcement type name already exists
    /// </summary>
    Task<bool> ExistsByNameAsync(
        string announcementTypeName,
        Guid? academicMediumId = null,
        Guid? excludeId = null);
}
