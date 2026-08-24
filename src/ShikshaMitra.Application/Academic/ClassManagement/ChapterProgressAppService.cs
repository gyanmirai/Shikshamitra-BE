using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Syllabus;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.HR.Employees;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace ShikshaMitra.Academic.ClassManagement.ChapterProgress;

[RemoteService(IsEnabled = false)]
public class ChapterProgressAppService(
	IRepository<AssignedSubject, Guid> subjectAssignmentRepository,
	IRepository<AssignedSubjectChapter, Guid> chapterRepository,
	IRepository<Syllabus.ChapterProgress, Guid> progressRepository,
	IRepository<ChapterProgressLog, Guid> progressLogRepository,
	IRepository<Subject, Guid> subjectRepository,
	IRepository<TeacherSubjectAllocation, Guid> teacherSubjectAllocationRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Division, Guid> divisionRepository,
	IUserScopeService userScopeService,
	IGuidGenerator guidGenerator,
	ILogger<ChapterProgressAppService> logger,
	IRepository<Employee, Guid> employeeRepository,
	IReportingManagerAccessService reportingManagerAccessService
) : ApplicationService, IChapterProgressAppService
{
	public async Task<List<ClassDivisionSubjectDto>> GetSubjectsForClassDivisionAsync(
		Guid academicMediumId,
		Guid classId,
		Guid divisionId
	)
	{
		// Validate required parameters
		if (academicMediumId == Guid.Empty)
		{
			throw new ArgumentException("Academic medium ID cannot be empty", nameof(academicMediumId));
		}

		if (classId == Guid.Empty)
		{
			throw new ArgumentException("Class ID cannot be empty", nameof(classId));
		}

		if (divisionId == Guid.Empty)
		{
			throw new ArgumentException("Division ID cannot be empty", nameof(divisionId));
		}

		var hasSubjectAllocationAccess = await HasAnyTeacherSubjectAccessAsync(classId, divisionId);
		if (!hasSubjectAllocationAccess)
		{
			await EnsureCurrentUserCanAccessClassDivisionAsync(classId, divisionId);
		}

		logger.LogInformation(
			"Getting subjects for class {ClassId}, division {DivisionId}",
			classId, divisionId
		);

		// Get all subject assignments for the class
		var assignments = await subjectAssignmentRepository.GetListAsync(a =>
			a.StandardId == classId
		);
		if (hasSubjectAllocationAccess)
		{
			var accessibleSubjectIds = await GetTeacherSubjectIdsAsync(classId, divisionId);
			assignments = assignments.Where(a => accessibleSubjectIds.Contains(a.SubjectId)).ToList();
		}

		var result = new List<ClassDivisionSubjectDto>();
		if (!assignments.Any())
		{
			return result;
		}

		var subjectIds = assignments.Select(a => a.SubjectId).Distinct().ToList();
		var subjects = await subjectRepository.GetListAsync(s => subjectIds.Contains(s.Id));
		var subjectLookup = subjects.ToDictionary(s => s.Id);

		// AssignedSubjectChapter.AssignedSubjectId is the AssignedSubject.Id (not Subject.Id)
		var assignmentIds = assignments.Select(a => a.Id).Distinct().ToList();
		var chapters = await chapterRepository.GetListAsync(c => assignmentIds.Contains(c.AssignedSubjectId));
		var chaptersByAssignment = chapters
			.GroupBy(c => c.AssignedSubjectId)
			.ToDictionary(g => g.Key, g => g.ToList());
		var chapterIds = chapters.Select(c => c.Id).ToList();
		var progresses = chapterIds.Count == 0
			? new List<Syllabus.ChapterProgress>()
			: await progressRepository.GetListAsync(p =>
				chapterIds.Contains(p.ChapterId) &&
				p.DivisionId == divisionId
			);
		// Group by ChapterId and select most recent to handle duplicates
		var progressLookup = progresses
			.GroupBy(p => p.ChapterId)
			.ToDictionary(
				g => g.Key,
				g => g.OrderByDescending(p => p.CreationTime).First()
			);

		foreach (var assignment in assignments)
		{
			if (!subjectLookup.TryGetValue(assignment.SubjectId, out var subject))
			{
				continue;
			}

			if (!chaptersByAssignment.TryGetValue(assignment.Id, out var subjectChapters))
			{
				subjectChapters = new List<AssignedSubjectChapter>();
			}

			var totalChapters = subjectChapters.Count;
			var completedChapters = 0;
			var progressSum = 0;
			var expectedProgressSum = 0;
			DateTime? lastUpdatedAt = null;
			var subjectRiskRank = 0;

			foreach (var chapter in subjectChapters)
			{
				progressLookup.TryGetValue(chapter.Id, out var progress);

				var expectedProgress = CalculateExpectedProgress(
					chapter.ChapterStartTime == default ? null : chapter.ChapterStartTime.UtcDateTime,
					chapter.ChapterEndTime == default ? null : chapter.ChapterEndTime.UtcDateTime
				);
				var currentProgress = progress?.Progress ?? 0;
				var riskLevel = CalculateRiskLevel(currentProgress, expectedProgress, progress?.LastModificationTime ?? progress?.CreationTime);

				expectedProgressSum += expectedProgress;
				progressSum += currentProgress;
				subjectRiskRank = Math.Max(subjectRiskRank, GetRiskRank(riskLevel));

				var progressUpdatedAt = progress?.LastModificationTime ?? progress?.CreationTime;
				if (progressUpdatedAt.HasValue && (!lastUpdatedAt.HasValue || progressUpdatedAt.Value > lastUpdatedAt.Value))
				{
					lastUpdatedAt = progressUpdatedAt;
				}

				if (currentProgress == 100)
				{
					completedChapters++;
				}
			}

			var progressPercentage = totalChapters > 0
				? (int) Math.Round(progressSum / (double) totalChapters)
				: 0;
			var expectedProgressPercentage = totalChapters > 0
				? (int) Math.Round(expectedProgressSum / (double) totalChapters)
				: 0;
			var variancePercentage = progressPercentage - expectedProgressPercentage;
			var scheduleStatus = CalculateScheduleStatus(progressPercentage, expectedProgressPercentage, completedChapters == totalChapters && totalChapters > 0);

			result.Add(new ClassDivisionSubjectDto
			{
				SubjectId = subject.Id,
				SubjectName = subject.Name,
				SubjectCode = assignment.SubjectCode,
				SubjectColor = subject.Color,
				TotalChapters = totalChapters,
				CompletedChapters = completedChapters,
				ProgressPercentage = progressPercentage,
				ExpectedProgressPercentage = expectedProgressPercentage,
				VariancePercentage = variancePercentage,
				ScheduleStatus = scheduleStatus,
				RiskLevel = GetRiskLevelFromRank(subjectRiskRank),
				LastUpdatedAt = lastUpdatedAt
			});
		}

		return result;
	}

	public async Task<PaginatedChapterProgressResponseDto> GetChapterProgressListAsync(
		Guid academicMediumId,
		Guid classId,
		Guid divisionId,
		Guid subjectId,
		string? search = null
	)
	{
		// Validate required parameters
		if (academicMediumId == Guid.Empty)
		{
			throw new ArgumentException("Academic medium ID cannot be empty", nameof(academicMediumId));
		}

		if (classId == Guid.Empty)
		{
			throw new ArgumentException("Class ID cannot be empty", nameof(classId));
		}

		if (divisionId == Guid.Empty)
		{
			throw new ArgumentException("Division ID cannot be empty", nameof(divisionId));
		}

		if (subjectId == Guid.Empty)
		{
			throw new ArgumentException("Subject ID cannot be empty", nameof(subjectId));
		}

		if (!await HasTeacherSubjectAccessAsync(classId, subjectId, divisionId))
		{
			await EnsureCurrentUserCanAccessClassDivisionAsync(classId, divisionId);
		}

		logger.LogInformation(
			"Getting chapter progress for subject {SubjectId}, division {DivisionId}, class {ClassId}, academicMedium {AcademicMediumId}",
			subjectId, divisionId, classId, academicMediumId
		);

		// Get all chapters for the subject
		// AssignedSubjectChapter.AssignedSubjectId is the AssignedSubject.Id (not Subject.Id)
		// Look up the AssignedSubject row for this (classId, subjectId) pair
		// Use GetListAsync+FirstOrDefault instead of FindAsync to avoid InvalidOperationException
		// when duplicate AssignedSubject rows exist for the same (StandardId, SubjectId).
		var assignment = (await subjectAssignmentRepository.GetListAsync(a =>
			a.StandardId == classId && a.SubjectId == subjectId
		)).FirstOrDefault();
		if (assignment == null)
		{
			return new PaginatedChapterProgressResponseDto { TotalRecords = 0, Records = [] };
		}

		var chaptersQuery = await chapterRepository.GetQueryableAsync();
		chaptersQuery = chaptersQuery.Where(c => c.AssignedSubjectId == assignment.Id);

		if (!string.IsNullOrWhiteSpace(search))
		{
			// Use culture-invariant, DB-friendly search
			// Try to parse search as number for ChapterNumber comparison
			var isNumericSearch = int.TryParse(search, out var searchNumber);

			// Build pattern for case-insensitive LIKE search
			var searchPattern = $"%{search}%";

			chaptersQuery = chaptersQuery.Where(c =>
				c.ChapterName.ToLower().Contains(search.ToLower()) ||
				(isNumericSearch && c.ChapterNumber == searchNumber)
			);
		}

		var chapters = await AsyncExecuter.ToListAsync(chaptersQuery.OrderBy(c => c.ChapterNumber));

		// Get progress for all chapters
		var chapterIds = chapters.Select(c => c.Id).ToList();
		var progresses = await progressRepository.GetListAsync(p =>
			chapterIds.Contains(p.ChapterId) &&
			p.DivisionId == divisionId
		);
		// Group by ChapterId and select most recent to handle duplicates
		var progressLookup = progresses
			.GroupBy(p => p.ChapterId)
			.ToDictionary(
				g => g.Key,
				g => g.OrderByDescending(p => p.CreationTime).First()
			);

		var result = new PaginatedChapterProgressResponseDto
		{
			TotalRecords = chapters.Count,
			Records = chapters.Select(chapter =>
			{
				progressLookup.TryGetValue(chapter.Id, out var progress);
				var expectedProgress = CalculateExpectedProgress(
					chapter.ChapterStartTime == default ? null : chapter.ChapterStartTime.UtcDateTime,
					chapter.ChapterEndTime == default ? null : chapter.ChapterEndTime.UtcDateTime
				);
				var currentProgress = progress?.Progress ?? 0;
				var lastUpdatedAt = progress?.LastModificationTime ?? progress?.CreationTime;
				var status = CalculateChapterStatus(
					currentProgress,
					chapter.ChapterStartTime == default ? null : chapter.ChapterStartTime.UtcDateTime,
					chapter.ChapterEndTime == default ? null : chapter.ChapterEndTime.UtcDateTime
				);

				return new ChapterProgressRecordDto
				{
					ChapterId = chapter.Id,
					ChapterNumber = chapter.ChapterNumber,
					ChapterName = chapter.ChapterName,
					StartDate = chapter.ChapterStartTime == default ? null : chapter.ChapterStartTime.UtcDateTime,
					EndDate = chapter.ChapterEndTime == default ? null : chapter.ChapterEndTime.UtcDateTime,
					Progress = currentProgress,
					ActualStartDate = progress?.StartDate,
					ActualEndDate = progress?.CompletionDate,
					ExpectedProgress = expectedProgress,
					Variance = currentProgress - expectedProgress,
					Status = status,
					ScheduleStatus = CalculateScheduleStatus(currentProgress, expectedProgress, currentProgress == 100),
					RiskLevel = CalculateRiskLevel(currentProgress, expectedProgress, lastUpdatedAt),
					LastUpdatedBy = null,
					LastUpdatedAt = lastUpdatedAt
				};
			}).ToList()
		};

		return result;
	}

	public async Task<ApiSuccessResponseDto> UpdateChapterProgressAsync(
		Guid chapterId,
		UpdateChapterProgressDto input
	)
	{
		if (chapterId == Guid.Empty)
		{
			throw new BusinessException("ClassManagement:ChapterRequired");
		}

		if (input.DivisionId == Guid.Empty)
		{
			throw new BusinessException("ClassManagement:DivisionRequired");
		}

		var chapter = await chapterRepository.FirstOrDefaultAsync(candidate => candidate.Id == chapterId);
		if (chapter == null)
		{
			throw new BusinessException("ClassManagement:ChapterNotFound")
				.WithData("ChapterId", chapterId);
		}

		var assignment = await subjectAssignmentRepository.FirstOrDefaultAsync(
			candidate => candidate.Id == chapter.AssignedSubjectId);
		if (assignment == null)
		{
			throw new BusinessException("ClassManagement:ChapterNotFound")
				.WithData("ChapterId", chapterId);
		}

		if (!await HasTeacherSubjectAccessAsync(assignment.StandardId, assignment.SubjectId, input.DivisionId))
		{
			await EnsureCurrentUserCanAccessClassDivisionAsync(assignment.StandardId, input.DivisionId);
		}

		logger.LogInformation(
			"Updating progress for chapter {ChapterId} in division {DivisionId} to {Progress}%",
			chapterId, input.DivisionId, input.Progress
		);

		try
		{
			// Retry logic to handle race conditions when inserting new progress records
			// If two concurrent requests try to insert for the same (ChapterId, DivisionId),
			// one will fail with a duplicate key error. We retry by reloading the record.
			const int maxRetries = 3;
			for (int attempt = 1; attempt <= maxRetries; attempt++)
			{
				try
				{
					// Find existing progress record for the chapter and division
					var progress = await progressRepository.FirstOrDefaultAsync(p =>
						p.ChapterId == chapterId &&
						p.DivisionId == input.DivisionId
					);

					if (progress != null)
					{
						EnsureProgressUpdateAllowed(progress.Progress, input.Progress, input.Description);
						var previousProgress = progress.Progress;

						// Update existing record
						progress.UpdateProgress(input.Progress, input.Description);
						await progressRepository.UpdateAsync(progress);

						logger.LogDebug(
							"Updated existing progress for chapter {ChapterId}, division {DivisionId}",
							chapterId, input.DivisionId
						);

						await InsertProgressLogAsync(chapterId, input.DivisionId, previousProgress, input);
					}
					else
					{
						EnsureProgressUpdateAllowed(0, input.Progress, input.Description);

						// Create new progress record
						var newProgress = new Syllabus.ChapterProgress(
							guidGenerator.Create(),
							chapterId,
							input.DivisionId
						);
						newProgress.UpdateProgress(input.Progress, input.Description);
						await progressRepository.InsertAsync(newProgress);

						logger.LogDebug(
							"Created new progress for chapter {ChapterId}, division {DivisionId}",
							chapterId, input.DivisionId
						);

						await InsertProgressLogAsync(chapterId, input.DivisionId, 0, input);
					}

					break; // Success - exit retry loop
				}
				catch (Exception ex) when (
					attempt < maxRetries &&
					IsDuplicateKeyException(ex)
				)
				{
					// Concurrent insert detected - another request created the record
					// Retry by reloading the existing record and updating it
					logger.LogWarning(
						ex,
						"Concurrent insert detected for chapter {ChapterId}, division {DivisionId}. Retry attempt {Attempt}/{MaxRetries}",
						chapterId, input.DivisionId, attempt, maxRetries
					);

					// Brief delay to allow the other transaction to complete
					await Task.Delay(TimeSpan.FromMilliseconds(50 * attempt));

					// Continue to next iteration - will reload and update existing record
				}
			}

			return new ApiSuccessResponseDto
			{
				Success = true,
				Message = "Chapter progress updated successfully."
			};
		}
		catch (ArgumentOutOfRangeException ex)
		{
			throw new BusinessException("ClassManagement:InvalidProgress")
				.WithData("Message", ex.Message);
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(
				ex,
				"Failed to update chapter progress for chapter {ChapterId}, division {DivisionId}",
				chapterId, input.DivisionId
			);
			throw new UserFriendlyException(
				"Failed to update chapter progress. Please try again later."
			);
		}
	}

	private async Task EnsureCurrentUserCanAccessClassDivisionAsync(Guid standardId, Guid divisionId)
	{
		if (!CurrentUser.Id.HasValue)
		{
			throw new BusinessException(UserScopeErrorCodes.NoCurrentUser);
		}

		var scope = await userScopeService.GetUserScopeAsync(CurrentUser.Id.Value);
		if (CanAccessClassDivision(scope, standardId, divisionId))
		{
			return;
		}

		throw new BusinessException(UserScopeErrorCodes.AccessDenied);
	}

	private async Task<bool> HasAnyTeacherSubjectAccessAsync(Guid standardId, Guid divisionId)
	{
		return (await GetTeacherSubjectIdsAsync(standardId, divisionId)).Count > 0;
	}

	private async Task<bool> HasTeacherSubjectAccessAsync(Guid standardId, Guid subjectId, Guid divisionId)
	{
		var subjectIds = await GetTeacherSubjectIdsAsync(standardId, divisionId);
		return subjectIds.Contains(subjectId);
	}

	private async Task<HashSet<Guid>> GetTeacherSubjectIdsAsync(Guid standardId, Guid divisionId)
	{
		if (!CurrentUser.Id.HasValue)
		{
			return [];
		}

		var employee = await employeeRepository.FirstOrDefaultAsync(x => x.UserId == CurrentUser.Id.Value);
		var employeeIds = new HashSet<Guid>();
		employeeIds.Add(CurrentUser.Id.Value);
		if (employee != null)
		{
			employeeIds.Add(employee.Id);
		}
		employeeIds.UnionWith(await reportingManagerAccessService.GetReporteeEmployeeIdsAsync(CurrentUser.Id.Value));
		if (employeeIds.Count == 0)
		{
			return [];
		}

		var allocationQuery = await teacherSubjectAllocationRepository.GetQueryableAsync();
		var allocatedSubjectIds = (await AsyncExecuter.ToListAsync(allocationQuery
			.Where(x => x.StandardId == standardId &&
				(divisionId == Guid.Empty || x.DivisionId == divisionId) &&
				x.Teachers.Any(teacher => employeeIds.Contains(teacher.TeacherId)))
			.Select(x => x.SubjectId)
			.Distinct())).ToHashSet();

		var assignmentQuery = await subjectAssignmentRepository.GetQueryableAsync();
		var directlyAssignedSubjectIds = await AsyncExecuter.ToListAsync(assignmentQuery
			.Where(x => x.StandardId == standardId &&
				x.Teachers.Any(teacher => employeeIds.Contains(teacher.TeacherId)))
			.Select(x => x.SubjectId)
			.Distinct());
		allocatedSubjectIds.UnionWith(directlyAssignedSubjectIds);
		return allocatedSubjectIds;
	}

	private async Task EnsureCurrentUserCanAccessStandardAsync(Guid standardId)
	{
		if (!CurrentUser.Id.HasValue)
		{
			throw new BusinessException(UserScopeErrorCodes.NoCurrentUser);
		}

		var scope = await userScopeService.GetUserScopeAsync(CurrentUser.Id.Value);
		if (scope.IsAdmin ||
			scope.HasTenantWideScope() ||
			scope.StandardIds.Contains(standardId) ||
			scope.WildcardDivisionStandardIds.Contains(standardId))
		{
			return;
		}

		var divisions = await divisionRepository.GetListAsync(
			division => division.StandardId == standardId);
		if (divisions.Any(division => scope.DivisionIds.Contains(division.Id)))
		{
			return;
		}

		throw new BusinessException(UserScopeErrorCodes.AccessDenied);
	}

	private static bool CanAccessClassDivision(UserScopeContext scope, Guid standardId, Guid divisionId)
	{
		return scope.IsAdmin ||
			scope.HasTenantWideScope() ||
			scope.DivisionIds.Contains(divisionId) ||
			scope.StandardIds.Contains(standardId) ||
			scope.WildcardDivisionStandardIds.Contains(standardId);
	}

	/// <summary>
	/// Determines if the exception is a duplicate key/unique constraint violation.
	/// Handles database-specific exception types using type checking, error codes, and message patterns.
	/// </summary>
	private static void EnsureProgressUpdateAllowed(int currentProgress, int requestedProgress, string? description)
	{
		if (requestedProgress == currentProgress)
		{
			throw new BusinessException("ClassManagement:ProgressNoChange")
				.WithData("CurrentProgress", currentProgress)
				.WithData("RequestedProgress", requestedProgress);
		}

		if (requestedProgress < currentProgress && string.IsNullOrWhiteSpace(description))
		{
			throw new BusinessException("ClassManagement:ProgressDecreaseReasonRequired")
				.WithData("CurrentProgress", currentProgress)
				.WithData("RequestedProgress", requestedProgress);
		}

		if (requestedProgress == 100 && string.IsNullOrWhiteSpace(description))
		{
			throw new BusinessException("ClassManagement:CompletionEvidenceRequired")
				.WithData("RequestedProgress", requestedProgress);
		}

		if (string.IsNullOrWhiteSpace(description))
		{
			throw new BusinessException("ClassManagement:ProgressReasonRequired")
				.WithData("CurrentProgress", currentProgress)
				.WithData("RequestedProgress", requestedProgress);
		}
	}

	private async Task InsertProgressLogAsync(
		Guid chapterId,
		Guid divisionId,
		int previousProgress,
		UpdateChapterProgressDto input)
	{
		var log = new ChapterProgressLog(
			guidGenerator.Create(),
			chapterId,
			divisionId,
			input.Progress,
			BuildProgressLogActorSummary(previousProgress, input)
		);
		await progressLogRepository.InsertAsync(log);
	}

	private string BuildProgressLogActorSummary(int previousProgress, UpdateChapterProgressDto input)
	{
		var displayName = GetCurrentUserDisplayName();
		var fields = new List<string>
		{
			displayName,
			$"Previous: {previousProgress}",
			$"New: {input.Progress}",
			$"Reason: {NormalizeLogValue(input.Description)}"
		};

		AddLogField(fields, "Status", input.Status);
		AddLogField(fields, "Delay", input.DelayReason);
		AddLogField(fields, "Delay Detail", input.DelayDescription);
		AddLogField(fields, "Revised", input.RevisedEndDate?.ToString("yyyy-MM-dd"));
		AddLogField(fields, "Lectures", input.ConductedLectures?.ToString());
		AddLogField(fields, "Topic", input.CurrentTopic);
		AddLogField(fields, "Method", input.TeachingMethod);
		AddLogField(fields, "Understanding", input.StudentUnderstanding);
		AddLogField(fields, "Escalation", input.EscalationRequired?.ToString());

		return string.Join(" | ", fields);
	}

	private static void AddLogField(List<string> fields, string label, string? value)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			fields.Add($"{label}: {NormalizeLogValue(value)}");
		}
	}

	private static string NormalizeLogValue(string? value)
	{
		return string.Join(' ', (value ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
	}

	private static int CalculateExpectedProgress(DateTime? plannedStartDate, DateTime? plannedEndDate)
	{
		if (!plannedStartDate.HasValue || !plannedEndDate.HasValue)
		{
			return 0;
		}

		var start = plannedStartDate.Value.Date;
		var end = plannedEndDate.Value.Date;
		var today = DateTime.UtcNow.Date;

		if (end < start)
		{
			return 0;
		}

		if (today < start)
		{
			return 0;
		}

		if (today >= end)
		{
			return 100;
		}

		var totalWorkingDays = CountWorkingDays(start, end);
		var elapsedWorkingDays = CountWorkingDays(start, today);

		if (totalWorkingDays <= 0)
		{
			return 0;
		}

		return Math.Clamp((int) Math.Round(elapsedWorkingDays * 100d / totalWorkingDays), 0, 100);
	}

	private static int CountWorkingDays(DateTime startDate, DateTime endDate)
	{
		if (endDate < startDate)
		{
			return 0;
		}

		var count = 0;
		for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
		{
			if (date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
			{
				count++;
			}
		}

		return count;
	}

	private static string CalculateChapterStatus(int progress, DateTime? plannedStartDate, DateTime? plannedEndDate)
	{
		var today = DateTime.UtcNow.Date;

		if (progress >= 100)
		{
			return "Completed";
		}

		if (plannedEndDate.HasValue && plannedEndDate.Value.Date < today)
		{
			return "Delayed";
		}

		if (progress > 0)
		{
			return "In Progress";
		}

		if (plannedStartDate.HasValue && plannedStartDate.Value.Date <= today)
		{
			return "Due To Start";
		}

		return "Not Started";
	}

	private static string CalculateScheduleStatus(int progress, int expectedProgress, bool isCompleted)
	{
		if (isCompleted)
		{
			return "Completed";
		}

		var variance = progress - expectedProgress;
		return variance switch
		{
			>= 0 => "On Track",
			>= -10 => "Slightly Behind",
			>= -25 => "Delayed",
			_ => "Critically Delayed"
		};
	}

	private static string CalculateRiskLevel(int progress, int expectedProgress, DateTime? lastUpdatedAt)
	{
		if (progress >= 100)
		{
			return "Low";
		}

		var variance = progress - expectedProgress;
		if (variance <= -30)
		{
			return "Critical";
		}

		if (variance <= -15)
		{
			return "High";
		}

		if (variance < 0)
		{
			return "Medium";
		}

		if (lastUpdatedAt.HasValue && lastUpdatedAt.Value.Date <= DateTime.UtcNow.Date.AddDays(-14))
		{
			return "Medium";
		}

		return "Low";
	}

	private static int GetRiskRank(string riskLevel)
	{
		return riskLevel switch
		{
			"Critical" => 4,
			"High" => 3,
			"Medium" => 2,
			"Low" => 1,
			_ => 0
		};
	}

	private static string GetRiskLevelFromRank(int rank)
	{
		return rank switch
		{
			>= 4 => "Critical",
			3 => "High",
			2 => "Medium",
			1 => "Low",
			_ => "Low"
		};
	}

	private static ChapterProgressLogDto MapProgressLog(ChapterProgressLog log)
	{
		var fields = ParseProgressLogFields(log.ChangedByName);
		var changedByName = fields.TryGetValue("actor", out var actor) ? actor : log.ChangedByName;
		var previousProgress = TryParseInt(fields, "Previous");
		var newProgress = TryParseInt(fields, "New") ?? log.Progress;

		if (!previousProgress.HasValue && fields.TryGetValue("legacyChange", out var legacyChange))
		{
			var parts = legacyChange.Split("->", StringSplitOptions.TrimEntries);
			if (parts.Length == 2)
			{
				previousProgress = ParsePercentage(parts[0]);
				newProgress = ParsePercentage(parts[1]) ?? log.Progress;
			}
		}

		return new ChapterProgressLogDto
		{
			ChangedByName = changedByName,
			Progress = newProgress,
			PreviousProgress = previousProgress,
			ChangeAmount = previousProgress.HasValue ? newProgress - previousProgress.Value : 0,
			Description = fields.TryGetValue("Reason", out var reason) ? reason : null,
			Status = fields.TryGetValue("Status", out var status) ? status : null,
			DelayReason = fields.TryGetValue("Delay", out var delay) ? delay : null,
			RevisedEndDate = fields.TryGetValue("Revised", out var revised) && DateTime.TryParse(revised, out var revisedEndDate)
				? revisedEndDate
				: null,
			ChangedAt = log.CreationTime
		};
	}

	private static Dictionary<string, string> ParseProgressLogFields(string? summary)
	{
		var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		if (string.IsNullOrWhiteSpace(summary))
		{
			return result;
		}

		var parts = summary.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length == 0)
		{
			return result;
		}

		result["actor"] = parts[0];

		foreach (var part in parts.Skip(1))
		{
			var separatorIndex = part.IndexOf(':');
			if (separatorIndex > 0)
			{
				result[part[..separatorIndex].Trim()] = part[(separatorIndex + 1)..].Trim();
				continue;
			}

			if (part.Contains("->", StringComparison.OrdinalIgnoreCase))
			{
				result["legacyChange"] = part;
			}
		}

		return result;
	}

	private static int? TryParseInt(IReadOnlyDictionary<string, string> fields, string key)
	{
		return fields.TryGetValue(key, out var value) && int.TryParse(value, out var parsed)
			? parsed
			: null;
	}

	private static int? ParsePercentage(string value)
	{
		var normalized = value.Replace("%", string.Empty).Trim();
		return int.TryParse(normalized, out var parsed) ? parsed : null;
	}

	private string GetCurrentUserDisplayName()
	{
		if (!string.IsNullOrWhiteSpace(CurrentUser.Name))
		{
			return $"{CurrentUser.Name} {CurrentUser.SurName}".Trim();
		}

		return CurrentUser.UserName ?? "Unknown";
	}

	private bool IsDuplicateKeyException(Exception ex)
	{
		if (ex == null)
		{
			return false;
		}

		// Check exception type names for database-specific types
		var exceptionTypeName = ex.GetType().Name;
		var innerExceptionTypeName = ex.InnerException?.GetType().Name ?? string.Empty;

		// Common DB exception types that indicate constraint violations
		if (exceptionTypeName.Contains("DbUpdate", StringComparison.OrdinalIgnoreCase) ||
			innerExceptionTypeName.Contains("PostgresException", StringComparison.OrdinalIgnoreCase) ||
			innerExceptionTypeName.Contains("SqlException", StringComparison.OrdinalIgnoreCase) ||
			innerExceptionTypeName.Contains("MySqlException", StringComparison.OrdinalIgnoreCase))
		{
			// Check for PostgreSQL-specific SqlState "23505" (unique violation)
			if (ex.InnerException != null)
			{
				var innerEx = ex.InnerException;

				// Try to get SqlState property using reflection
				var sqlStateProperty = innerEx.GetType().GetProperty("SqlState");
				if (sqlStateProperty != null)
				{
					var sqlState = sqlStateProperty.GetValue(innerEx)?.ToString();
					if (sqlState == "23505") // PostgreSQL unique violation
					{
						return true;
					}
				}

				// Try to get ConstraintName property using reflection
				var constraintNameProperty = innerEx.GetType().GetProperty("ConstraintName");
				if (constraintNameProperty != null)
				{
					var constraintName = constraintNameProperty.GetValue(innerEx)?.ToString();
					if (!string.IsNullOrEmpty(constraintName))
					{
						return true;
					}
				}

				// Check SQL Server error number (2627 = unique constraint, 2601 = unique index)
				var numberProperty = innerEx.GetType().GetProperty("Number");
				if (numberProperty != null)
				{
					var errorNumber = numberProperty.GetValue(innerEx);
					if (errorNumber is int number && (number == 2627 || number == 2601))
					{
						return true;
					}
				}
			}
		}

		// Fallback: Check exception messages using culture-invariant comparison
		var message = ex.Message ?? string.Empty;
		var innerMessage = ex.InnerException?.Message ?? string.Empty;

		return message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
			   message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) ||
			   message.Contains("unique index", StringComparison.OrdinalIgnoreCase) ||
			   message.Contains("cannot insert duplicate", StringComparison.OrdinalIgnoreCase) ||
			   message.Contains("violation of unique key", StringComparison.OrdinalIgnoreCase) ||
			   innerMessage.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
			   innerMessage.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) ||
			   innerMessage.Contains("unique index", StringComparison.OrdinalIgnoreCase);
	}

	public async Task<List<ChapterProgressLogDto>> GetChapterProgressLogsAsync(Guid chapterId, Guid divisionId)
	{
		var chapter = await chapterRepository.FirstOrDefaultAsync(candidate => candidate.Id == chapterId);
		if (chapter == null)
		{
			throw new BusinessException("ClassManagement:ChapterNotFound")
				.WithData("ChapterId", chapterId);
		}

		var assignment = await subjectAssignmentRepository.FirstOrDefaultAsync(
			candidate => candidate.Id == chapter.AssignedSubjectId);
		if (assignment == null)
		{
			throw new BusinessException("ClassManagement:ChapterNotFound")
				.WithData("ChapterId", chapterId);
		}

		// Progress logs belong to the chapter's subject. A teacher with subject
		// access must be able to read them even when no separate class/division
		// scope entry exists.
		if (!await HasTeacherSubjectAccessAsync(assignment.StandardId, assignment.SubjectId, divisionId))
		{
			await EnsureCurrentUserCanAccessClassDivisionAsync(assignment.StandardId, divisionId);
		}

		logger.LogInformation(
			"Getting progress logs for chapter {ChapterId}, division {DivisionId}",
			chapterId, divisionId
		);

		var logs = await progressLogRepository.GetListAsync(l =>
			l.ChapterId == chapterId &&
			l.DivisionId == divisionId
		);

		return logs
			.OrderByDescending(l => l.CreationTime)
			.Select(MapProgressLog)
			.ToList();
	}

	public async Task<List<SubjectForChaptersDto>> GetSubjectsForStandardAsync(Guid standardId)
	{
		var hasSubjectAccess = await HasAnyTeacherSubjectAccessAsync(standardId, Guid.Empty);
		if (!hasSubjectAccess)
		{
			await EnsureCurrentUserCanAccessStandardAsync(standardId);
		}

		logger.LogInformation("Getting subjects for standard {StandardId}", standardId);

		var assignments = await subjectAssignmentRepository.GetListAsync(a => a.StandardId == standardId);
		if (hasSubjectAccess)
		{
			var accessibleSubjectIds = await GetTeacherSubjectIdsAsync(standardId, Guid.Empty);
			assignments = assignments.Where(a => accessibleSubjectIds.Contains(a.SubjectId)).ToList();
		}

		if (!assignments.Any())
		{
			return new List<SubjectForChaptersDto>();
		}

		var subjectIds = assignments.Select(a => a.SubjectId).Distinct().ToList();
		var subjects = await subjectRepository.GetListAsync(s => subjectIds.Contains(s.Id));
		var subjectLookup = subjects.ToDictionary(s => s.Id);

		return assignments
			.Where(a => subjectLookup.ContainsKey(a.SubjectId))
			.Select(a => new SubjectForChaptersDto
			{
				SubjectId = a.SubjectId,
				SubjectAssignmentId = a.Id,
				SubjectName = subjectLookup[a.SubjectId].Name,
				SubjectCode = a.SubjectCode ?? string.Empty
			})
			.ToList();
	}

	public async Task<List<EmployeeClassDto>> GetClassesByEmployeeAsync(Guid employeeId)
	{
		if (employeeId == Guid.Empty)
		{
			throw new ArgumentException("Employee ID cannot be empty", nameof(employeeId));
		}

		logger.LogInformation("Getting classes for employee {EmployeeId}", employeeId);

		var allocationQuery = await teacherSubjectAllocationRepository.GetQueryableAsync();
		var standardIds = await AsyncExecuter.ToListAsync(
			allocationQuery
				.Where(a => a.Teachers.Any(t => t.TeacherId == employeeId))
				.Select(a => a.StandardId)
				.Distinct()
		);

		if (!standardIds.Any())
		{
			return new List<EmployeeClassDto>();
		}

		var standards = await standardRepository.GetListAsync(s => standardIds.Contains(s.Id));

		return standards
			.OrderBy(s => s.Name)
			.Select(s => new EmployeeClassDto { Id = s.Id, Name = s.Name })
			.ToList();
	}

	public async Task<List<EmployeeDivisionDto>> GetDivisionsByEmployeeAndClassAsync(Guid employeeId, Guid classId)
	{
		if (employeeId == Guid.Empty)
		{
			throw new ArgumentException("Employee ID cannot be empty", nameof(employeeId));
		}

		if (classId == Guid.Empty)
		{
			throw new ArgumentException("Class ID cannot be empty", nameof(classId));
		}

		logger.LogInformation(
			"Getting divisions for employee {EmployeeId} and class {ClassId}",
			employeeId, classId
		);

		var allocationQuery = await teacherSubjectAllocationRepository.GetQueryableAsync();
		var divisionIds = await AsyncExecuter.ToListAsync(
			allocationQuery
				.Where(a => a.StandardId == classId && a.Teachers.Any(t => t.TeacherId == employeeId))
				.Select(a => a.DivisionId)
				.Distinct()
		);

		if (!divisionIds.Any())
		{
			return new List<EmployeeDivisionDto>();
		}

		var divisions = await divisionRepository.GetListAsync(d => divisionIds.Contains(d.Id));

		return divisions
			.OrderBy(d => d.Name)
			.Select(d => new EmployeeDivisionDto { Id = d.Id, Name = d.Name })
			.ToList();
	}

	public async Task<List<ClassDivisionSubjectDto>> GetSubjectsProgressByEmployeeAsync(
		Guid employeeId,
		Guid classId,
		Guid divisionId
	)
	{
		if (employeeId == Guid.Empty)
		{
			throw new ArgumentException("Employee ID cannot be empty", nameof(employeeId));
		}

		if (classId == Guid.Empty)
		{
			throw new ArgumentException("Class ID cannot be empty", nameof(classId));
		}

		if (divisionId == Guid.Empty)
		{
			throw new ArgumentException("Division ID cannot be empty", nameof(divisionId));
		}

		if (!await HasAnyTeacherSubjectAccessAsync(classId, divisionId))
		{
			await EnsureCurrentUserCanAccessClassDivisionAsync(classId, divisionId);
		}

		logger.LogInformation(
			"Getting subject progress for employee {EmployeeId}, class {ClassId}, division {DivisionId}",
			employeeId, classId, divisionId
		);

		// Find the subject IDs this employee is allocated to for this class+division
		var allocationQuery = await teacherSubjectAllocationRepository.GetQueryableAsync();
		var allocatedSubjectIds = await AsyncExecuter.ToListAsync(
			allocationQuery
				.Where(a =>
					a.StandardId == classId &&
					a.DivisionId == divisionId &&
					a.Teachers.Any(t => t.TeacherId == employeeId))
				.Select(a => a.SubjectId)
				.Distinct()
		);

		if (!allocatedSubjectIds.Any())
		{
			return new List<ClassDivisionSubjectDto>();
		}

		// Get subject assignments for this class, filtered to employee's subjects only
		var assignments = await subjectAssignmentRepository.GetListAsync(a =>
			a.StandardId == classId && allocatedSubjectIds.Contains(a.SubjectId)
		);

		var result = new List<ClassDivisionSubjectDto>();
		if (!assignments.Any())
		{
			return result;
		}

		var subjectIds = assignments.Select(a => a.SubjectId).Distinct().ToList();
		var subjects = await subjectRepository.GetListAsync(s => subjectIds.Contains(s.Id));
		var subjectLookup = subjects.ToDictionary(s => s.Id);

		var assignmentIds = assignments.Select(a => a.Id).Distinct().ToList();
		var chapters = await chapterRepository.GetListAsync(c => assignmentIds.Contains(c.AssignedSubjectId));
		var chaptersByAssignment = chapters
			.GroupBy(c => c.AssignedSubjectId)
			.ToDictionary(g => g.Key, g => g.ToList());
		var chapterIds = chapters.Select(c => c.Id).ToList();
		var progresses = chapterIds.Count == 0
			? new List<Syllabus.ChapterProgress>()
			: await progressRepository.GetListAsync(p =>
				chapterIds.Contains(p.ChapterId) &&
				p.DivisionId == divisionId
			);
		var progressLookup = progresses
			.GroupBy(p => p.ChapterId)
			.ToDictionary(
				g => g.Key,
				g => g.OrderByDescending(p => p.CreationTime).First()
			);

		foreach (var assignment in assignments)
		{
			if (!subjectLookup.TryGetValue(assignment.SubjectId, out var subject))
			{
				continue;
			}

			if (!chaptersByAssignment.TryGetValue(assignment.Id, out var subjectChapters))
			{
				subjectChapters = new List<AssignedSubjectChapter>();
			}

			var totalChapters = subjectChapters.Count;
			var completedChapters = 0;
			var progressSum = 0;
			var expectedProgressSum = 0;
			DateTime? lastUpdatedAt = null;
			var subjectRiskRank = 0;

			foreach (var chapter in subjectChapters)
			{
				progressLookup.TryGetValue(chapter.Id, out var progress);

				var expectedProgress = CalculateExpectedProgress(
					chapter.ChapterStartTime == default ? null : chapter.ChapterStartTime.UtcDateTime,
					chapter.ChapterEndTime == default ? null : chapter.ChapterEndTime.UtcDateTime
				);
				var currentProgress = progress?.Progress ?? 0;
				var riskLevel = CalculateRiskLevel(currentProgress, expectedProgress, progress?.LastModificationTime ?? progress?.CreationTime);

				expectedProgressSum += expectedProgress;
				progressSum += currentProgress;
				subjectRiskRank = Math.Max(subjectRiskRank, GetRiskRank(riskLevel));

				var progressUpdatedAt = progress?.LastModificationTime ?? progress?.CreationTime;
				if (progressUpdatedAt.HasValue && (!lastUpdatedAt.HasValue || progressUpdatedAt.Value > lastUpdatedAt.Value))
				{
					lastUpdatedAt = progressUpdatedAt;
				}

				if (currentProgress == 100)
				{
					completedChapters++;
				}
			}

			var progressPercentage = totalChapters > 0
				? (int) Math.Round(progressSum / (double) totalChapters)
				: 0;
			var expectedProgressPercentage = totalChapters > 0
				? (int) Math.Round(expectedProgressSum / (double) totalChapters)
				: 0;
			var variancePercentage = progressPercentage - expectedProgressPercentage;
			var scheduleStatus = CalculateScheduleStatus(progressPercentage, expectedProgressPercentage, completedChapters == totalChapters && totalChapters > 0);

			result.Add(new ClassDivisionSubjectDto
			{
				SubjectId = subject.Id,
				SubjectName = subject.Name,
				SubjectCode = assignment.SubjectCode,
				SubjectColor = subject.Color,
				TotalChapters = totalChapters,
				CompletedChapters = completedChapters,
				ProgressPercentage = progressPercentage,
				ExpectedProgressPercentage = expectedProgressPercentage,
				VariancePercentage = variancePercentage,
				ScheduleStatus = scheduleStatus,
				RiskLevel = GetRiskLevelFromRank(subjectRiskRank),
				LastUpdatedAt = lastUpdatedAt
			});
		}

		return result;
	}
}
