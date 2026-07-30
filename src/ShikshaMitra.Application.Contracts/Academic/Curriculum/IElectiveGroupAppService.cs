using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Application service interface for managing elective groups within education mediums
/// </summary>
public interface IElectiveGroupAppService : IApplicationService
{
	/// <summary>
	/// Get all elective groups for a specific education medium
	/// </summary>
	/// <param name="academicMediumId">Education medium ID</param>
	/// <param name="searchTerm">Optional search term to filter by group name</param>
	/// <returns>List of elective groups with associated subjects</returns>
	Task<List<ElectiveGroupDto>> GetListAsync(Guid academicMediumId, string? searchTerm = null, bool includeArchived = false);

	/// <summary>
	/// Get a single elective group by ID
	/// </summary>
	/// <param name="academicMediumId">Education medium ID</param>
	/// <param name="id">Elective group ID</param>
	/// <returns>Elective group details</returns>
	Task<ElectiveGroupDto> GetAsync(Guid academicMediumId, Guid id);

	/// <summary>
	/// Create a new elective group
	/// </summary>
	/// <param name="academicMediumId">Education medium ID</param>
	/// <param name="input">Elective group creation data</param>
	/// <returns>Created elective group details</returns>
	Task<ElectiveGroupDto> CreateAsync(Guid academicMediumId, CreateUpdateElectiveGroupDto input);

	/// <summary>
	/// Update an existing elective group
	/// </summary>
	/// <param name="academicMediumId">Education medium ID</param>
	/// <param name="id">Elective group ID</param>
	/// <param name="input">Updated elective group data</param>
	/// <returns>Updated elective group details</returns>
	Task<ElectiveGroupDto> UpdateAsync(
		Guid academicMediumId,
		Guid id,
		CreateUpdateElectiveGroupDto input
	);

	/// <summary>
	/// Delete an elective group
	/// </summary>
	/// <param name="academicMediumId">Education medium ID</param>
	/// <param name="id">Elective group ID</param>
	Task DeleteAsync(Guid academicMediumId, Guid id);

	Task<ElectiveGroupDto> ArchiveAsync(
		Guid academicMediumId,
		Guid id,
		ElectiveGroupStatusChangeDto input
	);

	Task<ElectiveGroupDto> RestoreAsync(Guid academicMediumId, Guid id);

	Task<ElectiveGroupDuplicateCheckResultDto> CheckDuplicateAsync(
		Guid academicMediumId,
		ElectiveGroupDuplicateCheckDto input
	);

	Task<ElectiveGroupUsageDetailDto> GetUsageDetailsAsync(Guid academicMediumId, Guid id);

	Task<List<ElectiveGroupAuditLogDto>> GetAuditLogsAsync(Guid academicMediumId, Guid id);
}
