using System;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.ClassManagement.StudentElectiveAssignment;

public interface IStudentElectiveAssignmentAppService
{
	Task<PaginatedStudentElectiveResponseDto> GetStudentElectiveAssignmentsAsync(
		Guid academicMediumId,
		Guid classId,
		Guid divisionId,
		Guid electiveGroupId,
		string? search = null
	);

	Task<ApiSuccessResponseDto> SaveStudentElectiveAssignmentsAsync(
		SaveStudentElectiveAssignmentsDto input
	);

	Task<ApiSuccessResponseDto> LockStudentElectiveAssignmentsAsync(
		LockStudentElectiveAssignmentsDto input
	);
}
