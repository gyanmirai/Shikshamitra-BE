using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

/// <summary>
/// Repository interface for LeavePolicy entity
/// </summary>
public interface ILeavePolicyRepository : IRepository<LeavePolicy, Guid>
{
	/// <summary>
	/// Check if a leave policy with the given name already exists (case-insensitive)
	/// </summary>
	/// <param name="policyName">Policy name</param>
	/// <param name="excludeId">ID to exclude from check (for update scenarios)</param>
	/// <returns>True if exists, false otherwise</returns>
	Task<bool> IsPolicyNameExistsAsync(string policyName, Guid? excludeId = null);
	
	/// <summary>
	/// Get leave policy by name (case-insensitive)
	/// </summary>
	Task<LeavePolicy?> FindByPolicyNameAsync(string policyName);
}
