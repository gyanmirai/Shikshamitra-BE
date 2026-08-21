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
using ShikshaMitra.TenantSettings.Divisions;

namespace ShikshaMitra.Academic.Curriculum;

[RemoteService(IsEnabled = false)]
public class ChapterAppService(
	IRepository<AssignedSubject, Guid> assignedSubjectRepository,
	IGuidGenerator guidGenerator,
	IRepository<Syllabus.ChapterProgress, Guid> progressRepository,
	IRepository<HomeworkAssignmentEntity, Guid> homeworkRepository,
	IRepository<AssignmentEntity, Guid> assignmentRepository,
	IRepository<AssessmentRubric, Guid> rubricRepository,
	IRepository<LessonPlan, Guid> lessonPlanRepository,
	IRepository<TeacherDiary, Guid> teacherDiaryRepository,
	IRepository<QuestionBankItem, Guid> questionBankRepository,
	IRepository<QuestionPaperBlueprintChapterRule, Guid> blueprintChapterRuleRepository,
	IRepository<Division, Guid> divisionRepository
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

	public async Task<List<ChapterDto>> GetByStandardAndSubjectAsync(Guid standardId, Guid subjectId, Guid? divisionId = null)
	{
		var assignedSubject = (await assignedSubjectRepository.GetListAsync(
				a => a.StandardId == standardId && a.SubjectId == subjectId,
				includeDetails: true
			)).FirstOrDefault()
			?? throw new BusinessException("Academic:Chapter:002", "Subject not assigned to this class");

		if (divisionId.HasValue)
		{
			var divisionExists = await divisionRepository.AnyAsync(
				division => division.Id == divisionId.Value && division.StandardId == standardId);
			if (!divisionExists)
			{
				throw new BusinessException("Academic:Chapter:006", "Division does not belong to the selected class.");
			}
		}

		var chapters = assignedSubject.Chapters.OrderBy(c => c.ChapterNumber).ToList();
		var dtos = ObjectMapper.Map<List<AssignedSubjectChapter>, List<ChapterDto>>(chapters);
		ApplyResourceStatusFallback(dtos);
		foreach (var dto in dtos)
		{
			dto.StandardId = assignedSubject.StandardId;
			dto.SubjectId = assignedSubject.SubjectId;
		}

		await EnrichLinkedPlanningStatusesAsync(assignedSubject, dtos, divisionId);

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
		ApplyResourceStatusFallback(new List<ChapterDto> { dto });
		dto.StandardId = assignedSubject.StandardId;
		dto.SubjectId = assignedSubject.SubjectId;
		await EnrichLinkedPlanningStatusesAsync(assignedSubject, new List<ChapterDto> { dto }, divisionId: null);
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

	private async Task EnrichLinkedPlanningStatusesAsync(
		AssignedSubject assignedSubject,
		List<ChapterDto> dtos,
		Guid? divisionId)
	{
		if (dtos.Count == 0)
		{
			return;
		}

		var chapterIds = dtos.Select(dto => dto.Id).ToList();
		var linkedByChapter = chapterIds.ToDictionary(id => id, _ => new ChapterLinkSummary());

		var lessonPlans = await lessonPlanRepository.GetListAsync(lp =>
			lp.StandardId == assignedSubject.StandardId &&
			lp.SubjectId == assignedSubject.SubjectId &&
			chapterIds.Contains(lp.ChapterId) &&
			(!divisionId.HasValue || lp.DivisionId == divisionId.Value) &&
			lp.Status != LessonPlanStatus.Rejected);
		foreach (var lessonPlan in lessonPlans)
		{
			var summary = linkedByChapter[lessonPlan.ChapterId];
			summary.LessonPlans++;
			summary.LessonPlanIds.Add(lessonPlan.Id);
			// The lesson plan is the progress fallback until a submitted
			// Teacher Diary provides the actual delivered-lesson progress.
			summary.CompletionValues.Add(Math.Clamp(lessonPlan.PlannedCompletionPercentage, 0, 100));
		}

		if (lessonPlans.Count > 0)
		{
			var lessonPlanIds = lessonPlans.Select(plan => plan.Id).ToList();
			var submittedDiaries = await teacherDiaryRepository.GetListAsync(diary =>
				lessonPlanIds.Contains(diary.LessonPlanId) &&
				diary.Status == TeacherDiaryStatus.Submitted);
			var diaryByLessonPlan = submittedDiaries
				.GroupBy(diary => diary.LessonPlanId)
				.ToDictionary(group => group.Key, group => group.OrderByDescending(diary => diary.CreationTime).First());

			foreach (var lessonPlan in lessonPlans)
			{
				if (diaryByLessonPlan.TryGetValue(lessonPlan.Id, out var diary))
				{
					var summary = linkedByChapter[lessonPlan.ChapterId];
					var planIndex = summary.LessonPlanIds.IndexOf(lessonPlan.Id);
					if (planIndex >= 0)
					{
						summary.CompletionValues[planIndex] = diary.ActualCompletionPercentage;
					}
				}
			}
		}

		var homeworks = await homeworkRepository.GetListAsync(hw =>
			hw.StandardId == assignedSubject.StandardId &&
			hw.SubjectId == assignedSubject.SubjectId &&
			(!divisionId.HasValue ||
				hw.DivisionId == divisionId.Value ||
				hw.DivisionSchedules.Any(schedule => schedule.DivisionId == divisionId.Value)));
		var homeworkChapterLinks = new Dictionary<Guid, List<Guid>>();
		foreach (var homework in homeworks)
		{
			foreach (var chapterId in chapterIds.Where(chapterId => IsLinkedToChapter(homework.ChapterId, homework.ChapterIdsJson, chapterId)))
			{
				linkedByChapter[chapterId].Homeworks++;
				AddLinkedTask(homeworkChapterLinks, homework.Id, chapterId);
			}
		}

		var assignments = await assignmentRepository.GetListAsync(assignment =>
			assignment.StandardId == assignedSubject.StandardId &&
			assignment.SubjectId == assignedSubject.SubjectId &&
			(!divisionId.HasValue ||
				assignment.DivisionId == divisionId.Value ||
				assignment.DivisionSchedules.Any(schedule => schedule.DivisionId == divisionId.Value)));
		var assignmentChapterLinks = new Dictionary<Guid, List<Guid>>();
		foreach (var assignment in assignments)
		{
			foreach (var chapterId in chapterIds.Where(chapterId => IsLinkedToChapter(null, assignment.ChapterIdsJson, chapterId)))
			{
				linkedByChapter[chapterId].Assessments++;
				AddLinkedTask(assignmentChapterLinks, assignment.Id, chapterId);
			}
		}

		var questions = await questionBankRepository.GetListAsync(question =>
			question.StandardId == assignedSubject.StandardId &&
			question.SubjectId == assignedSubject.SubjectId &&
			question.ChapterId.HasValue &&
			chapterIds.Contains(question.ChapterId.Value) &&
			!question.IsArchived);
		foreach (var questionGroup in questions.GroupBy(question => question.ChapterId!.Value))
		{
			linkedByChapter[questionGroup.Key].Questions += questionGroup.Count();
			linkedByChapter[questionGroup.Key].Assessments++;
		}

		var blueprintRules = await blueprintChapterRuleRepository.GetListAsync(rule => chapterIds.Contains(rule.ChapterId));
		foreach (var chapterId in blueprintRules.Select(rule => rule.ChapterId).Distinct())
		{
			linkedByChapter[chapterId].Assessments++;
		}

		await AddRubricAssessmentLinksAsync(homeworkChapterLinks, assignmentChapterLinks, linkedByChapter);

		foreach (var dto in dtos)
		{
			var summary = linkedByChapter[dto.Id];
			dto.LessonPlanStatus = summary.LessonPlans > 0 ? "Linked" : DefaultStatus(dto.LessonPlanStatus, "Not Linked");
			if (summary.CompletionValues.Count > 0)
			{
				dto.ProgressPercentage = (int)Math.Round(summary.CompletionValues.Average());
				dto.CoverageStatus = dto.ProgressPercentage switch
				{
					100 => "Completed",
					> 0 => "In Progress",
					_ => "Not Started"
				};
			}
			dto.HomeworkStatus = summary.Homeworks > 0 ? "Mapped" : DefaultStatus(dto.HomeworkStatus, "Not Mapped");
			dto.AssessmentStatus = summary.Assessments > 0 ? "Mapped" : DefaultStatus(dto.AssessmentStatus, "Not Mapped");
			dto.QuestionCount = summary.Questions > 0 ? summary.Questions : dto.QuestionCount;
		}
	}

	private async Task AddRubricAssessmentLinksAsync(
		Dictionary<Guid, List<Guid>> homeworkChapterLinks,
		Dictionary<Guid, List<Guid>> assignmentChapterLinks,
		Dictionary<Guid, ChapterLinkSummary> linkedByChapter)
	{
		var homeworkIds = homeworkChapterLinks.Keys.ToList();
		var assignmentIds = assignmentChapterLinks.Keys.ToList();
		if (homeworkIds.Count == 0 && assignmentIds.Count == 0)
		{
			return;
		}

		var rubrics = await rubricRepository.GetListAsync(rubric =>
			!rubric.IsTemplate &&
			rubric.Status != RubricStatus.Archived &&
			((rubric.HomeworkId.HasValue && homeworkIds.Contains(rubric.HomeworkId.Value)) ||
			 (rubric.AssignmentId.HasValue && assignmentIds.Contains(rubric.AssignmentId.Value))));

		foreach (var rubric in rubrics)
		{
			if (rubric.HomeworkId.HasValue && homeworkChapterLinks.TryGetValue(rubric.HomeworkId.Value, out var homeworkChapterIds))
			{
				foreach (var chapterId in homeworkChapterIds)
				{
					linkedByChapter[chapterId].Assessments++;
				}
			}

			if (rubric.AssignmentId.HasValue && assignmentChapterLinks.TryGetValue(rubric.AssignmentId.Value, out var assignmentChapterIds))
			{
				foreach (var chapterId in assignmentChapterIds)
				{
					linkedByChapter[chapterId].Assessments++;
				}
			}
		}
	}

	private static void AddLinkedTask(Dictionary<Guid, List<Guid>> taskChapterLinks, Guid taskId, Guid chapterId)
	{
		if (!taskChapterLinks.TryGetValue(taskId, out var chapterIds))
		{
			chapterIds = new List<Guid>();
			taskChapterLinks[taskId] = chapterIds;
		}

		chapterIds.Add(chapterId);
	}

	private static bool IsLinkedToChapter(Guid? legacyChapterId, string? chapterIdsJson, Guid chapterId)
	{
		return legacyChapterId == chapterId ||
			(!string.IsNullOrWhiteSpace(chapterIdsJson) &&
			 chapterIdsJson.Contains(chapterId.ToString(), StringComparison.OrdinalIgnoreCase));
	}

	private static string DefaultStatus(string? currentStatus, string fallback) =>
		string.IsNullOrWhiteSpace(currentStatus) ? fallback : currentStatus;

	private static void ApplyResourceStatusFallback(IEnumerable<ChapterDto> dtos)
	{
		foreach (var dto in dtos)
		{
			dto.ResourceStatus = DefaultStatus(
				dto.ResourceStatus,
				dto.Documents.Count > 0 ? "Ready" : "Missing");
		}
	}

	private sealed class ChapterLinkSummary
	{
		public int LessonPlans { get; set; }
		public List<Guid> LessonPlanIds { get; } = new();
		public List<int> CompletionValues { get; } = new();
		public int Homeworks { get; set; }
		public int Assessments { get; set; }
		public int Questions { get; set; }
	}
}
