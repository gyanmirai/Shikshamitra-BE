using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

/// <summary>
/// Repository interface for LeaveType entity
/// </summary>
public interface ILeaveTypeRepository : IRepository<LeaveType, Guid>
{
	/// <summary>
	/// Check if a leave type with the given name already exists within the organization (case-insensitive)
	/// </summary>
	/// <param name="educationMediumStageId">Organization identifier</param>
	/// <param name="type">Leave type name</param>
	/// <param name="excludeId">ID to exclude from check (for update scenarios)</param>
	/// <returns>True if exists, false otherwise</returns>
	Task<bool> IsTypeExistsAsync(Guid educationMediumStageId, string type, Guid? excludeId = null);
	
	/// <summary>
	/// Get leave type by name within the organization (case-insensitive)
	/// </summary>
	Task<LeaveType?> FindByTypeAsync(Guid educationMediumStageId, string type);
	
	/// <summary>
	/// Get paginated list of leave types for an organization
	/// </summary>
	/// <param name="educationMediumStageId">Organization identifier</param>
	/// <param name="skipCount">Number of records to skip</param>
	/// <param name="maxResultCount">Maximum records to return</param>
	/// <param name="sorting">Sorting expression</param>
	/// <param name="includeInactive">Include inactive leave types</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>List of leave types</returns>
	Task<List<LeaveType>> GetListByMediumStageIdAsync(
		Guid educationMediumStageId,
		int skipCount = 0,
		int maxResultCount = int.MaxValue,
		string? sorting = null,
		bool includeInactive = false,
		CancellationToken cancellationToken = default);
	
	/// <summary>
	/// Get count of leave types for an organization
	/// </summary>
	/// <param name="educationMediumStageId">Organization identifier</param>
	/// <param name="includeInactive">Include inactive leave types</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Count</returns>
	Task<long> GetCountByMediumStageIdAsync(
		Guid educationMediumStageId,
		bool includeInactive = false,
		CancellationToken cancellationToken = default);
}
