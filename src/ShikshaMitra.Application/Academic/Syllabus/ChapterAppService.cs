using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace ShikshaMitra.Academic.Syllabus;

[RemoteService(IsEnabled = false)]
public class ChapterAppService(
	IRepository<SubjectChapter, Guid> chapterRepository,
	IGuidGenerator guidGenerator
) : ApplicationService, IChapterAppService
{
	public async Task<ChapterDto> CreateAsync(CreateUpdateChapterDto createChapterDto)
	{
		try
		{
			// Validate EducationMediumId is provided`r`n`t`t`tif (createChapterDto.EducationMediumId == Guid.Empty)`r`n`t`t`t{`r`n`t`t`t`tthrow new UserFriendlyException("Education Medium ID is required and cannot be empty.");`r`n`t`t`t}
			var educationMediumId = createChapterDto.EducationMediumId;
			
			var chapter = new SubjectChapter(
				id: guidGenerator.Create(),
				standardId: createChapterDto.StandardId,
				subjectId: createChapterDto.SubjectId,
				educationMediumId: educationMediumId,
				chapterNumber: createChapterDto.ChapterNumber,
				chapterName: createChapterDto.ChapterName,
				chapterStartTime: createChapterDto.ChapterStartTime,
				chapterEndTime: createChapterDto.ChapterEndTime,
				description: createChapterDto.Description
			);

			await chapterRepository.InsertAsync(chapter, autoSave: true);
			return ObjectMapper.Map<SubjectChapter, ChapterDto>(chapter);
		}
		catch (Exception ex)
		{
			throw new UserFriendlyException("Error creating chapter", ex.Message);
		}
	}

	public async Task<List<ChapterDto>> GetByStandardSubjectIdAsync(Guid standardId, Guid subjectId)
	{
		try
		{
			var chapters = await chapterRepository.GetListAsync(x =>
				x.StandardId == standardId && x.SubjectId == subjectId
			);

			return ObjectMapper.Map<List<SubjectChapter>, List<ChapterDto>>(chapters);
		}
		catch (Exception ex)
		{
			throw new UserFriendlyException("Error retrieving chapters", ex.Message);
		}
	}

	public async Task<ChapterDto> UpdateAsync(Guid id, CreateUpdateChapterDto updateChapterDto)
	{
		try
		{
			var chapter =
				await chapterRepository.FindAsync(id)
				?? throw new UserFriendlyException("Chapter not found");

			chapter.Update(
				updateChapterDto.ChapterName,
				updateChapterDto.ChapterStartTime,
				updateChapterDto.ChapterEndTime,
				updateChapterDto.Description
			);

			chapter.ChapterNumber = updateChapterDto.ChapterNumber;

			await chapterRepository.UpdateAsync(chapter, autoSave: true);
			return ObjectMapper.Map<SubjectChapter, ChapterDto>(chapter);
		}
		catch (UserFriendlyException ex)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw new UserFriendlyException("Error updating chapter", ex.Message);
		}
	}

	public Task DeleteAsync(Guid id)
	{
		try
		{
			return chapterRepository.DeleteAsync(id, autoSave: true);
		}
		catch (Exception ex)
		{
			throw new UserFriendlyException("Error deleting chapter", ex.Message);
		}
	}
}
