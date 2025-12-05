using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Attendance.Settings;

/// <summary>
/// Repository interface for StudentLeaveType entity
/// </summary>
public interface IStudentLeaveTypeRepository : IRepository<StudentLeaveType, Guid>
{
	/// <summary>
	/// Finds a student leave type by its name
	/// </summary>
	Task<StudentLeaveType?> FindByNameAsync(
		string leaveTypeName,
		Guid? tenantId = null,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Checks if a leave type name already exists
	/// </summary>
	Task<bool> IsNameExistsAsync(
		string leaveTypeName,
		Guid? excludeId = null,
		Guid? tenantId = null,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets all active student leave types for a tenant
	/// </summary>
	Task<List<StudentLeaveType>> GetActiveLeaveTypesAsync(
		Guid? tenantId = null,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets paginated list of student leave types with optional filtering
	/// </summary>
	Task<List<StudentLeaveType>> GetListAsync(
		string? filterText = null,
		bool? isActive = null,
		string? sorting = null,
		int maxResultCount = int.MaxValue,
		int skipCount = 0,
		Guid? tenantId = null,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets the count of student leave types with optional filtering
	/// </summary>
	Task<long> GetCountAsync(
		string? filterText = null,
		bool? isActive = null,
		Guid? tenantId = null,
		CancellationToken cancellationToken = default);
}
