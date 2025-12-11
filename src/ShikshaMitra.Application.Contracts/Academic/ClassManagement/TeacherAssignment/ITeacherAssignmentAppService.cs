using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.ClassManagement.TeacherAssignment;

public interface ITeacherAssignmentAppService
{
	Task<TeacherAssignmentGridResponseDto> GetTeacherAssignmentsForClassAsync(
		Guid academicMediumId,
		Guid classId
	);

	Task<ApiSuccessResponseDto> SaveTeacherAssignmentsForClassAsync(
		SaveTeacherAssignmentsDto input
	);

	Task<List<TeacherInfoDto>> GetTeachersAsync(string? search = null);
}
