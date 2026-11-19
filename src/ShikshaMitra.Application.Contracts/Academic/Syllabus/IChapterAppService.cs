using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Common;

namespace ShikshaMitra.Academic.Syllabus;

public interface IChapterAppService
{
	Task<List<ChapterLookupDto>> GetLookupAsync(Guid standardId, Guid subjectId, Guid? divisionId = null);
	Task<ApiSuccessResponseDto> CreateAsync(Guid standardId, Guid subjectId, List<CreateChapterDto> input);
	Task<List<ChapterDto>> GetByStandardAndSubjectAsync(Guid standardId, Guid subjectId, Guid? divisionId = null);
	Task<ChapterDto> UpdateAsync(Guid id, CreateUpdateChapterDto input);
	Task DeleteAsync(Guid id);
}
