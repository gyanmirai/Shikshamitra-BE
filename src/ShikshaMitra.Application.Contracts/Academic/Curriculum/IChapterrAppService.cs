using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Curriculum;
public interface IChapterrAppService : IApplicationService
{
    Task<ChapterrDto> CreateChapterAsync(CreateChapterDto createChapterDto);
    Task<List<ChapterrDto>> GetListChaptersAsync(Guid standardId, Guid subjectId);
	Task<ChapterrDto> GetByIdChapterAsync(Guid id);
	Task<ChapterrDto> UpdateByIdChapterAsync(Guid id, UpdateChapterDto updateChapterDto);
	Task DeleteByIdChapterAsync(Guid id);
	Task DeleteByIdChapterDocumentAsync(Guid id);
}
