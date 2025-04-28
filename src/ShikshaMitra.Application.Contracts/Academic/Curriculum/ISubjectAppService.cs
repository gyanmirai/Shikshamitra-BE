using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Curriculum;

public interface ISubjectAppService
{
	Task<SubjectDto> CreateAsync(CreateSubjectDto createSubjectDto);

	Task<PagedResultDto<SubjectDto>> GetAllAsync(Guid classId, int skipCount, int maxResultCount);
}
