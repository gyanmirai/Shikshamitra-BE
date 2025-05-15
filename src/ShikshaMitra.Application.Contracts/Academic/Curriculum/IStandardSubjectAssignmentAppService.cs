using ShikshaMitra.Academic.Curriculum.SubjectAssignment;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.Curriculum;

public interface IStandardSubjectAssignmentAppService
{
	Task<StandardSubjectAssignmentResponseDto> CreateAssignmentAsync(CreateStandardSubjectAssignmentDto createAssignmentDto);
	Task UpdateTeachersAsync(Guid assignmentId, Guid primaryId, List<Guid> secondaryIds);
	Task ChangeStatusAsync(Guid assignmentId, SubjectStatus status);
	Task<List<StandardSubjectAssignmentResponseDto>> GetListAsync(Guid standardId);
}
