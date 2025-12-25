using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

/// <summary>
/// Repository interface for LeavePolicy entity
/// </summary>
public interface ILeavePolicyRepository : IRepository<LeavePolicy, Guid>
{
	/// <summary>
	/// Check if a leave policy with the given name already exists within the organization (case-insensitive)
	/// </summary>
	/// <param name="educationMediumStageId">Organization identifier</param>
	/// <param name="policyName">Policy name</param>
	/// <param name="excludeId">ID to exclude from check (for update scenarios)</param>
	/// <returns>True if exists, false otherwise</returns>
	Task<bool> IsPolicyNameExistsAsync(Guid educationMediumStageId, string policyName, Guid? excludeId = null);
	
	/// <summary>
	/// Get leave policy by name within the organization (case-insensitive)
	/// </summary>
	Task<LeavePolicy?> FindByPolicyNameAsync(Guid educationMediumStageId, string policyName);
	
	/// <summary>
	/// Get paginated list of leave policies for an organization with LeaveType included
	/// </summary>
	/// <param name="educationMediumStageId">Organization identifier</param>
	/// <param name="skipCount">Number of records to skip</param>
	/// <param name="maxResultCount">Maximum records to return</param>
	/// <param name="sorting">Sorting expression</param>
	/// <param name="includeInactive">Include inactive leave policies</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>List of leave policies with LeaveType navigation property loaded</returns>
	Task<List<LeavePolicy>> GetListByMediumStageIdAsync(
		Guid educationMediumStageId,
		int skipCount = 0,
		int maxResultCount = int.MaxValue,
		string? sorting = null,
		bool includeInactive = false,
		CancellationToken cancellationToken = default);
	
	/// <summary>
	/// Get count of leave policies for an organization
	/// </summary>
	/// <param name="educationMediumStageId">Organization identifier</param>
	/// <param name="includeInactive">Include inactive leave policies</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Count</returns>
	Task<long> GetCountByMediumStageIdAsync(
		Guid educationMediumStageId,
		bool includeInactive = false,
		CancellationToken cancellationToken = default);
	
	/// <summary>
	/// Get a leave policy by ID with LeaveType navigation property loaded
	/// </summary>
	Task<LeavePolicy?> GetWithLeaveTypeAsync(Guid id, CancellationToken cancellationToken = default);
}
