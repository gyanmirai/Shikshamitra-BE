using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.ClassManagement.ClassTeacherAssignment;

public interface IClassTeacherAssignmentAppService
{
	Task<List<ClassTeacherRowDto>> GetClassTeacherAssignmentsAsync(Guid organizationId);

	Task<ApiSuccessResponseDto> SaveClassTeacherAssignmentsAsync(
		SaveClassTeacherAssignmentsDto input
	);
}
