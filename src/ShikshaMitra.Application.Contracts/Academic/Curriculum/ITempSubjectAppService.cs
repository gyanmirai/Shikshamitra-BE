using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Curriculum;

public interface ITempSubjectAppService
{
	Task<TempSubjectDto> CreateAsync(CreateTempSubjectDto createSubjectDto);

	Task<PagedResultDto<TempSubjectDto>> GetSubjectsByClassAsync(Guid classId, int skipCount = 0, int maxResultCount = 10);

	Task DeleteAsync(Guid id);

	Task ChangeSubjectStatusAsync(Guid subjectId, SubjectStatus status);
}
