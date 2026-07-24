using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.ClassManagement.ChapterProgress;

public interface IChapterProgressAppService
{
	Task<List<ClassDivisionSubjectDto>> GetSubjectsForClassDivisionAsync(
		Guid academicMediumId,
		Guid classId,
		Guid divisionId
	);

	Task<PaginatedChapterProgressResponseDto> GetChapterProgressListAsync(
		Guid academicMediumId,
		Guid classId,
		Guid divisionId,
		Guid subjectId,
		string? search = null
	);

	Task<ApiSuccessResponseDto> UpdateChapterProgressAsync(
		Guid chapterId,
		UpdateChapterProgressDto input
	);

	Task<List<SubjectForChaptersDto>> GetSubjectsForStandardAsync(Guid standardId);

	Task<List<ChapterProgressLogDto>> GetChapterProgressLogsAsync(Guid chapterId, Guid divisionId);

	Task<List<EmployeeClassDto>> GetClassesByEmployeeAsync(Guid employeeId);

	Task<List<EmployeeDivisionDto>> GetDivisionsByEmployeeAndClassAsync(Guid employeeId, Guid classId);

	Task<List<ClassDivisionSubjectDto>> GetSubjectsProgressByEmployeeAsync(Guid employeeId, Guid classId, Guid divisionId);
}
