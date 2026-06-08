using ShikshaMitra.Academic.Syllabus;
using ShikshaMitra.Common;
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
	public async Task<ApiSuccessResponseDto> CreateAsync(Guid standardId, Guid subjectId, CreateChapterDto createChapterDto)
	{
		var assignedSubject = (await assignedSubjectRepository.GetListAsync(
				a => a.StandardId == standardId && a.SubjectId == subjectId
			)).FirstOrDefault()
			?? throw new BusinessException("Academic:Chapter:001", "Subject not assigned to this class");

		assignedSubject.AddChapter(
			guidGenerator.Create(),
			createChapterDto.ChapterNo,
			createChapterDto.ChapterName,
			createChapterDto.StartDate,
			createChapterDto.EndDate,
			createChapterDto.Documents ?? [],
			createChapterDto.Description
		);

		await assignedSubjectRepository.UpdateAsync(assignedSubject, autoSave: true);

		return new ApiSuccessResponseDto { Message = "Chapter created successfully." };
	}

	public async Task<List<ChapterDto>> GetByStandardAndSubjectAsync(Guid standardId, Guid subjectId)
	{
		var assignedSubject = (await assignedSubjectRepository.GetListAsync(
				a => a.StandardId == standardId && a.SubjectId == subjectId,
				includeDetails: true
			)).FirstOrDefault()
			?? throw new BusinessException("Academic:Chapter:002", "Subject not assigned to this class");

		var chapters = assignedSubject.Chapters.OrderBy(c => c.ChapterNumber).ToList();
		var dtos = ObjectMapper.Map<List<AssignedSubjectChapter>, List<ChapterDto>>(chapters);
		foreach (var dto in dtos)
		{
			dto.StandardId = assignedSubject.StandardId;
			dto.SubjectId = assignedSubject.SubjectId;
		}
		return dtos;
	}

	public async Task<ChapterDto> UpdateAsync(Guid id, CreateUpdateChapterDto updateChapterDto)
	{
		var assignedSubject =
			await assignedSubjectRepository.FindAsync(
				chapter => chapter.Chapters.Any(c => c.Id == id),
				includeDetails: true
			)
			?? throw new BusinessException("Academic:Chapter:003", "Chapter not found");

		var chapter = assignedSubject.Chapters.First(c => c.Id == id);
		chapter.UpdateDetails(
			updateChapterDto.ChapterNo,
			updateChapterDto.ChapterName,
			updateChapterDto.Description
		);
		chapter.UpdateDates(
			updateChapterDto.StartDate,
			updateChapterDto.EndDate
		);
		await assignedSubjectRepository.UpdateAsync(assignedSubject, autoSave: true);

		var dto = ObjectMapper.Map<AssignedSubjectChapter, ChapterDto>(chapter);
		dto.StandardId = assignedSubject.StandardId;
		dto.SubjectId = assignedSubject.SubjectId;
		return dto;
	}

	public async Task DeleteAsync(Guid id)
	{
		var assignedSubject =
			await assignedSubjectRepository.FindAsync(
				chapter => chapter.Chapters.Any(c => c.Id == id),
				includeDetails: true
			)
			?? throw new BusinessException("Academic:Chapter:003", "Chapter not found");

		assignedSubject.RemoveChapter(id);
		await assignedSubjectRepository.UpdateAsync(assignedSubject, autoSave: true);
	}
}
