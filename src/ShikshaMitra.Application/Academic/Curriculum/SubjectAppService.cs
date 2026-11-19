using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Curriculum;

[RemoteService(IsEnabled = false)]
public class SubjectAppService(
		IRepository<Subject, Guid> subjectRepository,
		SubjectManager subjectManager,
		IRepository<AssignedSubject, Guid> assignedSubjectRepository,
		IRepository<ExamMark, Guid> examMarkRepository,
		IRepository<HomeworkAssignmentEntity, Guid> homeworkAssignmentRepository,
	IRepository<AssignmentEntity, Guid> assignmentRepository,
	IRepository<AssignedSubjectChapter, Guid> chapterRepository,
	IRepository<TeacherSubjectAllocation, Guid> teacherSubjectAllocationRepository,
	IRepository<LessonPlan, Guid> lessonPlanRepository,
	IRepository<Standard, Guid> standardRepository,
	ILogger<SubjectAppService> logger,
	IRepository<ElectiveGroup, Guid> electiveGroupRepository,
	SubjectAuditWriter subjectAuditWriter,
	IRepository<SubjectAuditLog, Guid> subjectAuditLogRepository,
	IRepository<WeeklyLectureSlot, Guid> weeklyLectureSlotRepository,
	IRepository<ShikshaMitra.Academic.Timetable.Timetables.Timetable, Guid> timetableRepository,
	IRepository<Exam, Guid> examRepository,
	AdmissionAssessmentSubjectUsageService admissionAssessmentUsage
) : ApplicationService, ISubjectAppService
{
	public async Task<List<SubjectLookupDto>> GetLookupAsync(Guid educationMediumId, string? searchTerm = null, bool includeArchived = false)
	{
		var query = (await subjectRepository.GetQueryableAsync()).Where(subject => subject.EducationMediumId == educationMediumId);
		if (!includeArchived) query = query.Where(subject => subject.Status != SubjectMasterStatus.Archived);
		if (!string.IsNullOrWhiteSpace(searchTerm))
			query = query.Where(subject => subject.Name.Contains(searchTerm) ||
				(subject.DisplayName != null && subject.DisplayName.Contains(searchTerm)) ||
				(subject.Category != null && subject.Category.Contains(searchTerm)));
		return await AsyncExecuter.ToListAsync(query.OrderBy(subject => subject.SortOrder ?? int.MaxValue)
			.ThenBy(subject => subject.Name).Select(subject => new SubjectLookupDto
		{
			Id = subject.Id, Name = subject.Name, DisplayName = subject.DisplayName,
			Category = subject.Category, Color = subject.Color, Icon = subject.Icon,
			Status = subject.Status, SortOrder = subject.SortOrder, EducationMediumId = subject.EducationMediumId
		}));
	}

	public async Task<SubjectDto> GetAsync(Guid id)
	{
		try
		{
			var subject = await subjectRepository.GetAsync(id);
			return await MapSubjectAsync(subject);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error retrieving subject with ID {SubjectId}", id);
			throw new UserFriendlyException("Error occurred while retrieving subject");
		}
	}

	public async Task<List<SubjectDto>> GetListAsync(
		Guid educationMediumId,
		string? searchTerm = null,
		bool includeArchived = false
	)
	{
		try
		{
			var queryable = await subjectRepository.GetQueryableAsync();

			// Filter by education medium
			queryable = queryable.Where(x => x.EducationMediumId == educationMediumId);
			if (!includeArchived)
			{
				queryable = queryable.Where(x => x.Status != SubjectMasterStatus.Archived);
			}

			// Apply search filter if provided
			if (!string.IsNullOrWhiteSpace(searchTerm))
			{
				queryable = queryable.Where(x =>
					x.Name.Contains(searchTerm)
					|| (x.DisplayName != null && x.DisplayName.Contains(searchTerm))
					|| (x.Category != null && x.Category.Contains(searchTerm))
				);
			}

			// Use ABP's AsyncExecuter for asynchronous query execution
			var subjects = await AsyncExecuter.ToListAsync(
				queryable.OrderBy(x => x.SortOrder ?? int.MaxValue).ThenBy(x => x.Name)
			);

			var usageBySubject = await GetUsageCountsAsync(subjects.Select(subject => subject.Id).ToList());
			return subjects.Select(subject => MapSubject(subject, usageBySubject[subject.Id])).ToList();
		}
		catch (UserFriendlyException ex)
		{
			logger.LogError(ex.Message);
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(
				ex,
				"Error retrieving subjects for medium {EducationMediumId}",
				educationMediumId
			);
			throw new UserFriendlyException("Error occurred while retrieving subjects");
		}
	}

	public async Task<SubjectDto> CreateAsync(CreateUpdateSubjectDto createSubjectDto)
	{
		try
		{
			Subject subject = await subjectManager.CreateSubject(
				createSubjectDto.Name,
				createSubjectDto.Color,
				createSubjectDto.EducationMediumId,
				createSubjectDto.DisplayName,
				createSubjectDto.Category,
				createSubjectDto.Icon,
				createSubjectDto.Description,
				createSubjectDto.Status,
				createSubjectDto.SortOrder
			);

			await subjectRepository.InsertAsync(subject);

			await subjectAuditWriter.WriteAsync(
				subject.Id,
				"Created",
				null,
				ToJsonSnapshot(subject),
				entityType: "subject",
				entityName: subject.Name
			);

			return MapSubject(subject, new SubjectUsageCounts());
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(
				ex,
				"Error creating subject with name {SubjectName}",
				createSubjectDto.Name
			);
			throw new UserFriendlyException("Error occurred while creating subject");
		}
	}

	public async Task<List<SubjectDto>> BulkCreateAsync(BulkCreateSubjectsDto input)
	{
		try
		{
			var subjects = await subjectManager.CreateSubjectsAsync(input.Subjects.Select(subject => new SubjectCreation(
				subject.Name, subject.Color, subject.EducationMediumId, subject.DisplayName, subject.Category,
				subject.Icon, subject.Description, subject.Status, subject.SortOrder)).ToList());
			await subjectRepository.InsertManyAsync(subjects);
			await subjectAuditWriter.WriteCreatedManyAsync(subjects.Select(subject =>
				(SubjectId: subject.Id, Snapshot: ToJsonSnapshot(subject), subject.Name)).ToList());
			return subjects.Select(subject => MapSubject(subject, new SubjectUsageCounts())).ToList();
		}
		catch (BusinessException) { throw; }
		catch (Exception error)
		{
			logger.LogError(error, "Error creating subject batch");
			throw new UserFriendlyException("Error occurred while creating subject");
		}
	}

	public async Task<SubjectDuplicateCheckResultDto> CheckDuplicateAsync(
		Guid educationMediumId,
		SubjectDuplicateCheckDto input
	)
	{
		var normalized = NormalizeSubjectName(input.Name);
		var queryable = await subjectRepository.GetQueryableAsync();
		var subjects = await AsyncExecuter.ToListAsync(
			queryable.Where(x => x.EducationMediumId == educationMediumId && x.Id != input.ExcludeSubjectId)
		);

		var matches = subjects
			.Select(x => new
			{
				Subject = x,
				Normalized = NormalizeSubjectName(x.Name),
				DisplayNormalized = NormalizeSubjectName(x.DisplayName ?? string.Empty)
			})
			.Where(x =>
				x.Normalized == normalized
				|| x.DisplayNormalized == normalized
				|| IsSimilarSubjectName(normalized, x.Normalized)
				|| IsSimilarSubjectName(normalized, x.DisplayNormalized)
			)
			.Select(x => new SubjectDuplicateMatchDto
			{
				Id = x.Subject.Id,
				Name = x.Subject.Name,
				DisplayName = x.Subject.DisplayName,
				Status = x.Subject.Status,
				MatchType = x.Normalized == normalized || x.DisplayNormalized == normalized ? "Exact" : "Similar"
			})
			.ToList();

		return new SubjectDuplicateCheckResultDto
		{
			HasExactDuplicate = matches.Any(x => x.MatchType == "Exact"),
			HasSimilarDuplicate = matches.Any(x => x.MatchType == "Similar"),
			Matches = matches
		};
	}

	public async Task<SubjectDto> UpdateAsync(Guid id, CreateUpdateSubjectDto updateSubjectDto)
	{
		try
		{
			var existingSubject = await subjectRepository.GetAsync(id);
			var beforeSnapshot = ToJsonSnapshot(existingSubject);

			var subject = await subjectManager.UpdateAsync(
				id,
				updateSubjectDto.Name,
				updateSubjectDto.Color,
				updateSubjectDto.EducationMediumId,
				updateSubjectDto.DisplayName,
				updateSubjectDto.Category,
				updateSubjectDto.Icon,
				updateSubjectDto.Description,
				updateSubjectDto.Status,
				updateSubjectDto.SortOrder
			);

			await subjectRepository.UpdateAsync(subject);

			if (updateSubjectDto.Status == SubjectMasterStatus.Active)
			{
				var assignments = await assignedSubjectRepository.GetListAsync(a => a.SubjectId == id && a.Status == SubjectStatus.Inactive);
				if (assignments.Count > 0)
				{
					foreach (var assignment in assignments)
					{
						assignment.ChangeStatus(SubjectStatus.Draft);
					}
					await assignedSubjectRepository.UpdateManyAsync(assignments);
				}
			}
			else if (updateSubjectDto.Status == SubjectMasterStatus.Inactive)
			{
				var assignments = await assignedSubjectRepository.GetListAsync(a => a.SubjectId == id && a.Status != SubjectStatus.Inactive && a.Status != SubjectStatus.Archived);
				if (assignments.Count > 0)
				{
					foreach (var assignment in assignments)
					{
						assignment.ChangeStatus(SubjectStatus.Inactive);
					}
					await assignedSubjectRepository.UpdateManyAsync(assignments);
				}
			}

			await subjectAuditWriter.WriteAsync(
				subject.Id,
				"Updated",
				beforeSnapshot,
				ToJsonSnapshot(subject),
				entityType: "subject",
				entityName: subject.Name
			);

			return await MapSubjectAsync(subject);
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error updating subject with ID {SubjectId}", id);
			throw new UserFriendlyException("Error occurred while updating subject");
		}
	}

	public async Task<SubjectDto> ArchiveAsync(Guid id, SubjectStatusChangeDto input)
	{
		var subject = await subjectRepository.GetAsync(id);
		var beforeSnapshot = ToJsonSnapshot(subject);
		subject.Archive(input.Reason);
		await subjectRepository.UpdateAsync(subject);
		
		await subjectAuditWriter.WriteAsync(
			subject.Id,
			"Archived",
			beforeSnapshot,
			ToJsonSnapshot(subject),
			entityType: "subject",
			entityName: subject.Name,
			reason: input.Reason
		);

		return await MapSubjectAsync(subject);
	}

	public async Task<SubjectDto> RestoreAsync(Guid id)
	{
		var subject = await subjectRepository.GetAsync(id);
		var beforeSnapshot = ToJsonSnapshot(subject);

		// Check if another active/inactive subject with the same name already exists
		var normalizedName = subject.Name.Trim().ToUpperInvariant();
		var duplicateActiveSubject = await subjectRepository.FindAsync(s =>
			s.Name.ToUpper() == normalizedName
			&& s.EducationMediumId == subject.EducationMediumId
			&& s.Id != id
			&& s.Status != SubjectMasterStatus.Archived);

		if (duplicateActiveSubject != null)
		{
			throw new BusinessException(CurriculumErrorCodes.DuplicateSubjectName)
				.WithData("Name", subject.Name)
				.WithData("message", $"Cannot restore '{subject.Name}' because another active/inactive subject with the same name already exists.");
		}

		subject.Restore();
		await subjectRepository.UpdateAsync(subject);

		var assignments = await assignedSubjectRepository.GetListAsync(a => a.SubjectId == id && a.Status == SubjectStatus.Inactive);
		if (assignments.Count > 0)
		{
			foreach (var assignment in assignments)
			{
				assignment.ChangeStatus(SubjectStatus.Draft);
			}
			await assignedSubjectRepository.UpdateManyAsync(assignments);
		}

		await subjectAuditWriter.WriteAsync(
			subject.Id,
			"Restored",
			beforeSnapshot,
			ToJsonSnapshot(subject),
			entityType: "subject",
			entityName: subject.Name
		);

		return await MapSubjectAsync(subject);
	}

	public async Task DeleteAsync(Guid id)
	{
		try
		{
			var subject = await subjectRepository.GetAsync(id);

			// Block deletion if this subject is the last one in an elective group
			var allGroups = await electiveGroupRepository.WithDetailsAsync(eg => eg.Subjects);
			var blockingGroup = allGroups
				.FirstOrDefault(eg => eg.Subjects.Count == 1 && eg.Subjects.Any(s => s.SubjectId == id));
			if (blockingGroup != null)
			{
				throw new BusinessException(CurriculumErrorCodes.SubjectIsLastInElectiveGroup)
					.WithData("message", $"Cannot delete '{subject.Name}' because it is the only subject in elective group '{blockingGroup.Name}'. Please delete the elective group first.");
			}

			// Block deletion if the subject is in use anywhere
			var usageParts = new System.Collections.Generic.List<string>();
			var assessmentCount = (await admissionAssessmentUsage.CountAsync([id]))[id];
			if (assessmentCount > 0) usageParts.Add($"{assessmentCount} admission assessment stage(s)");

			var allocationCount = await assignedSubjectRepository.CountAsync(a => a.SubjectId == id);
			if (allocationCount > 0)
				usageParts.Add($"{allocationCount} class allocation(s)");

			var weeklySlotCount = await AsyncExecuter.CountAsync((await weeklyLectureSlotRepository.GetQueryableAsync())
				.SelectMany(slot => slot.Subjects).Where(subject => subject.SubjectId == id));
			if (weeklySlotCount > 0)
				usageParts.Add($"{weeklySlotCount} weekly lecture slot(s)");

			var timetableSlotCount = await AsyncExecuter.CountAsync((await timetableRepository.GetQueryableAsync())
				.SelectMany(timetable => timetable.Slots).SelectMany(slot => slot.Subjects).Where(subject => subject.SubjectId == id));
			if (timetableSlotCount > 0)
				usageParts.Add($"{timetableSlotCount} timetable slot(s)");

			var examSubjectCount = await AsyncExecuter.CountAsync((await examRepository.GetQueryableAsync())
				.SelectMany(exam => exam.ExamSubjects).Where(subject => subject.SubjectId == id));
			if (examSubjectCount > 0)
				usageParts.Add($"{examSubjectCount} exam(s)");

			var examMarkCount = await examMarkRepository.CountAsync(m => m.SubjectId == id);
			if (examMarkCount > 0)
				usageParts.Add($"{examMarkCount} exam result(s)");

			var homeworkCount = await homeworkAssignmentRepository.CountAsync(h => h.SubjectId == id);
			if (homeworkCount > 0)
				usageParts.Add($"{homeworkCount} homework(s)");

			var assignmentCount = await assignmentRepository.CountAsync(a => a.SubjectId == id);
			if (assignmentCount > 0)
				usageParts.Add($"{assignmentCount} assignment(s)");

			if (usageParts.Count > 0)
			{
				throw new BusinessException(CurriculumErrorCodes.SubjectIsInUse)
					.WithData("message", $"Cannot delete '{subject.Name}' because it is in use: {string.Join(", ", usageParts)}. Please remove these usages first.");
			}

			await subjectRepository.DeleteAsync(subject);
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error deleting subject with ID {SubjectId}", id);
			throw new UserFriendlyException("Error occurred while deleting subject");
		}
	}

	public async Task<SubjectUsageCheckDto> CheckUsagesAsync(Guid id)
	{
		try
		{
			var subject = await subjectRepository.GetAsync(id);

			var usage = await GetUsageCountsAsync(id);
			var allocations = usage.Allocations;
			var weeklySlots = usage.WeeklySlots;
			var timetableSlots = usage.TimetableSlots;
			var examSubjects = usage.ExamSubjects;
			var examResults = usage.ExamResults;
			var homeworks = usage.Homeworks;
			var assignments = usage.Assignments;

			var total = usage.Total;

			return new SubjectUsageCheckDto
			{
				SubjectId = id,
				SubjectName = subject.Name,
				HasUsages = total > 0,
				Allocations = allocations,
				WeeklyLectureSlots = weeklySlots,
				TimetableSlots = timetableSlots,
				ExamSubjects = examSubjects,
				ExamResults = examResults,
				Homeworks = homeworks,
				Assignments = assignments,
				AdmissionAssessments = usage.AdmissionAssessments
			};
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error checking usages for subject {SubjectId}", id);
			throw new UserFriendlyException("Error occurred while checking subject usages");
		}
	}

	public async Task<SubjectUsageDetailDto> GetUsageDetailsAsync(Guid id)
	{
		var subject = await subjectRepository.GetAsync(id);
		var assignedSubjects = await assignedSubjectRepository.GetListAsync(x => x.SubjectId == id);
		var standards = await standardRepository.GetListAsync(x => assignedSubjects.Select(a => a.StandardId).Contains(x.Id));
		var groups = await electiveGroupRepository.WithDetailsAsync(x => x.Subjects);
		var usage = await GetUsageCountsAsync(id);

		var detail = new SubjectUsageDetailDto
		{
			SubjectId = id,
			SubjectName = subject.Name,
			CanDelete = usage.Total == 0,
			SafeActions = usage.Total == 0
				? ["Edit display name", "Edit color", "Edit icon", "Delete"]
				: ["Edit display name", "Edit color", "Edit icon", "Archive"],
			Usage = new SubjectUsageSummaryDto
			{
				SubjectAllocations = assignedSubjects.Select(a => new SubjectAllocationUsageDto
				{
					Standard = standards.FirstOrDefault(s => s.Id == a.StandardId)?.Name,
					Code = a.SubjectCode
				}).ToList(),
				ElectiveGroups = groups.Where(g => g.Subjects.Any(s => s.SubjectId == id)).Select(g => g.Name).ToList(),
				ChaptersCount = usage.Chapters,
				TimetablePeriodsCount = usage.WeeklySlots + usage.TimetableSlots,
				ExamSchedulesCount = usage.ExamSubjects,
				LessonPlansCount = usage.LessonPlans,
				HomeworkCount = usage.Homeworks + usage.Assignments,
				TeacherMappingsCount = usage.TeacherMappings,
				AdmissionAssessmentsCount = usage.AdmissionAssessments
			}
		};

		return detail;
	}

	public async Task<List<SubjectAuditLogDto>> GetAuditLogsAsync(Guid id)
	{
		var logs = await subjectAuditLogRepository.GetListAsync(x => x.SubjectId == id);
		return logs.Select(x => new SubjectAuditLogDto
		{
			ChangedDate = x.ChangedAt,
			ChangedBy = x.ActorNameSnapshot,
			Action = $"Subject {x.Action}",
			BeforeValue = null,
			AfterValue = x.EntityName,
			Reason = x.Reason
		}).OrderByDescending(x => x.ChangedDate).ToList();
	}

	private async Task<SubjectDto> MapSubjectAsync(Subject subject)
	{
		var usage = await GetUsageCountsAsync(subject.Id);
		return MapSubject(subject, usage);
	}

	private SubjectDto MapSubject(Subject subject, SubjectUsageCounts usage)
	{
		var dto = ObjectMapper.Map<Subject, SubjectDto>(subject);
		dto.UsedInClassesCount = usage.Allocations;
		dto.UsedInElectiveGroupsCount = usage.ElectiveGroups;
		dto.IsUsed = usage.Total > 0;
		dto.CanDelete = usage.Total == 0;
		dto.LastUpdatedAt = subject.LastModificationTime ?? subject.CreationTime;
		dto.LastUpdatedBy = (subject.LastModifierId ?? subject.CreatorId)?.ToString();
		return dto;
	}

	private async Task<SubjectUsageCounts> GetUsageCountsAsync(Guid subjectId)
	{
		return (await GetUsageCountsAsync(new List<Guid> { subjectId }))[subjectId];
	}

	private async Task<Dictionary<Guid, SubjectUsageCounts>> GetUsageCountsAsync(IReadOnlyCollection<Guid> subjectIds)
	{
		var result = subjectIds.Distinct().ToDictionary(id => id, _ => new SubjectUsageCounts());
		if (result.Count == 0)
		{
			return result;
		}
		var admissionCounts = await admissionAssessmentUsage.CountAsync(subjectIds);
		foreach (var entry in admissionCounts) result[entry.Key].AdmissionAssessments = entry.Value;

		var assignments = (await assignedSubjectRepository.GetQueryableAsync())
			.Where(assignment => subjectIds.Contains(assignment.SubjectId));
		await PopulateUsageCountsAsync(assignments.Select(assignment => assignment.SubjectId), result,
			(usage, count) => usage.Allocations = count);

		var groups = await electiveGroupRepository.GetQueryableAsync();
		var groupSubjects = groups.SelectMany(group => group.Subjects, (group, subject) => new { GroupId = group.Id, subject.SubjectId })
			.Where(subject => subjectIds.Contains(subject.SubjectId)).Distinct();
		await PopulateUsageCountsAsync(groupSubjects.Select(subject => subject.SubjectId), result,
			(usage, count) => usage.ElectiveGroups = count);

		var chapters = await chapterRepository.GetQueryableAsync();
		var chapterSubjects = from chapter in chapters
			join assignment in assignments on chapter.AssignedSubjectId equals assignment.Id
			select assignment.SubjectId;
		await PopulateUsageCountsAsync(chapterSubjects, result, (usage, count) => usage.Chapters = count);

		// Child rows inherit tenant and soft-delete ownership from their aggregate.
		var weeklySlots = (await weeklyLectureSlotRepository.GetQueryableAsync()).SelectMany(slot => slot.Subjects);
		await PopulateUsageCountsAsync(weeklySlots.Where(slot => slot.SubjectId.HasValue && subjectIds.Contains(slot.SubjectId.Value))
			.Select(slot => slot.SubjectId!.Value), result, (usage, count) => usage.WeeklySlots = count);
		var timetableSlots = (await timetableRepository.GetQueryableAsync()).SelectMany(timetable => timetable.Slots)
			.SelectMany(slot => slot.Subjects);
		await PopulateUsageCountsAsync(timetableSlots.Where(slot => subjectIds.Contains(slot.SubjectId)).Select(slot => slot.SubjectId),
			result, (usage, count) => usage.TimetableSlots = count);
		var examSubjects = (await examRepository.GetQueryableAsync()).SelectMany(exam => exam.ExamSubjects);
		await PopulateUsageCountsAsync(examSubjects.Where(subject => subjectIds.Contains(subject.SubjectId)).Select(subject => subject.SubjectId),
			result, (usage, count) => usage.ExamSubjects = count);
		var examMarks = await examMarkRepository.GetQueryableAsync();
		await PopulateUsageCountsAsync(examMarks.Where(mark => subjectIds.Contains(mark.SubjectId)).Select(mark => mark.SubjectId),
			result, (usage, count) => usage.ExamResults = count);
		var homeworks = await homeworkAssignmentRepository.GetQueryableAsync();
		await PopulateUsageCountsAsync(homeworks.Where(homework => subjectIds.Contains(homework.SubjectId)).Select(homework => homework.SubjectId),
			result, (usage, count) => usage.Homeworks = count);
		var learningTasks = await assignmentRepository.GetQueryableAsync();
		await PopulateUsageCountsAsync(learningTasks.Where(assignment => subjectIds.Contains(assignment.SubjectId)).Select(assignment => assignment.SubjectId),
			result, (usage, count) => usage.Assignments = count);
		var teacherMappings = await teacherSubjectAllocationRepository.GetQueryableAsync();
		await PopulateUsageCountsAsync(teacherMappings.Where(allocation => subjectIds.Contains(allocation.SubjectId)).Select(allocation => allocation.SubjectId),
			result, (usage, count) => usage.TeacherMappings = count);
		var lessonPlans = await lessonPlanRepository.GetQueryableAsync();
		await PopulateUsageCountsAsync(lessonPlans.Where(plan => subjectIds.Contains(plan.SubjectId)).Select(plan => plan.SubjectId),
			result, (usage, count) => usage.LessonPlans = count);
		return result;
	}

	private async Task PopulateUsageCountsAsync(
		IQueryable<Guid> subjectIds,
		IReadOnlyDictionary<Guid, SubjectUsageCounts> usages,
		Action<SubjectUsageCounts, int> applyCount)
	{
		var counts = await AsyncExecuter.ToListAsync(subjectIds.GroupBy(id => id)
			.Select(group => new { SubjectId = group.Key, Count = group.Count() }));
		foreach (var item in counts)
		{
			applyCount(usages[item.SubjectId], item.Count);
		}
	}

	private static string NormalizeSubjectName(string name)
	{
		var normalized = new string(name.Trim().ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
		return normalized switch
		{
			"MATH" or "MATHS" => "MATHEMATICS",
			"SCI" => "SCIENCE",
			"SS" or "SOCIALSTUDIES" => "SOCIALSCIENCE",
			"COMP" or "COMPUTER" => "COMPUTERSCIENCE",
			_ => normalized
		};
	}

	private static bool IsSimilarSubjectName(string left, string right)
	{
		return left.Length >= 3 && right.Length >= 3 && (left.Contains(right) || right.Contains(left));
	}

	private sealed class SubjectUsageCounts
	{
		public int Allocations { get; set; }
		public int ElectiveGroups { get; set; }
		public int Chapters { get; set; }
		public int WeeklySlots { get; set; }
		public int TimetableSlots { get; set; }
		public int ExamSubjects { get; set; }
		public int ExamResults { get; set; }
		public int Homeworks { get; set; }
		public int Assignments { get; set; }
		public int TeacherMappings { get; set; }
		public int LessonPlans { get; set; }
		public int AdmissionAssessments { get; set; }
		public int Total => Allocations + ElectiveGroups + Chapters + WeeklySlots + TimetableSlots
			+ ExamSubjects + ExamResults + Homeworks + Assignments + TeacherMappings + LessonPlans + AdmissionAssessments;
	}

	private static string ToJsonSnapshot(Subject subject)
	{
		return System.Text.Json.JsonSerializer.Serialize(new
		{
			subject.Id,
			subject.Name,
			subject.DisplayName,
			subject.Category,
			subject.Color,
			subject.Icon,
			subject.Description,
			subject.Status,
			subject.SortOrder,
			subject.EducationMediumId
		}, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
	}
}
