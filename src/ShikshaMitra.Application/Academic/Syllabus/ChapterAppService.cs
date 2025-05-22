using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace ShikshaMitra.Academic.Syllabus;

[RemoteService(IsEnabled = false)]
public class ChapterAppService(IRepository<SubjectChapter, Guid> chapterRepository, IGuidGenerator guidGenerator) : ApplicationService, IChapterAppService
{
	public async Task<ChapterDto> CreateAsync(CreateUpdateChapterDto input)
	{
		var chapter = new SubjectChapter(
			guidGenerator.Create(),
			input.StandardSubjectId,
			input.ChapterNumber,
			input.ChapterName,
			input.ChapterStartTime,
			input.ChapterEndTime,
			input.Description
		);

		await chapterRepository.InsertAsync(chapter, autoSave: true);
		return ObjectMapper.Map<SubjectChapter, ChapterDto>(chapter);
	}

	public async Task<List<ChapterDto>> GetByStandardSubjectIdAsync(Guid standardSubjectId)
	{
		var chapters = (await chapterRepository.GetListAsync(x => x.StandardSubjectId == standardSubjectId));

		return ObjectMapper.Map<List<SubjectChapter>, List<ChapterDto>>(chapters);
	}

	public async Task<ChapterDto> UpdateAsync(Guid id, CreateUpdateChapterDto input)
	{
		var chapter = await chapterRepository.GetAsync(id);

		chapter.Update(
			input.ChapterName,
			input.ChapterStartTime,
			input.ChapterEndTime,
			input.Description
		);

		chapter.ChapterNumber = input.ChapterNumber;

		await chapterRepository.UpdateAsync(chapter, autoSave: true);
		return ObjectMapper.Map<SubjectChapter, ChapterDto>(chapter);
	}

}
