using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Attendances;

/// <summary>
/// Application service interface for Student Leave operations
/// </summary>
public interface IStudentLeaveAppService : IApplicationService
{
	/// <summary>
	/// Gets a paginated list of student leave requests with filters
	/// </summary>
	Task<PagedResultDto<StudentLeaveDto>> GetListAsync(GetStudentLeaveListInput input);

	/// <summary>
	/// Gets a single student leave request by ID
	/// </summary>
	Task<StudentLeaveDto> GetAsync(Guid id);

	/// <summary>
	/// Creates a new student leave request
	/// </summary>
	Task<StudentLeaveDto> CreateAsync(CreateStudentLeaveDto input);

	/// <summary>
	/// Updates an existing student leave request (only if status is Pending)
	/// </summary>
	Task<StudentLeaveDto> UpdateAsync(Guid id, UpdateStudentLeaveDto input);

	/// <summary>
	/// Deletes a student leave request
	/// </summary>
	Task DeleteAsync(Guid id);

	/// <summary>
	/// Approves a student leave request
	/// </summary>
	Task<StudentLeaveDto> ApproveAsync(Guid id, ApproveRejectStudentLeaveDto input);

	/// <summary>
	/// Rejects a student leave request
	/// </summary>
	Task<StudentLeaveDto> RejectAsync(Guid id, ApproveRejectStudentLeaveDto input);

	/// <summary>
	/// Gets the attachment file for a student leave request
	/// </summary>
	Task<StudentLeaveAttachmentDto> GetAttachmentAsync(Guid id);
}
