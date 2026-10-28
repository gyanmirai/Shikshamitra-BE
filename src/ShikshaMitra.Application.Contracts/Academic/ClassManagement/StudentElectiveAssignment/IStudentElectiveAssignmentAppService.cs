using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.ClassManagement.StudentElectiveAssignment;

public interface IStudentElectiveAssignmentAppService
{
	Task<List<ElectiveSelectionCountDto>> GetSelectionCountsAsync(ElectiveSelectionCountsRequestDto input);

	Task<PaginatedStudentElectiveResponseDto> GetStudentElectiveAssignmentsAsync(
		Guid academicMediumId,
		Guid classId,
		Guid divisionId,
		Guid electiveGroupId,
		string? search = null,
		bool includeSelectableSubjects = true
	);

	Task<ApiSuccessResponseDto> SaveStudentElectiveAssignmentsAsync(
		SaveStudentElectiveAssignmentsDto input
	);

	Task<ApiSuccessResponseDto> LockStudentElectiveAssignmentsAsync(
		LockStudentElectiveAssignmentsDto input
	);
}
