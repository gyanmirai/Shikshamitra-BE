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
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Academic.QuestionBank;
using ShikshaMitra.Academic.QuestionPapers;

namespace ShikshaMitra.Academic.Curriculum;

[RemoteService(IsEnabled = false)]
public class ChapterAppService(
	IRepository<AssignedSubject, Guid> assignedSubjectRepository,
	IGuidGenerator guidGenerator,
	IRepository<Syllabus.ChapterProgress, Guid> progressRepository,
	IRepository<HomeworkAssignmentEntity, Guid> homeworkRepository,
	IRepository<AssignmentEntity, Guid> assignmentRepository,
	IRepository<LessonPlan, Guid> lessonPlanRepository,
	IRepository<QuestionBankItem, Guid> questionBankRepository,
	IRepository<QuestionPaperBlueprintChapterRule, Guid> blueprintChapterRuleRepository
) : ApplicationService, IChapterAppService
{
	public async Task<ApiSuccessResponseDto> CreateAsync(Guid standardId, Guid subjectId, List<CreateChapterDto> input)
	{
		var assignedSubject = (await assignedSubjectRepository.GetListAsync(
				a => a.StandardId == standardId && a.SubjectId == subjectId,
				includeDetails: true
			)).FirstOrDefault()
			?? throw new BusinessException("Academic:Chapter:001", "Subject not assigned to this class");

		if (input.Select(x => x.ChapterNo).Distinct().Count() != input.Count)
		{
			throw new BusinessException("Academic:Chapter:005", "Duplicate chapter numbers are not allowed.");
		}

		foreach (var createChapterDto in input)
		{
			if (assignedSubject.Chapters.Any(c => c.ChapterNumber == createChapterDto.ChapterNo))
			{
				throw new BusinessException("Academic:Chapter:004", $"Chapter number {createChapterDto.ChapterNo} already exists.");
			}

			var chapter = assignedSubject.AddChapter(
				guidGenerator.Create(),
				createChapterDto.ChapterNo,
				createChapterDto.ChapterName,
				createChapterDto.StartDate,
				createChapterDto.EndDate,
				createChapterDto.Documents ?? [],
				createChapterDto.Description
			);
			ApplyPlanningDetails(chapter, createChapterDto);
		}

		assignedSubject.RecalculateSetupHealth();
		await assignedSubjectRepository.UpdateAsync(assignedSubject, autoSave: true);

		return new ApiSuccessResponseDto { Message = "Chapters created successfully." };
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
		ApplyPlanningDetails(chapter, updateChapterDto);

		// Sync documents
		var incomingDocs = updateChapterDto.Documents ?? new List<string>();

		// Remove documents not in incoming list
		var removedDocs = chapter.Documents
			.Where(d => !incomingDocs.Contains(d.BlobName))
			.ToList();
		foreach (var doc in removedDocs)
		{
			chapter.RemoveDocument(doc);
			assignedSubject.RemoveDocument(doc.Id, doc.BlobName);
		}

		// Add new documents
		var existingBlobNames = chapter.Documents.Select(d => d.BlobName).ToHashSet();
		foreach (var blobName in incomingDocs)
		{
			if (!existingBlobNames.Contains(blobName))
			{
				var newDoc = new AssignedSubjectDocument(
					guidGenerator.Create(),
					assignedSubject.Id,
					chapter.Id,
					blobName
				);
				chapter.AddDocument(newDoc);
				assignedSubject.Documents.Add(newDoc);
			}
		}

		assignedSubject.RecalculateSetupHealth();
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

		var chapter = assignedSubject.Chapters.First(c => c.Id == id);

		// Block deletion if the chapter is in use
		var usageParts = new System.Collections.Generic.List<string>();

		var progressCount = await progressRepository.CountAsync(p => p.ChapterId == id);
		if (progressCount > 0)
			usageParts.Add($"{progressCount} chapter progress record(s)");

		var homeworkCount = await homeworkRepository.CountAsync(h => h.ChapterId == id || (h.ChapterIdsJson != null && h.ChapterIdsJson.Contains(id.ToString())));
		if (homeworkCount > 0)
			usageParts.Add($"{homeworkCount} homework(s)");

		var assignmentCount = await assignmentRepository.CountAsync(a => a.ChapterIdsJson != null && a.ChapterIdsJson.Contains(id.ToString()));
		if (assignmentCount > 0)
			usageParts.Add($"{assignmentCount} assignment(s)");

		var lessonPlanCount = await lessonPlanRepository.CountAsync(l => l.ChapterId == id);
		if (lessonPlanCount > 0)
			usageParts.Add($"{lessonPlanCount} lesson plan(s)");

		var questionCount = await questionBankRepository.CountAsync(q => q.ChapterId == id);
		if (questionCount > 0)
			usageParts.Add($"{questionCount} question bank item(s)");

		var questionPaperBlueprintCount = await blueprintChapterRuleRepository.CountAsync(r => r.ChapterId == id);
		if (questionPaperBlueprintCount > 0)
			usageParts.Add($"{questionPaperBlueprintCount} question paper blueprint rule(s)");

		if (usageParts.Count > 0)
		{
			throw new BusinessException(CurriculumErrorCodes.ChapterIsInUse)
				.WithData("message", $"Cannot delete '{chapter.ChapterName}' because it is in use: {string.Join(", ", usageParts)}. Please remove these usages first.");
		}

		assignedSubject.RemoveChapter(id);
		assignedSubject.RecalculateSetupHealth();
		await assignedSubjectRepository.UpdateAsync(assignedSubject, autoSave: true);
	}

	private static void ApplyPlanningDetails(AssignedSubjectChapter chapter, CreateChapterDto dto)
	{
		chapter.UpdatePlanningDetails(
			dto.UnitName,
			dto.TermName,
			dto.Topic,
			dto.EstimatedTeachingDays,
			dto.EstimatedPeriods,
			dto.RevisionDays,
			dto.BufferDays,
			dto.DifficultyLevel,
			dto.BloomLevel,
			dto.Tags,
			dto.LearningOutcomes,
			dto.SkillsCovered,
			dto.Prerequisites,
			dto.TeachingMethods,
			dto.ClassActivities,
			dto.CompletionCriteria,
			dto.TeacherNotes,
			dto.StudentSummary,
			dto.ParentSummary,
			dto.ReferenceLinks,
			dto.Worksheets,
			dto.AudioVideoMaterials,
			dto.BoardDocuments,
			dto.AssessmentMethod,
			dto.QuestionTypes,
			dto.MarksWeightage,
			dto.BloomCoverage,
			dto.DifficultyDistribution,
			dto.Visibility,
			dto.PublishStatus,
			dto.ReviewStatus,
			dto.NotifyOnPublish,
			dto.ProgressPercentage,
			dto.CoverageStatus,
			dto.LessonPlanStatus,
			dto.HomeworkStatus,
			dto.AssessmentStatus,
			dto.ResourceStatus,
			dto.QuestionCount,
			dto.HealthScore,
			dto.IsAiGenerated,
			dto.CopiedFromPreviousYear,
			dto.TemplateSource,
			dto.DependencyChapterIds,
			dto.VersionLabel
		);
	}

	private static void ApplyPlanningDetails(AssignedSubjectChapter chapter, CreateUpdateChapterDto dto)
	{
		chapter.UpdatePlanningDetails(
			dto.UnitName,
			dto.TermName,
			dto.Topic,
			dto.EstimatedTeachingDays,
			dto.EstimatedPeriods,
			dto.RevisionDays,
			dto.BufferDays,
			dto.DifficultyLevel,
			dto.BloomLevel,
			dto.Tags,
			dto.LearningOutcomes,
			dto.SkillsCovered,
			dto.Prerequisites,
			dto.TeachingMethods,
			dto.ClassActivities,
			dto.CompletionCriteria,
			dto.TeacherNotes,
			dto.StudentSummary,
			dto.ParentSummary,
			dto.ReferenceLinks,
			dto.Worksheets,
			dto.AudioVideoMaterials,
			dto.BoardDocuments,
			dto.AssessmentMethod,
			dto.QuestionTypes,
			dto.MarksWeightage,
			dto.BloomCoverage,
			dto.DifficultyDistribution,
			dto.Visibility,
			dto.PublishStatus,
			dto.ReviewStatus,
			dto.NotifyOnPublish,
			dto.ProgressPercentage,
			dto.CoverageStatus,
			dto.LessonPlanStatus,
			dto.HomeworkStatus,
			dto.AssessmentStatus,
			dto.ResourceStatus,
			dto.QuestionCount,
			dto.HealthScore,
			dto.IsAiGenerated,
			dto.CopiedFromPreviousYear,
			dto.TemplateSource,
			dto.DependencyChapterIds,
			dto.VersionLabel
		);
	}
}
