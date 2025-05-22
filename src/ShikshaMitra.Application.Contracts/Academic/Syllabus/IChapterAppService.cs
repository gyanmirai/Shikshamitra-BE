using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Syllabus;

public interface IChapterAppService : IApplicationService
{
	Task<ChapterDto> CreateAsync(CreateUpdateChapterDto input);
	Task<List<ChapterDto>> GetByStandardSubjectIdAsync(Guid standardSubjectId);
}
