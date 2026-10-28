using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.ClassManagement.ClassTeacherAssignment;

public interface IClassTeacherAssignmentAppService
{
	Task<ClassTeacherWorkspaceDto> GetWorkspaceAsync(Guid organizationId);

	Task<List<ClassTeacherRowDto>> GetClassTeacherAssignmentsAsync(Guid organizationId);

	Task<ClassTeacherSummaryDto> GetClassTeacherAssignmentSummaryAsync(Guid organizationId);

	Task<List<ClassTeacherEligibleTeacherDto>> GetEligibleTeachersAsync(
		Guid organizationId,
		Guid? classId = null,
		Guid? divisionId = null,
		string? search = null
	);

	Task<ClassTeacherValidationResultDto> ValidateClassTeacherAssignmentsAsync(
		SaveClassTeacherAssignmentsDto input
	);

	Task<List<ClassTeacherAssignmentHistoryDto>> GetClassTeacherAssignmentHistoryAsync(
		Guid organizationId
	);

	Task<ApiSuccessResponseDto> SaveClassTeacherAssignmentsAsync(
		SaveClassTeacherAssignmentsDto input
	);
}
