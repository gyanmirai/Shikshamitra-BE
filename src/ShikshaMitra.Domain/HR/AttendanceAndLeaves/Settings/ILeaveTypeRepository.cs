using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

/// <summary>
/// Repository interface for LeaveType entity
/// </summary>
public interface ILeaveTypeRepository : IRepository<LeaveType, Guid>
{
	/// <summary>
	/// Check if a leave type with the given name already exists (case-insensitive)
	/// </summary>
	/// <param name="type">Leave type name</param>
	/// <param name="excludeId">ID to exclude from check (for update scenarios)</param>
	/// <returns>True if exists, false otherwise</returns>
	Task<bool> IsTypeExistsAsync(string type, Guid? excludeId = null);
	
	/// <summary>
	/// Get leave type by name (case-insensitive)
	/// </summary>
	Task<LeaveType?> FindByTypeAsync(string type);
}
