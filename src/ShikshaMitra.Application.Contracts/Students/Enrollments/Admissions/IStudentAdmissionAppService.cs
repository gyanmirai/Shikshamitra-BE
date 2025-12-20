using ShikshaMitra.Students.Admissions;
using ShikshaMitra.Students.StudentEnrollments;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Enrollments.Admissions;

/// <summary>
/// Application service interface for Student Admission operations
/// </summary>
public interface IStudentAdmissionAppService : IApplicationService
{
	/// <summary>
	/// Gets a paginated list of students in admission stage with filters
	/// </summary>
	Task<PagedResultDto<StudentEnrollmentListDto>> GetListAsync(GetStudentAdmissionsInput input);
	
	/// <summary>
	/// Assigns or changes division for a student
	/// </summary>
	Task AssignDivisionAsync(Guid studentId, AssignDivisionDto input);
	
	/// <summary>
	/// Changes admission status (approve/reject) for multiple students
	/// </summary>
	Task<BatchAdmissionStatusChangeResultDto> ChangeStatusBatchAsync(BatchAdmissionStatusChangeRequestDto input);
}
