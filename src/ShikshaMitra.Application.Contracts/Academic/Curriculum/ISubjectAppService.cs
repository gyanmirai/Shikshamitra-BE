using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Curriculum;

public interface ISubjectAppService
{
	Task<SubjectDto> CreateAsync(CreateSubjectDto subjectDto);

	Task<PagedResultDto<SubjectDto>> GetAllAsync(Guid classId, int skipCount, int maxResultCount);
}
