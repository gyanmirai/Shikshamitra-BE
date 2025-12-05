using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments;

/// <summary>
/// Application service interface for managing entrance exam operations
/// </summary>
public interface IStudentEnrollmentEntranceExamAppService : IApplicationService
{
	/// <summary>
	/// Gets a paginated list of students in entrance exam stage
	/// </summary>
	Task<PagedResultDto<EntranceExamStudentDto>> GetListAsync(GetEntranceExamStudentsInput input);

	/// <summary>
	/// Gets detailed entrance exam information for a specific student
	/// </summary>
	Task<EntranceExamDetailDto> GetAsync(Guid studentId);

	/// <summary>
	/// Schedules a student for an entrance exam slot
	/// </summary>
	Task ScheduleExamAsync(Guid studentId, ScheduleEntranceExamDto input);

	/// <summary>
	/// Uploads entrance exam result for a student
	/// </summary>
	Task<EntranceExamResultResponseDto> UploadResultAsync(Guid studentId, UploadEntranceExamResultDto input);

	/// <summary>
	/// Moves a student who passed the entrance exam to admission stage
	/// </summary>
	Task MoveToAdmissionAsync(Guid studentId);

	/// <summary>
	/// Moves multiple students to admission stage in bulk
	/// </summary>
	Task<BulkMoveResultDto> BulkMoveToAdmissionAsync(BulkMoveToAdmissionDto input);
}
