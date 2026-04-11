using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Attendance;

/// <summary>
/// Repository interface for StudentLeave entity
/// </summary>
public interface IStudentLeaveRepository : IRepository<StudentLeave, Guid>
{
	/// <summary>
	/// Gets a filtered, sorted, and paginated list of student leaves
	/// </summary>
	Task<List<StudentLeave>> GetListAsync(
		string? filterText = null,
		Guid? studentId = null,
		Guid? educationMediumId = null,
		Guid? standardId = null,
		Guid? divisionId = null,
		Guid? studentLeaveTypeId = null,
		StudentLeaveStatusEnum? status = null,
		DateTimeOffset? startDate = null,
		DateTimeOffset? endDate = null,
		string? sorting = null,
		int maxResultCount = int.MaxValue,
		int skipCount = 0,
		Guid? tenantId = null,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets the count of student leaves based on filters
	/// </summary>
	Task<long> GetCountAsync(
		string? filterText = null,
		Guid? studentId = null,
		Guid? educationMediumId = null,
		Guid? standardId = null,
		Guid? divisionId = null,
		Guid? studentLeaveTypeId = null,
		StudentLeaveStatusEnum? status = null,
		DateTimeOffset? startDate = null,
		DateTimeOffset? endDate = null,
		Guid? tenantId = null,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets a single student leave with navigation properties included
	/// </summary>
	Task<StudentLeave?> GetWithDetailsAsync(
		Guid id,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets a list of student leaves with navigation properties included
	/// </summary>
	Task<List<StudentLeave>> GetListWithDetailsAsync(
		string? filterText = null,
		Guid? studentId = null,
		Guid? educationMediumId = null,
		Guid? standardId = null,
		Guid? divisionId = null,
		Guid? studentLeaveTypeId = null,
		StudentLeaveStatusEnum? status = null,
		DateTimeOffset? startDate = null,
		DateTimeOffset? endDate = null,
		string? sorting = null,
		int maxResultCount = int.MaxValue,
		int skipCount = 0,
		Guid? tenantId = null,
		CancellationToken cancellationToken = default);
}
