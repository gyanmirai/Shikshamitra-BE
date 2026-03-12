using ShikshaMitra.Academic.Syllabus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace ShikshaMitra.Academic.Curriculum;

[RemoteService(IsEnabled = false)]
public class ChapterAppService(
	IRepository<AssignedSubject, Guid> assignedSubjectRepository,
	IGuidGenerator guidGenerator
) : ApplicationService, IChapterAppService
{
	public async Task<ChapterDto> CreateAsync(CreateUpdateChapterDto createChapterDto)
	{
		try
		{
			var assignedSubject =
				await assignedSubjectRepository.FindAsync(createChapterDto.AssignedSubjectId)
				?? throw new UserFriendlyException("Assigned subject not found");

			assignedSubject.AddChapter(
				guidGenerator.Create(),
				createChapterDto.ChapterNumber,
				createChapterDto.ChapterName,
				createChapterDto.ChapterStartTime,
				createChapterDto.ChapterEndTime,
				createChapterDto.Documents,
				createChapterDto.Description
			);

			await assignedSubjectRepository.UpdateAsync(assignedSubject, autoSave: true);

			return ObjectMapper.Map<
				AssignedSubjectChapter,
				ChapterDto>(assignedSubject.Chapters.Last());
		}
		catch (Exception ex)
		{
			throw new UserFriendlyException("Error creating chapter", ex.Message);
		}
	}

	public async Task<List<ChapterDto>> GetByAssignedSubjectIdAsync(Guid assignedSubjectId)
	{
		try
		{
			var assignedSubject =
				await assignedSubjectRepository.FindAsync(assignedSubjectId)
				?? throw new UserFriendlyException("Assigned subject not found");

			var chapters = assignedSubject.Chapters.ToList();
			return ObjectMapper.Map<List<AssignedSubjectChapter>, List<ChapterDto>>(chapters);
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
			var assignedSubject =
				await assignedSubjectRepository.FindAsync(
					chapter => chapter.Chapters.Any(c => c.Id == id),
					includeDetails: true
				)
				?? throw new UserFriendlyException("Chapter not found");
			var chapter = assignedSubject.Chapters.First(c => c.Id == id);
			chapter.UpdateDetails(
				updateChapterDto.ChapterNumber,
				updateChapterDto.ChapterName,
				updateChapterDto.Description
			);
			await assignedSubjectRepository.UpdateAsync(assignedSubject, autoSave: true);

			return ObjectMapper.Map<AssignedSubjectChapter, ChapterDto>(chapter);
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

	public async Task DeleteAsync(Guid id)
	{
		try
		{
			var assignedSubject =
				await assignedSubjectRepository.FindAsync(
					chapter => chapter.Chapters.Any(c => c.Id == id),
					includeDetails: true
				)
				?? throw new UserFriendlyException("Chapter not found");

			assignedSubject.RemoveChapter(id);
			await assignedSubjectRepository.UpdateAsync(assignedSubject, autoSave: true);
		}
		catch (Exception ex)
		{
			throw new UserFriendlyException("Error deleting chapter", ex.Message);
		}
	}
}
