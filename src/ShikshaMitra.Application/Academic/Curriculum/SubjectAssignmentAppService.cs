using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.HomeworkAssignment;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Application service for Subject Assignment operations
/// </summary>
[RemoteService(IsEnabled = false)]
public class SubjectAssignmentAppService(
	IRepository<AssignedSubject, Guid> assignmentRepository,
	IRepository<Subject, Guid> subjectRepository,
	IRepository<ElectiveGroup, Guid> electiveGroupRepository,
	IGuidGenerator guidGenerator,
	IIdentityUserRepository userRepository,
	ILogger<SubjectAssignmentAppService> logger,
	IWeeklyLectureSlotRepository weeklyLectureSlotRepository,
	ITimetableRepository timetableRepository,
	IUserScopeAccessService userScopeAccessService,
	IRepository<Exam, Guid> examRepository,
	IRepository<HomeworkAssignmentEntity, Guid> homeworkRepository,
	IRepository<AssignmentEntity, Guid> learningTaskAssignmentRepository,
	IRepository<LessonPlan, Guid> lessonPlanRepository,
	IRepository<TeacherDiary, Guid> teacherDiaryRepository
) : ApplicationService, ISubjectAssignmentAppService
{
	public async Task CreateAsync(
		Guid academicMediumId,
		CreateUpdateSubjectAssignmentDto createSubjectAssignmentDto
	)
	{
		try
		{
			EnsureSubjectAssignmentInputIsConsistent(createSubjectAssignmentDto);
			await EnsureSubjectsAreNotAlreadyAssignedAsync(
				createSubjectAssignmentDto.ClassId,
				createSubjectAssignmentDto.Subjects.Select(s => s.SubjectId),
				Array.Empty<Guid>());

			var assignedSubjects = new List<AssignedSubject>();
			foreach (var subject in createSubjectAssignmentDto.Subjects)
			{
				var assignedSubject =
					new AssignedSubject(
						guidGenerator.Create(),
						CurrentTenant.Id,
						createSubjectAssignmentDto.ClassId,
						subject.SubjectId,
						subject.SubjectCode,
						createSubjectAssignmentDto.Status,
						(subject.ElectiveGroupId == Guid.Empty || subject.ElectiveGroupId == null) ? SubjectType.Core : SubjectType.Elective,
						subject.ElectiveGroupId,
						subject.LearningGoal,
						subject.Prerequisites,
						subject.Description,
						subject.WeeklyPeriods,
						subject.DifficultyLevel,
						subject.SubjectGroup,
						subject.IsAiSuggested,
						subject.IsCopiedFromPreviousYear
					);

				if (subject.PrimaryTeacherId != null)
				{
					assignedSubject.SetPrimaryTeacher(subject.PrimaryTeacherId.Value);
				}

				if (subject.SecondaryTeacherIds != null)
				{
					assignedSubject.SetSecondaryTeachers(subject.SecondaryTeacherIds);
				}

				// Set documents
				if (subject.Documents != null && subject.Documents.Any())
				{
					foreach (var document in subject.Documents)
					{
						assignedSubject.AddDocument(
								guidGenerator.Create(),
								document
						);
					}
				}

				assignedSubject.RecalculateSetupHealth();
				assignedSubjects.Add(assignedSubject);
			}

			await assignmentRepository.InsertManyAsync(assignedSubjects, autoSave: true);

			// Auto-add new subjects to the weekly lecture slot for this class (with 0 weekly lectures).
			// If no slot exists yet, create one first.
			var weeklySlot = await weeklyLectureSlotRepository.FindByStandardIdAsync(
				createSubjectAssignmentDto.ClassId
			);
			if (weeklySlot == null)
			{
				weeklySlot = new WeeklyLectureSlot(
					guidGenerator.Create(),
					CurrentTenant.Id,
					createSubjectAssignmentDto.ClassId
				);
				weeklySlot = await weeklyLectureSlotRepository.InsertAsync(weeklySlot, autoSave: true);
			}

			var existingSubjectIds = weeklySlot.Subjects
				.Where(s => s.SubjectId.HasValue)
				.Select(s => s.SubjectId!.Value)
				.ToHashSet();
			var existingElectiveIds = weeklySlot.Subjects
				.Where(s => s.ElectiveGroupId.HasValue)
				.Select(s => s.ElectiveGroupId!.Value)
				.ToHashSet();

			var addedElectiveGroups = new HashSet<Guid>();
			var anyAdded = false;

			foreach (var assigned in assignedSubjects)
			{
				if (assigned.SubjectType == SubjectType.Elective && assigned.ElectiveGroupId.HasValue)
				{
					var egId = assigned.ElectiveGroupId.Value;
					if (!existingElectiveIds.Contains(egId) && addedElectiveGroups.Add(egId))
					{
						weeklySlot.AddSubject(guidGenerator.Create(), null, egId, 0);
						existingElectiveIds.Add(egId);
						anyAdded = true;
					}
				}
				else
				{
					if (!existingSubjectIds.Contains(assigned.SubjectId))
					{
						weeklySlot.AddSubject(guidGenerator.Create(), assigned.SubjectId, null, 0);
						existingSubjectIds.Add(assigned.SubjectId);
						anyAdded = true;
					}
				}
			}

			if (anyAdded)
				await weeklyLectureSlotRepository.UpdateAsync(weeklySlot, autoSave: true);
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to create subject assignment");
			throw new UserFriendlyException(
				"Failed to create subject assignment. Please try again later."
			);
		}
	}

	public async Task<SubjectAssignmentPagedResponse> GetListAsync(
		Guid classId,
		int page,
		int pageSize,
		SubjectStatus? status = null,
		bool includeArchived = false,
		string? teacherSearch = null
	)
	{
		var query = await assignmentRepository.WithDetailsAsync(a => a.Chapters, a => a.Teachers);

		query = query.Where(a => a.StandardId == classId);
		if (status.HasValue)
		{
			query = query.Where(a => a.Status == status.Value);
		}
		else if (!includeArchived)
		{
			query = query.Where(a => a.Status == SubjectStatus.Published);
		}

		// Non-admin users only see subjects they are personally assigned to teach.
		if (!await userScopeAccessService.CurrentUserHasTenantWideScopeAsync())
		{
			var userId = CurrentUser.Id!.Value;
			query = query.Where(a => a.Teachers.Any(t => t.TeacherId == userId));
		}

		var assignments = await AsyncExecuter.ToListAsync(query);
		var readMetrics = await BuildSubjectAssignmentReadMetricsAsync(assignments, classId);

		// Group assignments
		var groupedAssignments = new List<SubjectAssignmentGroupDto>();

		var groupedByElective = assignments
			.Where(a => a.SubjectType == SubjectType.Elective && a.ElectiveGroupId.HasValue)
			.GroupBy(a => a.ElectiveGroupId)
			.ToList();

		var coreSubjects = assignments
			.Where(a => a.SubjectType == SubjectType.Core || (a.SubjectType == SubjectType.Elective && !a.ElectiveGroupId.HasValue))
			.ToList();



		// I need to fetch all subjects for these assignments to get Name and Color.
		var subjectIds = assignments.Select(x => x.SubjectId).Distinct().ToList();
		var subjectsDict = (await subjectRepository.GetListAsync(s => subjectIds.Contains(s.Id)))
						   .ToDictionary(s => s.Id);

		var electiveGroupIds = assignments.Where(x => x.ElectiveGroupId.HasValue).Select(x => x.ElectiveGroupId.Value).Distinct().ToList();
		var electiveGroupsDict = (await electiveGroupRepository.GetListAsync(g => electiveGroupIds.Contains(g.Id)))
								 .ToDictionary(g => g.Id);

		// Fetch weekly lecture slot for this class to compute total/occupied/unoccupied slots
		var weeklySlot = await weeklyLectureSlotRepository.FindByStandardIdAsync(classId);
		// subjectId -> allocated weekly lectures
		var weeklyLecturesBySubject = weeklySlot?.Subjects
			.Where(s => s.SubjectId.HasValue)
			.ToDictionary(s => s.SubjectId!.Value, s => s.WeeklyLectures)
			?? new Dictionary<Guid, int>();
		var weeklyLecturesByElective = weeklySlot?.Subjects
			.Where(s => s.ElectiveGroupId.HasValue)
			.ToDictionary(s => s.ElectiveGroupId!.Value, s => s.WeeklyLectures)
			?? new Dictionary<Guid, int>();

		// Count already-placed timetable slots per subject for this class
		var timetables = await timetableRepository.GetByStandardIdAsync(classId);
		var occupiedBySubject = timetables
			.SelectMany(t => t.Slots)
			.SelectMany(s => s.Subjects)
			.GroupBy(ss => ss.SubjectId)
			.ToDictionary(g => g.Key, g => g.Count());

		// Bulk-load teachers for primary teacher display in list
		var allAssignmentTeacherIds = assignments
			.SelectMany(a => a.Teachers)
			.Where(t => t.TeacherType == SubjectTeacherType.Primary)
			.Select(t => t.TeacherId)
			.Distinct()
			.ToList();
		var teacherNamesDict = allAssignmentTeacherIds.Count > 0
			? (await userRepository.GetListByIdsAsync(allAssignmentTeacherIds))
				.ToDictionary(u => u.Id, u => $"{u.Name} {u.Surname}".Trim())
			: new Dictionary<Guid, string>();

		foreach (var coreAssignment in coreSubjects)
		{
			subjectsDict.TryGetValue(coreAssignment.SubjectId, out var subject);
			var metrics = GetReadMetrics(readMetrics, coreAssignment);
			var totalSlots = weeklyLecturesBySubject.GetValueOrDefault(coreAssignment.SubjectId, 0);
			var occupiedSlots = occupiedBySubject.GetValueOrDefault(coreAssignment.SubjectId, 0);
			var primaryTeacherEntries = coreAssignment.Teachers
				.Where(t => t.TeacherType == SubjectTeacherType.Primary)
				.ToList();
			var primaryTeacherNames = primaryTeacherEntries
				.Select(t => teacherNamesDict.GetValueOrDefault(t.TeacherId))
				.Where(name => !string.IsNullOrWhiteSpace(name))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();
			var primaryTeacherEntry = primaryTeacherEntries.FirstOrDefault();
			var usageParts = await GetAssignmentUsagePartsAsync(
				coreAssignment.StandardId,
				new[] { coreAssignment.SubjectId },
				coreAssignment.ElectiveGroupId,
				coreAssignment.Chapters.Count);

			groupedAssignments.Add(new SubjectAssignmentGroupDto
			{
				Id = coreAssignment.Id,
				SubjectType = coreAssignment.SubjectType,
				Status = coreAssignment.Status,
				Color = subject?.Color,
				TotalSlots = totalSlots,
				OccupiedSlots = occupiedSlots,
				UnoccupiedSlots = totalSlots - occupiedSlots,
				SetupHealthScore = metrics.SetupHealthScore,
				ChaptersCount = coreAssignment.Chapters.Count,
				WeeklyPeriods = metrics.WeeklyPeriods,
				PrimaryTeacherId = primaryTeacherEntry?.TeacherId.ToString(),
				PrimaryTeacherName = primaryTeacherNames.FirstOrDefault(),
				PrimaryTeacherNames = primaryTeacherNames,
				LastUpdatedAt = coreAssignment.LastModificationTime ?? coreAssignment.CreationTime,
				IsAiSuggested = coreAssignment.IsAiSuggested,
				IsCopiedFromPreviousYear = coreAssignment.IsCopiedFromPreviousYear,
				CanDelete = usageParts.Count == 0,
				DeleteDisabledReason = usageParts.Count == 0
					? null
					: $"This subject is already used in {string.Join(", ", usageParts)}.",
				Subjects = new List<SubjectLiteDto>
				{
					new SubjectLiteDto
					{
						SubjectType = coreAssignment.SubjectType,
						SubjectId = coreAssignment.SubjectId,
						SubjectName = subject?.Name,
						SubjectCode = coreAssignment.SubjectCode,
						Color = subject?.Color
					}
				}
			});
		}

		foreach (var group in groupedByElective)
		{
			var first = group.First();
			electiveGroupsDict.TryGetValue(first.ElectiveGroupId.Value, out var electiveGroup);
			var groupMetrics = group.Select(a => GetReadMetrics(readMetrics, a)).ToList();
			var electiveTotalSlots = weeklyLecturesByElective.GetValueOrDefault(first.ElectiveGroupId.Value, 0);
			var electiveOccupiedSlots = group.Sum(a => occupiedBySubject.GetValueOrDefault(a.SubjectId, 0));
			var avgHealth = groupMetrics.Count > 0 ? (int) groupMetrics.Average(a => a.SetupHealthScore) : 0;
			var primaryTeacherEntries = group
				.SelectMany(a => a.Teachers)
				.Where(t => t.TeacherType == SubjectTeacherType.Primary)
				.ToList();
			var primaryTeacherNames = primaryTeacherEntries
				.Select(t => teacherNamesDict.GetValueOrDefault(t.TeacherId))
				.Where(name => !string.IsNullOrWhiteSpace(name))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();
			var firstPrimary = primaryTeacherEntries.FirstOrDefault();
			var subjectIdsInGroup = group.Select(a => a.SubjectId).ToList();
			var usageParts = await GetAssignmentUsagePartsAsync(
				first.StandardId,
				subjectIdsInGroup,
				first.ElectiveGroupId,
				group.Sum(a => a.Chapters.Count));

			groupedAssignments.Add(new SubjectAssignmentGroupDto
			{
				Id = first.Id,
				SubjectType = SubjectType.Elective,
				Status = first.Status,
				ElectiveGroupId = first.ElectiveGroupId,
				ElectiveGroupName = electiveGroup?.Name,
				Color = electiveGroup?.Color,
				TotalSlots = electiveTotalSlots,
				OccupiedSlots = electiveOccupiedSlots,
				UnoccupiedSlots = electiveTotalSlots - electiveOccupiedSlots,
				SetupHealthScore = avgHealth,
				ChaptersCount = group.Sum(a => a.Chapters.Count),
				WeeklyPeriods = groupMetrics.FirstOrDefault()?.WeeklyPeriods ?? first.WeeklyPeriods,
				PrimaryTeacherId = firstPrimary?.TeacherId.ToString(),
				PrimaryTeacherName = primaryTeacherNames.FirstOrDefault(),
				PrimaryTeacherNames = primaryTeacherNames,
				LastUpdatedAt = first.LastModificationTime ?? first.CreationTime,
				IsAiSuggested = first.IsAiSuggested,
				IsCopiedFromPreviousYear = first.IsCopiedFromPreviousYear,
				CanDelete = usageParts.Count == 0,
				DeleteDisabledReason = usageParts.Count == 0
					? null
					: $"This subject group is already used in {string.Join(", ", usageParts)}.",
				Subjects = group.Select(a =>
				{
					subjectsDict.TryGetValue(a.SubjectId, out var subj);
					return new SubjectLiteDto
					{
						SubjectType = a.SubjectType,
						SubjectId = a.SubjectId,
						SubjectName = subj?.Name,
						SubjectCode = a.SubjectCode,
						Color = subj?.Color
					};
				}).ToList()
			});
		}

		var normalizedTeacherSearch = teacherSearch?.Trim();
		if (!string.IsNullOrWhiteSpace(normalizedTeacherSearch))
		{
			groupedAssignments = groupedAssignments
				.Where(a =>
					a.PrimaryTeacherNames.Any(name =>
					name.Contains(normalizedTeacherSearch, StringComparison.OrdinalIgnoreCase)))
				.ToList();
		}

		// Pagination
		var totalCount = groupedAssignments.Count;
		var totalPages = (int) Math.Ceiling(totalCount / (double) pageSize);

		var pagedItems = groupedAssignments
			.Skip(page * pageSize)
			.Take(pageSize)
			.ToList();

		return new SubjectAssignmentPagedResponse
		{
			TotalCount = totalCount,
			CurrentPage = page,
			PageSize = pageSize,
			TotalPages = totalPages,
			Items = pagedItems
		};
	}

	public async Task<SubjectAssignmentDto> GetAsync(Guid assignmentId)
	{
		var baseQuery = await assignmentRepository.WithDetailsAsync(a => a.Teachers, a => a.Documents, a => a.Subject, a => a.Chapters);

		var assignment = await AsyncExecuter.FirstOrDefaultAsync(
			baseQuery.Where(a => a.Id == assignmentId)
		) ?? throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(AssignedSubject), assignmentId);

		List<AssignedSubject> groupAssignments;
		if (assignment.ElectiveGroupId.HasValue)
		{
			groupAssignments = await AsyncExecuter.ToListAsync(
				baseQuery.Where(a => a.ElectiveGroupId == assignment.ElectiveGroupId && a.StandardId == assignment.StandardId)
			);
		}
		else
		{
			groupAssignments = new List<AssignedSubject> { assignment };
		}

		var readMetrics = await BuildSubjectAssignmentReadMetricsAsync(groupAssignments, assignment.StandardId);

		ElectiveGroup? electiveGroup = null;
		if (assignment.ElectiveGroupId.HasValue)
		{
			electiveGroup = await electiveGroupRepository.FindAsync(assignment.ElectiveGroupId.Value);
		}

		// Load teacher info in bulk
		var allTeacherIds = groupAssignments
			.SelectMany(a => a.Teachers)
			.Select(t => t.TeacherId)
			.Distinct()
			.ToList();
		var teachersDict = allTeacherIds.Count > 0
			? (await userRepository.GetListByIdsAsync(allTeacherIds))
				.ToDictionary(u => u.Id, u => $"{u.Name} {u.Surname}".Trim())
			: new Dictionary<Guid, string>();

		var first = groupAssignments.First();

		return new SubjectAssignmentDto
		{
			Id = first.Id,
			Status = first.Status,
			SubjectType = first.SubjectType,
			ElectiveGroupId = first.ElectiveGroupId,
			ElectiveGroupName = electiveGroup?.Name,
			Color = electiveGroup?.Color ?? first.Subject?.Color,
			Subjects = groupAssignments.Select(a =>
			{
				var metrics = GetReadMetrics(readMetrics, a);
				var primaryTeacherEntry = a.Teachers.FirstOrDefault(t => t.TeacherType == SubjectTeacherType.Primary);
				var secondaryTeacherEntries = a.Teachers.Where(t => t.TeacherType == SubjectTeacherType.Secondary).ToList();
				return new SubjectAssignmentDetailResponseDto
				{
					AssignedSubjectId = a.Id,
					SubjectId = a.SubjectId,
					SubjectName = a.Subject?.Name,
					SubjectCode = a.SubjectCode,
					Color = a.Subject?.Color,
					SubjectType = a.SubjectType,
					ElectiveGroupId = a.ElectiveGroupId,
					PrimaryTeacher = primaryTeacherEntry == null ? null : new TeacherInfoDto
					{
						Id = primaryTeacherEntry.TeacherId.ToString(),
						Name = teachersDict.GetValueOrDefault(primaryTeacherEntry.TeacherId, string.Empty)
					},
					SecondaryTeachers = secondaryTeacherEntries.Select(t => new TeacherInfoDto
					{
						Id = t.TeacherId.ToString(),
						Name = teachersDict.GetValueOrDefault(t.TeacherId, string.Empty)
					}).ToList(),
					LearningGoals = a.LearningGoals,
					Prerequisites = a.Prerequisites,
					Description = a.Description,
					Documents = a.Documents.Select(d => d.BlobName).ToList(),
					WeeklyPeriods = metrics.WeeklyPeriods,
					DifficultyLevel = a.DifficultyLevel,
					SubjectGroup = a.SubjectGroup,
					IsAiSuggested = a.IsAiSuggested,
					IsCopiedFromPreviousYear = a.IsCopiedFromPreviousYear,
					SetupHealthScore = metrics.SetupHealthScore,
					ChaptersCount = a.Chapters.Count,
					LastUpdatedAt = a.LastModificationTime ?? a.CreationTime,
				};
			}).ToList()
		};
	}

	public async Task<SubjectAssignmentDto> UpdateAsync(
		Guid assignmentId,
		Guid academicMediumId,
		CreateUpdateSubjectAssignmentDto input
	)
	{
		EnsureSubjectAssignmentInputIsConsistent(input);

		// 1. Identify the group being updated (with navigation properties loaded)
		var baseQuery = await assignmentRepository.WithDetailsAsync(a => a.Teachers, a => a.Documents);

		var existingEntity = await AsyncExecuter.FirstOrDefaultAsync(
			baseQuery.Where(a => a.Id == assignmentId)
		) ?? throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(AssignedSubject), assignmentId);

		List<AssignedSubject> existingGroup;

		if (existingEntity.ElectiveGroupId.HasValue)
		{
			existingGroup = await AsyncExecuter.ToListAsync(
				baseQuery.Where(x => x.ElectiveGroupId == existingEntity.ElectiveGroupId && x.StandardId == existingEntity.StandardId)
			);
		}
		else
		{
			existingGroup = new List<AssignedSubject> { existingEntity };
		}

		// 2. Process updates
		// We match existing subjects with input subjects by SubjectId
		var subjectsToDelete = new List<AssignedSubject>();
		var processedSubjectIds = new HashSet<Guid>();
		await EnsureSubjectsAreNotAlreadyAssignedAsync(
			input.ClassId,
			input.Subjects.Select(s => s.SubjectId),
			existingGroup.Select(a => a.Id));

		foreach (var inputSubject in input.Subjects)
		{
			processedSubjectIds.Add(inputSubject.SubjectId);
			var existingSubject = existingGroup.FirstOrDefault(x => x.SubjectId == inputSubject.SubjectId);

			if (existingSubject != null)
			{
				await EnsureTeacherRemovalAllowedAsync(existingSubject, inputSubject);

				// Update existing
				existingSubject.SubjectCode = inputSubject.SubjectCode ?? existingSubject.SubjectCode;
				existingSubject.LearningGoals = inputSubject.LearningGoal;
				existingSubject.Prerequisites = inputSubject.Prerequisites;
				existingSubject.Description = inputSubject.Description;
				existingSubject.ChangeStatus(input.Status);
				existingSubject.StandardId = input.ClassId;
				existingSubject.WeeklyPeriods = inputSubject.WeeklyPeriods;
				existingSubject.DifficultyLevel = inputSubject.DifficultyLevel;
				existingSubject.SubjectGroup = inputSubject.SubjectGroup;

				// Update Teachers
				if (inputSubject.PrimaryTeacherId.HasValue)
				{
					existingSubject.SetPrimaryTeacher(inputSubject.PrimaryTeacherId.Value);
				}
				else
				{
					existingSubject.RemovePrimaryTeacher();
				}
				if (inputSubject.SecondaryTeacherIds != null)
				{
					existingSubject.SetSecondaryTeachers(inputSubject.SecondaryTeacherIds);
				}

				// Update documents: clear and re-add
				if (inputSubject.Documents != null)
				{
					existingSubject.Documents.Clear();
					foreach (var doc in inputSubject.Documents)
					{
						existingSubject.AddDocument(guidGenerator.Create(), doc);
					}
				}

				existingSubject.ElectiveGroupId = inputSubject.ElectiveGroupId;
				existingSubject.SubjectType = (inputSubject.ElectiveGroupId == null || inputSubject.ElectiveGroupId == Guid.Empty)
					? SubjectType.Core : SubjectType.Elective;

				existingSubject.RecalculateSetupHealth();
				await assignmentRepository.UpdateAsync(existingSubject);
			}
			else
			{
				// Create new in this group
				var newSubject = new AssignedSubject(
					guidGenerator.Create(),
					CurrentTenant.Id,
					input.ClassId,
					inputSubject.SubjectId,
					inputSubject.SubjectCode,
					input.Status,
					(inputSubject.ElectiveGroupId == Guid.Empty || inputSubject.ElectiveGroupId == null) ? SubjectType.Core : SubjectType.Elective,
					inputSubject.ElectiveGroupId,
					inputSubject.LearningGoal,
					inputSubject.Prerequisites,
					inputSubject.Description,
					inputSubject.WeeklyPeriods,
					inputSubject.DifficultyLevel,
					inputSubject.SubjectGroup
				);

				if (inputSubject.PrimaryTeacherId.HasValue)
					newSubject.SetPrimaryTeacher(inputSubject.PrimaryTeacherId.Value);

				if (inputSubject.SecondaryTeacherIds != null)
					newSubject.SetSecondaryTeachers(inputSubject.SecondaryTeacherIds);

				if (inputSubject.Documents != null)
				{
					foreach (var doc in inputSubject.Documents)
					{
						newSubject.AddDocument(guidGenerator.Create(), doc);
					}
				}

				newSubject.RecalculateSetupHealth();
				await assignmentRepository.InsertAsync(newSubject);
				existingGroup.Add(newSubject); // Add to local list for return mapping
			}
		}

		// 3. Delete removed subjects
		foreach (var subject in existingGroup)
		{
			if (!processedSubjectIds.Contains(subject.SubjectId))
			{
				await EnsureAssignmentRemovalAllowedAsync(subject);
				await assignmentRepository.DeleteAsync(subject);
			}
		}

		// Re-fetch or use modified list.
		// Since we modified existingGroup in memory (for updates) and adding new ones,
		// wait, new ones were added to repo, but we need to include them in response.
		// Let's re-fetch to be safe and accurate, or construct response manually.
		// Since we might have deleted the one with 'assignmentId', we return the updated group.
		// If 'assignmentId' was deleted, what ID do we return?
		// The controller returns the DTO. The DTO Id is usually the first one.

		// If all deleted?
		if (processedSubjectIds.Count == 0) return null; // Should not happen due to validation

		// Re-fetch the group based on any of the surviving IDs or the new configuration
		// If we switched from Core to Elective or vice versa, logic holds.

		// For response, we can just use GetList logic behavior for this group.
		// But to be fast, let's map what we have if we tracked it well.
		// Safest is to Refetch using one of the current IDs.
		var representativeId = existingGroup.FirstOrDefault(x => processedSubjectIds.Contains(x.SubjectId))?.Id
			?? existingGroup.LastOrDefault()?.Id; // Fallback

		if (representativeId.HasValue)
		{
			return await GetAsync(representativeId.Value);
		}

		return null;
	}

	public async Task ChangeStatusAsync(Guid assignmentId, ChangeStatusRequestDto input)
	{
		var query = await assignmentRepository.WithDetailsAsync(a => a.Teachers, a => a.Documents, a => a.Chapters, a => a.Subject);
		var assignment = await AsyncExecuter.FirstOrDefaultAsync(query.Where(a => a.Id == assignmentId))
			?? throw new BusinessException("SubjectAssignment:NotFound").WithData("AssignmentId", assignmentId);

		if (!Enum.IsDefined(typeof(SubjectStatus), input.Status))
		{
			throw new UserFriendlyException("Invalid status value");
		}

		var status = (SubjectStatus) input.Status;
		EnsureSubjectAssignmentStatusTransitionAllowed(assignment, status);
		if (status == SubjectStatus.Published)
		{
			await SynchronizeWeeklyPeriodsAndRecalculateHealthAsync(new List<AssignedSubject> { assignment }, assignment.StandardId);
			EnsureSubjectAssignmentReadyToPublish(assignment);
		}

		assignment.ChangeStatus(status);

		if (status == SubjectStatus.Published)
		{
			assignment.PublishedAt = DateTimeOffset.UtcNow;
		}

		await assignmentRepository.UpdateAsync(assignment, autoSave: true);
	}

	public async Task<PublishResponseDto> PublishAsync(List<Guid> assignmentIds)
	{
		if (assignmentIds == null || assignmentIds.Count == 0)
		{
			throw new BusinessException("SubjectAssignment:PublishSelectionRequired");
		}

		try
		{
			var distinctAssignmentIds = assignmentIds.Distinct().ToList();
			var query = await assignmentRepository.WithDetailsAsync(a => a.Teachers, a => a.Documents, a => a.Chapters, a => a.Subject);
			var assignments = await AsyncExecuter.ToListAsync(query.Where(a => distinctAssignmentIds.Contains(a.Id)));

			if (assignments.Count != distinctAssignmentIds.Count)
			{
				throw new BusinessException("SubjectAssignment:NotFound")
					.WithData("AssignmentIds", JsonSerializer.Serialize(assignmentIds));
			}

			foreach (var classGroup in assignments.GroupBy(a => a.StandardId))
			{
				await SynchronizeWeeklyPeriodsAndRecalculateHealthAsync(classGroup.ToList(), classGroup.Key);
			}

			foreach (var assignment in assignments)
			{
				EnsureSubjectAssignmentStatusTransitionAllowed(assignment, SubjectStatus.Published);
				EnsureSubjectAssignmentReadyToPublish(assignment);
				assignment.ChangeStatus(SubjectStatus.Published);
				assignment.PublishedAt = DateTimeOffset.UtcNow;
			}

			await assignmentRepository.UpdateManyAsync(assignments, autoSave: true);

			return new PublishResponseDto
			{
				Id = assignments.First().Id,
				Status = SubjectStatus.Published,
				PublishedAt = DateTime.UtcNow
			};
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to publish assignments");
			throw new UserFriendlyException("Failed to publish assignments.");
		}
	}

	public async Task DeleteAsync(Guid assignmentId)
	{
		var assignment = await assignmentRepository.GetAsync(assignmentId);

		List<AssignedSubject> groupAssignments;
		if (assignment.ElectiveGroupId.HasValue)
		{
			groupAssignments = await assignmentRepository.GetListAsync(a =>
				a.ElectiveGroupId == assignment.ElectiveGroupId && a.StandardId == assignment.StandardId
			);
		}
		else
		{
			groupAssignments = new List<AssignedSubject> { assignment };
		}

		var subjectIds = groupAssignments.Select(a => a.SubjectId).ToList();

		var usageParts = await GetAssignmentUsagePartsAsync(
			assignment.StandardId,
			subjectIds,
			assignment.ElectiveGroupId,
			groupAssignments.Sum(a => a.Chapters.Count));

		if (usageParts.Count > 0)
		{
			foreach (var groupAssignment in groupAssignments)
			{
				await EnsureAssignmentRemovalAllowedAsync(groupAssignment);
				groupAssignment.ChangeStatus(SubjectStatus.Archived);
			}

			await assignmentRepository.UpdateManyAsync(groupAssignments, autoSave: true);
			return;
		}

		if (groupAssignments.Any(a => a.Status == SubjectStatus.Published))
		{
			foreach (var groupAssignment in groupAssignments)
			{
				await EnsureAssignmentRemovalAllowedAsync(groupAssignment);
				groupAssignment.ChangeStatus(SubjectStatus.Archived);
			}

			await assignmentRepository.UpdateManyAsync(groupAssignments, autoSave: true);
			return;
		}

		await assignmentRepository.DeleteManyAsync(groupAssignments, autoSave: true);

		var weeklySlot = await weeklyLectureSlotRepository.FindByStandardIdAsync(assignment.StandardId);
		if (weeklySlot != null)
		{
			weeklySlot.RemoveSubject(assignment.SubjectId, assignment.ElectiveGroupId);
			await weeklyLectureSlotRepository.UpdateAsync(weeklySlot, autoSave: true);
		}
	}

	public async Task<List<CoreElectiveSubjectResponseDto>> GetCoreElectiveSubjectsAsync(Guid academicMediumId)
	{
		try
		{
			var coreSubjects = await subjectRepository.GetListAsync(s =>
				s.EducationMediumId == academicMediumId && s.Status == SubjectMasterStatus.Active
			);
			var electiveGroups = await electiveGroupRepository.GetListAsync(eg =>
				eg.AcademicMediumId == academicMediumId && eg.Status == SubjectMasterStatus.Active
			);

			var result = new List<CoreElectiveSubjectResponseDto>();
			foreach (var coreSubject in coreSubjects)
			{
				var subjectDto = new CoreElectiveSubjectResponseDto
				{
					Id = coreSubject.Id,
					Name = coreSubject.Name,
					SubjectType = SubjectType.Core,
					Color = coreSubject.Color,
				};
				result.Add(subjectDto);

			}

			foreach (var electiveGroup in electiveGroups)
			{
				var subjectDto = new CoreElectiveSubjectResponseDto
				{
					Id = electiveGroup.Id,
					Name = electiveGroup.Name,
					SubjectType = SubjectType.Elective,
					Color = electiveGroup.Color,
					Subjects = electiveGroup.Subjects
						.Where(es => es.Subject.Status == SubjectMasterStatus.Active)
						.Select(es => new CoreElectiveSubjectDetailDto
						{
							Id = es.SubjectId,
							Name = es.Subject.Name,
							Color = es.Subject.Color,
						}).ToList()
				};
				result.Add(subjectDto);
			}
			return result;
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to retrieve core and elective subjects");
			throw new UserFriendlyException("Failed to retrieve subjects. Please try again later.");
		}
	}

	// ── New world-class endpoints ──────────────────────────────────────

	public async Task<SetupHealthDto> GetSetupHealthAsync(Guid assignmentId)
	{
		var query = await assignmentRepository.WithDetailsAsync(a => a.Teachers, a => a.Documents, a => a.Chapters, a => a.Subject);
		var assignment = await AsyncExecuter.FirstOrDefaultAsync(
			query.Where(a => a.Id == assignmentId)
		) ?? throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(AssignedSubject), assignmentId);

		var metrics = await BuildSubjectAssignmentReadMetricsAsync(new List<AssignedSubject> { assignment }, assignment.StandardId);
		var readMetrics = GetReadMetrics(metrics, assignment);

		var missing = new List<string>();
		if (string.IsNullOrWhiteSpace(assignment.SubjectCode)) missing.Add("Subject code is missing");
		if (!assignment.Teachers.Any(t => t.TeacherType == SubjectTeacherType.Primary)) missing.Add("Primary teacher is not assigned");
		if (string.IsNullOrWhiteSpace(assignment.LearningGoals)) missing.Add("Learning goals are missing");
		if (string.IsNullOrWhiteSpace(assignment.Description)) missing.Add("Description is missing");
		if (assignment.Chapters.Count == 0) missing.Add("No syllabus chapters added");
		else if (assignment.Chapters.Count < 3) missing.Add("Add at least 3 chapters for a complete syllabus");
		if (readMetrics.WeeklyPeriods == 0) missing.Add("Weekly periods not set");
		if (assignment.Documents.Count == 0) missing.Add("No documents attached");

		return new SetupHealthDto
		{
			AssignedSubjectId = assignment.Id,
			SubjectName = assignment.Subject?.Name,
			HealthScore = readMetrics.SetupHealthScore,
			MissingItems = missing
		};
	}

	public async Task<PublishReadinessDto> GetPublishReadinessAsync(Guid classId)
	{
		var query = await assignmentRepository.WithDetailsAsync(a => a.Teachers, a => a.Documents, a => a.Chapters, a => a.Subject);
		var assignments = await AsyncExecuter.ToListAsync(query.Where(a => a.StandardId == classId));

		var readMetrics = await BuildSubjectAssignmentReadMetricsAsync(assignments, classId);

		var issues = new List<PublishReadinessIssueDto>();
		foreach (var a in assignments)
		{

			if (!a.Teachers.Any(t => t.TeacherType == SubjectTeacherType.Primary))
			{
				issues.Add(new PublishReadinessIssueDto
				{
					SubjectId = a.Id,
					SubjectName = a.Subject?.Name,
					Severity = "Warning",
					Message = "Primary teacher is missing."
				});
			}

			if (string.IsNullOrWhiteSpace(a.LearningGoals))
			{
				issues.Add(new PublishReadinessIssueDto
				{
					SubjectId = a.Id,
					SubjectName = a.Subject?.Name,
					Severity = "Info",
					Message = "Learning goals are missing."
				});
			}

			if (a.Chapters.Count == 0)
			{
				issues.Add(new PublishReadinessIssueDto
				{
					SubjectId = a.Id,
					SubjectName = a.Subject?.Name,
					Severity = "Info",
					Message = "No syllabus chapters added."
				});
			}
		}

		var subjectsWithIssues = issues.DistinctBy(i => i.SubjectId).Count();
		var overallHealth = assignments.Count > 0
			? (int) assignments.Average(a => GetReadMetrics(readMetrics, a).SetupHealthScore)
			: 0;

		return new PublishReadinessDto
		{
			OverallHealth = overallHealth,
			ReadyToPublish = assignments.Count - subjectsWithIssues,
			NeedsAttention = subjectsWithIssues,
			Issues = issues
		};
	}

	public async Task<TeacherWorkloadDto> GetTeacherWorkloadAsync(Guid teacherId)
	{
		const int MaxAllowedWeeklyPeriods = 28;

		var teacherUser = await userRepository.FindAsync(teacherId)
			?? throw new UserFriendlyException("Teacher not found.");

		var query = await assignmentRepository.WithDetailsAsync(a => a.Teachers, a => a.Subject, a => a.Standard);
		var assignedSubjects = await AsyncExecuter.ToListAsync(
			query.Where(a => a.Teachers.Any(t => t.TeacherId == teacherId && t.TeacherType == SubjectTeacherType.Primary))
		);

		var totalPeriods = assignedSubjects.Sum(a => a.WeeklyPeriods);
		var loadStatus = totalPeriods switch
		{
			<= 20 => "Balanced",
			<= MaxAllowedWeeklyPeriods => "High",
			_ => "Overloaded"
		};

		return new TeacherWorkloadDto
		{
			TeacherId = teacherId,
			TeacherName = $"{teacherUser.Name} {teacherUser.Surname}".Trim(),
			CurrentSubjectsCount = assignedSubjects.Count,
			CurrentWeeklyPeriods = totalPeriods,
			MaxAllowedWeeklyPeriods = MaxAllowedWeeklyPeriods,
			LoadStatus = loadStatus,
			AssignedSubjects = [.. assignedSubjects.Select(a => new TeacherSubjectSummaryDto
			{
				AssignedSubjectId = a.Id,
				SubjectName = a.Subject?.Name,
				ClassName = a.Standard?.Name,
				WeeklyPeriods = a.WeeklyPeriods
			})]
		};
	}

	public async Task<SubjectKpiDto> GetKpiAsync(Guid classId)
	{
		var query = await assignmentRepository.WithDetailsAsync(a => a.Teachers);
		var assignments = await AsyncExecuter.ToListAsync(query.Where(a => a.StandardId == classId));

		return new SubjectKpiDto
		{
			TotalSubjects = assignments.Count,
			CoreSubjects = assignments.Count(a => a.SubjectType == SubjectType.Core),
			ElectiveSubjects = assignments.Count(a => a.SubjectType == SubjectType.Elective),
			OptionalSubjects = assignments.Count(a => a.SubjectType == SubjectType.Optional),
			SubjectsWithTeacher = assignments.Count(a => a.Teachers.Any(t => t.TeacherType == SubjectTeacherType.Primary)),
			SubjectsMissingTeacher = assignments.Count(a => !a.Teachers.Any(t => t.TeacherType == SubjectTeacherType.Primary)),
			PublishedSubjects = assignments.Count(a => a.Status == SubjectStatus.Published),
			DraftSubjects = assignments.Count(a => a.Status == SubjectStatus.Draft),
			OverallHealthScore = assignments.Count > 0 ? (int) assignments.Average(a => a.SetupHealthScore) : 0
		};
	}

	public async Task ArchiveAsync(Guid assignmentId)
	{
		var assignment = await assignmentRepository.GetAsync(assignmentId);
		var groupAssignments = assignment.ElectiveGroupId.HasValue
			? await assignmentRepository.GetListAsync(a =>
				a.ElectiveGroupId == assignment.ElectiveGroupId &&
				a.StandardId == assignment.StandardId)
			: new List<AssignedSubject> { assignment };

		foreach (var groupAssignment in groupAssignments)
		{
			groupAssignment.ChangeStatus(SubjectStatus.Archived);
		}

		await assignmentRepository.UpdateManyAsync(groupAssignments, autoSave: true);
	}

	public async Task RestoreAsync(Guid assignmentId)
	{
		var assignment = await assignmentRepository.GetAsync(assignmentId);
		if (assignment.Status != SubjectStatus.Archived)
		{
			throw new BusinessException("SubjectAssignment:OnlyArchivedAssignmentCanBeRestored")
				.WithData("AssignmentId", assignmentId)
				.WithData("CurrentStatus", assignment.Status);
		}

		var groupAssignments = assignment.ElectiveGroupId.HasValue
			? await assignmentRepository.GetListAsync(a =>
				a.ElectiveGroupId == assignment.ElectiveGroupId &&
				a.StandardId == assignment.StandardId &&
				a.Status == SubjectStatus.Archived)
			: new List<AssignedSubject> { assignment };

		await EnsureSubjectsAreNotAlreadyAssignedAsync(
			assignment.StandardId,
			groupAssignments.Select(a => a.SubjectId),
			groupAssignments.Select(a => a.Id));

		foreach (var groupAssignment in groupAssignments)
		{
			groupAssignment.ChangeStatus(SubjectStatus.Draft);
			groupAssignment.PublishedAt = null;
		}

		await assignmentRepository.UpdateManyAsync(groupAssignments, autoSave: true);
	}

	public async Task<CopySubjectAssignmentsResultDto> CopyAsync(CopySubjectAssignmentsDto input)
	{
		if (input.SourceClassId == Guid.Empty || input.TargetClassId == Guid.Empty)
		{
			throw new BusinessException("SubjectAssignment:CopyClassRequired");
		}

		if (input.SourceClassId == input.TargetClassId)
		{
			throw new BusinessException("SubjectAssignment:CopySourceAndTargetSame");
		}

		var conflictStrategy = (input.ConflictStrategy ?? "SKIP").Trim().ToUpperInvariant();
		if (conflictStrategy is not ("SKIP" or "OVERWRITE"))
		{
			throw new BusinessException("SubjectAssignment:InvalidCopyConflictStrategy")
				.WithData("ConflictStrategy", input.ConflictStrategy ?? string.Empty);
		}

		var requestedSubjectIds = input.SubjectIds?
			.Where(id => id != Guid.Empty)
			.Distinct()
			.ToHashSet();

		var query = await assignmentRepository.WithDetailsAsync(
			a => a.Teachers,
			a => a.Documents,
			a => a.Chapters);

		var sourceQuery = query.Where(a =>
			a.StandardId == input.SourceClassId &&
			a.Status != SubjectStatus.Archived &&
			a.Status != SubjectStatus.Inactive);
		if (requestedSubjectIds is { Count: > 0 })
		{
			sourceQuery = sourceQuery.Where(a => requestedSubjectIds.Contains(a.SubjectId));
		}

		var sourceAssignments = await AsyncExecuter.ToListAsync(sourceQuery);

		if (sourceAssignments.Count == 0)
		{
			throw new BusinessException("SubjectAssignment:CopySourceEmpty")
				.WithData("SourceClassId", input.SourceClassId);
		}

		var targetAssignments = await AsyncExecuter.ToListAsync(
			query.Where(a => a.StandardId == input.TargetClassId));
		var targetBySubjectId = targetAssignments.ToDictionary(a => a.SubjectId);

		var copied = new List<AssignedSubject>();
		var overwritten = new List<AssignedSubject>();
		var skipped = 0;

		foreach (var source in sourceAssignments.OrderBy(a => a.SubjectCode))
		{
			if (targetBySubjectId.TryGetValue(source.SubjectId, out var target))
			{
				if (conflictStrategy == "SKIP")
				{
					skipped++;
					continue;
				}

				ApplyCopiedSubject(source, target, input);
				overwritten.Add(target);
				await assignmentRepository.UpdateAsync(target);
				continue;
			}

			var newAssignment = CreateCopiedSubject(source, input.TargetClassId, input);
			copied.Add(newAssignment);
			targetBySubjectId[source.SubjectId] = newAssignment;
		}

		if (copied.Count > 0)
		{
			await assignmentRepository.InsertManyAsync(copied, autoSave: false);
		}

		var changedAssignments = copied.Concat(overwritten).ToList();
		if (changedAssignments.Count > 0)
		{
			await SyncCopiedSubjectsToWeeklySlotAsync(
				input.TargetClassId,
				changedAssignments,
				input.IncludeCurriculumDetails);
		}

		var result = new CopySubjectAssignmentsResultDto
		{
			CopiedCount = copied.Count,
			SkippedCount = skipped,
			OverwrittenCount = overwritten.Count
		};
		result.Message = $"{result.CopiedCount + result.OverwrittenCount} subject allocation(s) copied. {result.SkippedCount} skipped.";

		return result;
	}

	private AssignedSubject CreateCopiedSubject(
		AssignedSubject source,
		Guid targetClassId,
		CopySubjectAssignmentsDto input)
	{
		var target = new AssignedSubject(
			guidGenerator.Create(),
			CurrentTenant.Id,
			targetClassId,
			source.SubjectId,
			source.SubjectCode,
			SubjectStatus.Draft,
			source.SubjectType,
			source.ElectiveGroupId,
			input.IncludeCurriculumDetails ? source.LearningGoals : null,
			input.IncludeCurriculumDetails ? source.Prerequisites : null,
			input.IncludeCurriculumDetails ? source.Description : null,
			input.IncludeCurriculumDetails ? source.WeeklyPeriods : 0,
			input.IncludeCurriculumDetails ? source.DifficultyLevel : DifficultyLevel.Beginner,
			input.IncludeCurriculumDetails ? source.SubjectGroup : null,
			source.IsAiSuggested,
			true);

		ApplyCopiedChildren(source, target, input, preserveExistingTeachers: false);
		target.RecalculateSetupHealth();
		return target;
	}

	private void ApplyCopiedSubject(
		AssignedSubject source,
		AssignedSubject target,
		CopySubjectAssignmentsDto input)
	{
		target.StandardId = input.TargetClassId;
		target.SubjectId = source.SubjectId;
		target.SubjectCode = source.SubjectCode;
		target.SubjectType = source.SubjectType;
		target.ElectiveGroupId = source.ElectiveGroupId;
		target.IsAiSuggested = source.IsAiSuggested;
		target.IsCopiedFromPreviousYear = true;
		target.ChangeStatus(SubjectStatus.Draft);
		target.PublishedAt = null;

		if (input.IncludeCurriculumDetails)
		{
			target.LearningGoals = source.LearningGoals;
			target.Prerequisites = source.Prerequisites;
			target.Description = source.Description;
			target.WeeklyPeriods = source.WeeklyPeriods;
			target.DifficultyLevel = source.DifficultyLevel;
			target.SubjectGroup = source.SubjectGroup;
		}

		ApplyCopiedChildren(source, target, input, preserveExistingTeachers: true);
		target.RecalculateSetupHealth();
	}

	private void ApplyCopiedChildren(
		AssignedSubject source,
		AssignedSubject target,
		CopySubjectAssignmentsDto input,
		bool preserveExistingTeachers)
	{
		if (input.IncludeTeachers)
		{
			target.Teachers.Clear();
			var primaryTeacher = source.Teachers.FirstOrDefault(t => t.TeacherType == SubjectTeacherType.Primary);
			if (primaryTeacher != null)
			{
				target.SetPrimaryTeacher(primaryTeacher.TeacherId);
			}

			var secondaryTeacherIds = source.Teachers
				.Where(t => t.TeacherType == SubjectTeacherType.Secondary)
				.Select(t => t.TeacherId)
				.ToList();
			target.SetSecondaryTeachers(secondaryTeacherIds);
		}
		else if (!preserveExistingTeachers)
		{
			target.Teachers.Clear();
		}

		if (input.IncludeDocuments)
		{
			target.Documents.Clear();
			foreach (var document in source.Documents.Where(d => !d.ChapterId.HasValue))
			{
				target.AddDocument(guidGenerator.Create(), document.BlobName);
			}
		}

		if (!input.IncludeCurriculumDetails)
		{
			return;
		}

		target.Chapters.Clear();
		if (!input.IncludeDocuments)
		{
			foreach (var document in target.Documents.Where(d => d.ChapterId.HasValue).ToList())
			{
				target.Documents.Remove(document);
			}
		}

		foreach (var sourceChapter in source.Chapters.OrderBy(c => c.ChapterNumber))
		{
			var chapterId = guidGenerator.Create();
			var targetChapter = new AssignedSubjectChapter(
				chapterId,
				target.Id,
				sourceChapter.ChapterNumber,
				sourceChapter.ChapterName,
				sourceChapter.ChapterStartTime,
				sourceChapter.ChapterEndTime,
				sourceChapter.Description)
			{
				TenantId = CurrentTenant.Id,
				UnitName = sourceChapter.UnitName,
				TermName = sourceChapter.TermName,
				Topic = sourceChapter.Topic,
				EstimatedTeachingDays = sourceChapter.EstimatedTeachingDays,
				EstimatedPeriods = sourceChapter.EstimatedPeriods,
				RevisionDays = sourceChapter.RevisionDays,
				BufferDays = sourceChapter.BufferDays,
				DifficultyLevel = sourceChapter.DifficultyLevel,
				BloomLevel = sourceChapter.BloomLevel,
				Tags = sourceChapter.Tags,
				LearningOutcomes = sourceChapter.LearningOutcomes,
				SkillsCovered = sourceChapter.SkillsCovered,
				Prerequisites = sourceChapter.Prerequisites,
				TeachingMethods = sourceChapter.TeachingMethods,
				ClassActivities = sourceChapter.ClassActivities,
				CompletionCriteria = sourceChapter.CompletionCriteria,
				TeacherNotes = sourceChapter.TeacherNotes,
				StudentSummary = sourceChapter.StudentSummary,
				ParentSummary = sourceChapter.ParentSummary,
				ReferenceLinks = sourceChapter.ReferenceLinks,
				Worksheets = sourceChapter.Worksheets,
				AudioVideoMaterials = sourceChapter.AudioVideoMaterials,
				BoardDocuments = sourceChapter.BoardDocuments,
				AssessmentMethod = sourceChapter.AssessmentMethod,
				QuestionTypes = sourceChapter.QuestionTypes,
				MarksWeightage = sourceChapter.MarksWeightage,
				BloomCoverage = sourceChapter.BloomCoverage,
				DifficultyDistribution = sourceChapter.DifficultyDistribution,
				Visibility = sourceChapter.Visibility,
				PublishStatus = sourceChapter.PublishStatus,
				ReviewStatus = sourceChapter.ReviewStatus,
				NotifyOnPublish = sourceChapter.NotifyOnPublish,
				ProgressPercentage = sourceChapter.ProgressPercentage,
				CoverageStatus = sourceChapter.CoverageStatus,
				LessonPlanStatus = sourceChapter.LessonPlanStatus,
				HomeworkStatus = sourceChapter.HomeworkStatus,
				AssessmentStatus = sourceChapter.AssessmentStatus,
				ResourceStatus = sourceChapter.ResourceStatus,
				QuestionCount = sourceChapter.QuestionCount,
				HealthScore = sourceChapter.HealthScore,
				IsAiGenerated = sourceChapter.IsAiGenerated,
				CopiedFromPreviousYear = true,
				TemplateSource = sourceChapter.TemplateSource,
				DependencyChapterIds = sourceChapter.DependencyChapterIds,
				VersionLabel = sourceChapter.VersionLabel
			};

			if (input.IncludeDocuments)
			{
				var chapterDocuments = source.Documents
					.Where(d => d.ChapterId == sourceChapter.Id)
					.ToList();
				foreach (var document in chapterDocuments)
				{
					targetChapter.AddDocument(new AssignedSubjectDocument(
						guidGenerator.Create(),
						target.Id,
						targetChapter.Id,
						document.BlobName));
				}
			}

			target.Chapters.Add(targetChapter);
		}
	}

	private async Task SyncCopiedSubjectsToWeeklySlotAsync(
		Guid targetClassId,
		List<AssignedSubject> assignments,
		bool copyWeeklyPeriods)
	{
		var weeklySlot = await weeklyLectureSlotRepository.FindByStandardIdAsync(targetClassId);
		if (weeklySlot == null)
		{
			weeklySlot = new WeeklyLectureSlot(guidGenerator.Create(), CurrentTenant.Id, targetClassId);
			weeklySlot = await weeklyLectureSlotRepository.InsertAsync(weeklySlot, autoSave: true);
		}

		var processedSubjects = new HashSet<Guid>();
		var processedElectives = new HashSet<Guid>();
		foreach (var assignment in assignments)
		{
			var weeklyPeriods = copyWeeklyPeriods ? assignment.WeeklyPeriods : 0;
			if (assignment.SubjectType == SubjectType.Elective && assignment.ElectiveGroupId.HasValue)
			{
				if (!processedElectives.Add(assignment.ElectiveGroupId.Value))
				{
					continue;
				}

				weeklySlot.RemoveSubject(null, assignment.ElectiveGroupId);
				weeklySlot.AddSubject(guidGenerator.Create(), null, assignment.ElectiveGroupId, weeklyPeriods);
			}
			else
			{
				if (!processedSubjects.Add(assignment.SubjectId))
				{
					continue;
				}

				weeklySlot.RemoveSubject(assignment.SubjectId, null);
				weeklySlot.AddSubject(guidGenerator.Create(), assignment.SubjectId, null, weeklyPeriods);
			}
		}

		await weeklyLectureSlotRepository.UpdateAsync(weeklySlot, autoSave: true);
	}

	private static void EnsureSubjectAssignmentInputIsConsistent(CreateUpdateSubjectAssignmentDto input)
	{
		if (input.Subjects == null || input.Subjects.Count == 0)
		{
			throw new BusinessException("SubjectAssignment:SubjectsRequired");
		}

		var duplicateSubjectIds = input.Subjects
			.GroupBy(s => s.SubjectId)
			.Where(g => g.Key == Guid.Empty || g.Count() > 1)
			.Select(g => g.Key)
			.ToList();
		if (duplicateSubjectIds.Count > 0)
		{
			throw new BusinessException("SubjectAssignment:DuplicateSubjectInRequest")
				.WithData("SubjectIds", JsonSerializer.Serialize(duplicateSubjectIds));
		}

		foreach (var subject in input.Subjects)
		{
			var secondaryTeacherIds = subject.SecondaryTeacherIds?
				.Where(id => id != Guid.Empty)
				.ToList() ?? [];
			var duplicateSecondaryTeacherIds = secondaryTeacherIds
				.GroupBy(id => id)
				.Where(g => g.Count() > 1)
				.Select(g => g.Key)
				.ToList();

			if (duplicateSecondaryTeacherIds.Count > 0)
			{
				throw new BusinessException("SubjectAssignment:DuplicateSecondaryTeacher")
					.WithData("SubjectId", subject.SubjectId)
					.WithData("TeacherIds", JsonSerializer.Serialize(duplicateSecondaryTeacherIds));
			}

			if (subject.PrimaryTeacherId.HasValue && secondaryTeacherIds.Contains(subject.PrimaryTeacherId.Value))
			{
				throw new BusinessException("SubjectAssignment:PrimaryTeacherCannotBeSecondary")
					.WithData("SubjectId", subject.SubjectId)
					.WithData("TeacherId", subject.PrimaryTeacherId.Value);
			}
		}
	}

	private async Task EnsureSubjectsAreNotAlreadyAssignedAsync(
		Guid classId,
		IEnumerable<Guid> subjectIds,
		IEnumerable<Guid> excludedAssignmentIds)
	{
		var requestedSubjectIds = subjectIds.Distinct().ToList();
		var excludedIds = excludedAssignmentIds.Distinct().ToList();
		if (requestedSubjectIds.Count == 0)
		{
			return;
		}

		var duplicateCount = await assignmentRepository.CountAsync(a =>
			a.StandardId == classId &&
			a.Status != SubjectStatus.Archived &&
			requestedSubjectIds.Contains(a.SubjectId) &&
			!excludedIds.Contains(a.Id));

		if (duplicateCount > 0)
		{
			throw new BusinessException("SubjectAssignment:SubjectAlreadyAssignedInClass")
				.WithData("ClassId", classId)
				.WithData("SubjectIds", JsonSerializer.Serialize(requestedSubjectIds));
		}
	}

	private async Task<List<string>> GetAssignmentUsagePartsAsync(
		Guid standardId,
		IReadOnlyCollection<Guid> subjectIds,
		Guid? electiveGroupId,
		int chapterCount = 0)
	{
		var usageParts = new List<string>();

		if (chapterCount > 0)
		{
			usageParts.Add($"{chapterCount} syllabus chapter(s)");
		}

		var timetables = await timetableRepository.GetByStandardIdAsync(standardId);
		var timetableSlotSubjectsCount = timetables
			.SelectMany(t => t.Slots)
			.SelectMany(s => s.Subjects)
			.Count(ss => subjectIds.Contains(ss.SubjectId));
		var timetableSlotElectivesCount = electiveGroupId.HasValue
			? timetables.SelectMany(t => t.Slots).Count(s => s.ElectiveGroupId == electiveGroupId.Value)
			: 0;
		var totalTimetableSlots = timetableSlotSubjectsCount + timetableSlotElectivesCount;
		if (totalTimetableSlots > 0)
		{
			usageParts.Add($"{totalTimetableSlots} timetable slot(s)");
		}

		var examCount = await examRepository.CountAsync(e =>
			e.StandardId == standardId &&
			e.ExamSubjects.Any(es => subjectIds.Contains(es.SubjectId) ||
				(electiveGroupId.HasValue && es.SubjectId == electiveGroupId.Value)));
		if (examCount > 0)
		{
			usageParts.Add($"{examCount} exam(s)");
		}

		var homeworkCount = await homeworkRepository.CountAsync(h =>
			h.StandardId == standardId &&
			subjectIds.Contains(h.SubjectId));
		if (homeworkCount > 0)
		{
			usageParts.Add($"{homeworkCount} homework(s)");
		}

		var learningTaskCount = await learningTaskAssignmentRepository.CountAsync(a =>
			a.StandardId == standardId &&
			subjectIds.Contains(a.SubjectId));
		if (learningTaskCount > 0)
		{
			usageParts.Add($"{learningTaskCount} assignment(s)");
		}

		return usageParts;
	}

	private async Task EnsureTeacherRemovalAllowedAsync(
		AssignedSubject existingSubject,
		SubjectAssignmentDetailDto inputSubject)
	{
		var existingTeacherIds = existingSubject.Teachers
			.Select(t => t.TeacherId)
			.Distinct()
			.ToHashSet();
		var requestedTeacherIds = new HashSet<Guid>();
		if (inputSubject.PrimaryTeacherId.HasValue && inputSubject.PrimaryTeacherId.Value != Guid.Empty)
		{
			requestedTeacherIds.Add(inputSubject.PrimaryTeacherId.Value);
		}
		foreach (var secondaryTeacherId in inputSubject.SecondaryTeacherIds ?? [])
		{
			if (secondaryTeacherId != Guid.Empty)
			{
				requestedTeacherIds.Add(secondaryTeacherId);
			}
		}

		var removedTeacherIds = existingTeacherIds.Except(requestedTeacherIds).ToList();
		if (removedTeacherIds.Count == 0)
		{
			return;
		}

		var activeLessonPlans = await lessonPlanRepository.GetListAsync(lp =>
			lp.StandardId == existingSubject.StandardId &&
			lp.SubjectId == existingSubject.SubjectId &&
			removedTeacherIds.Contains(lp.TeacherId) &&
			lp.Status != LessonPlanStatus.Rejected);

		if (activeLessonPlans.Count > 0)
		{
			throw new BusinessException("SubjectAssignment:CannotRemoveTeacherWithActiveLessonPlans")
				.WithData("AssignmentId", existingSubject.Id)
				.WithData("SubjectId", existingSubject.SubjectId)
				.WithData("TeacherIds", JsonSerializer.Serialize(removedTeacherIds))
				.WithData("LessonPlanCount", activeLessonPlans.Count);
		}

		var lessonPlanIds = activeLessonPlans.Select(lp => lp.Id).ToList();
		if (lessonPlanIds.Count > 0)
		{
			var submittedDiaryCount = await teacherDiaryRepository.CountAsync(td =>
				lessonPlanIds.Contains(td.LessonPlanId) &&
				removedTeacherIds.Contains(td.TeacherId) &&
				td.Status == TeacherDiaryStatus.Submitted);
			if (submittedDiaryCount > 0)
			{
				throw new BusinessException("SubjectAssignment:CannotRemoveTeacherWithSubmittedDiary")
					.WithData("AssignmentId", existingSubject.Id)
					.WithData("SubjectId", existingSubject.SubjectId)
					.WithData("TeacherIds", JsonSerializer.Serialize(removedTeacherIds))
					.WithData("DiaryCount", submittedDiaryCount);
			}
		}
	}

	private async Task EnsureAssignmentRemovalAllowedAsync(AssignedSubject assignment)
	{
		var blockers = new List<string>();
		var activeLessonPlanCount = await lessonPlanRepository.CountAsync(lp =>
			lp.StandardId == assignment.StandardId &&
			lp.SubjectId == assignment.SubjectId &&
			lp.Status != LessonPlanStatus.Rejected);
		if (activeLessonPlanCount > 0)
		{
			blockers.Add($"{activeLessonPlanCount} active lesson plan(s)");
		}

		var publishedHomeworkCount = await homeworkRepository.CountAsync(h =>
			h.StandardId == assignment.StandardId &&
			h.SubjectId == assignment.SubjectId &&
			(h.Status == HomeworkStatus.Published || h.IsPublished || h.DivisionSchedules.Any(d => d.IsPublished)));
		if (publishedHomeworkCount > 0)
		{
			blockers.Add($"{publishedHomeworkCount} published homework(s)");
		}

		var publishedAssignmentCount = await learningTaskAssignmentRepository.CountAsync(a =>
			a.StandardId == assignment.StandardId &&
			a.SubjectId == assignment.SubjectId &&
			(a.Status == AssignmentStatus.Published || a.DivisionSchedules.Any(d => d.IsPublished)));
		if (publishedAssignmentCount > 0)
		{
			blockers.Add($"{publishedAssignmentCount} published assignment(s)");
		}

		if (blockers.Count > 0)
		{
			throw new BusinessException("SubjectAssignment:CannotArchiveOrDeleteActiveAssignment")
				.WithData("AssignmentId", assignment.Id)
				.WithData("SubjectId", assignment.SubjectId)
				.WithData("Blockers", blockers);
		}
	}

	private static void EnsureSubjectAssignmentStatusTransitionAllowed(AssignedSubject assignment, SubjectStatus targetStatus)
	{
		if (assignment.Status == SubjectStatus.Archived && targetStatus != SubjectStatus.Archived)
		{
			throw new BusinessException("SubjectAssignment:ArchivedAssignmentCannotBeReopened")
				.WithData("AssignmentId", assignment.Id)
				.WithData("SubjectName", assignment.Subject?.Name);
		}

		if (assignment.Status == SubjectStatus.Inactive && targetStatus == SubjectStatus.Published)
		{
			throw new BusinessException("SubjectAssignment:InactiveAssignmentCannotBePublished")
				.WithData("AssignmentId", assignment.Id)
				.WithData("SubjectName", assignment.Subject?.Name);
		}
	}

	private static void EnsureSubjectAssignmentReadyToPublish(AssignedSubject assignment)
	{
		var blockers = new List<string>();
		if (string.IsNullOrWhiteSpace(assignment.SubjectCode)) blockers.Add("Subject code is missing.");

		if (blockers.Count > 0)
		{
			throw new BusinessException("SubjectAssignment:PublishReadinessFailed")
				.WithData("AssignmentId", assignment.Id)
				.WithData("SubjectName", assignment.Subject?.Name)
				.WithData("Blockers", blockers);
		}
	}

	private async Task SynchronizeWeeklyPeriodsAndRecalculateHealthAsync(List<AssignedSubject> assignments, Guid classId)
	{
		var weeklySlot = await weeklyLectureSlotRepository.FindByStandardIdAsync(classId);
		var weeklyLecturesBySubject = weeklySlot?.Subjects
			.Where(s => s.SubjectId.HasValue)
			.ToDictionary(s => s.SubjectId!.Value, s => s.WeeklyLectures)
			?? new Dictionary<Guid, int>();
		var weeklyLecturesByElective = weeklySlot?.Subjects
			.Where(s => s.ElectiveGroupId.HasValue)
			.ToDictionary(s => s.ElectiveGroupId!.Value, s => s.WeeklyLectures)
			?? new Dictionary<Guid, int>();

		foreach (var a in assignments)
		{
			var lectures = 0;
			if (a.SubjectType == SubjectType.Elective && a.ElectiveGroupId.HasValue)
			{
				lectures = weeklyLecturesByElective.GetValueOrDefault(a.ElectiveGroupId.Value, 0);
			}
			else
			{
				lectures = weeklyLecturesBySubject.GetValueOrDefault(a.SubjectId, 0);
			}

			if (a.WeeklyPeriods != lectures)
			{
				a.WeeklyPeriods = lectures;
			}
			a.RecalculateSetupHealth();
			await assignmentRepository.UpdateAsync(a);
		}
	}

	private async Task<Dictionary<Guid, SubjectAssignmentReadMetrics>> BuildSubjectAssignmentReadMetricsAsync(
		IReadOnlyCollection<AssignedSubject> assignments,
		Guid classId)
	{
		if (assignments.Count == 0)
		{
			return new Dictionary<Guid, SubjectAssignmentReadMetrics>();
		}

		var weeklySlot = await weeklyLectureSlotRepository.FindByStandardIdAsync(classId);
		var weeklyLecturesBySubject = weeklySlot?.Subjects
			.Where(s => s.SubjectId.HasValue)
			.ToDictionary(s => s.SubjectId!.Value, s => s.WeeklyLectures)
			?? new Dictionary<Guid, int>();
		var weeklyLecturesByElective = weeklySlot?.Subjects
			.Where(s => s.ElectiveGroupId.HasValue)
			.ToDictionary(s => s.ElectiveGroupId!.Value, s => s.WeeklyLectures)
			?? new Dictionary<Guid, int>();

		return assignments.ToDictionary(
			a => a.Id,
			a =>
			{
				var weeklyPeriods = a.SubjectType == SubjectType.Elective && a.ElectiveGroupId.HasValue
					? weeklyLecturesByElective.GetValueOrDefault(a.ElectiveGroupId.Value, 0)
					: weeklyLecturesBySubject.GetValueOrDefault(a.SubjectId, 0);

				return new SubjectAssignmentReadMetrics(
					weeklyPeriods,
					CalculateSetupHealthScore(a, weeklyPeriods));
			});
	}

	private static SubjectAssignmentReadMetrics GetReadMetrics(
		IReadOnlyDictionary<Guid, SubjectAssignmentReadMetrics> metrics,
		AssignedSubject assignment)
	{
		return metrics.TryGetValue(assignment.Id, out var value)
			? value
			: SubjectAssignmentReadMetrics.FromAssignment(assignment);
	}

	private static int CalculateSetupHealthScore(AssignedSubject assignment, int weeklyPeriods)
	{
		var score = 0;
		if (!string.IsNullOrWhiteSpace(assignment.SubjectCode)) score += 10;
		score += 10;
		if (assignment.Teachers.Any(t => t.TeacherType == SubjectTeacherType.Primary)) score += 20;
		if (HasMeaningfulContent(assignment.LearningGoals)) score += 15;
		if (HasMeaningfulContent(assignment.Description)) score += 5;
		if (HasMeaningfulContent(assignment.Prerequisites)) score += 5;
		if (assignment.Chapters.Count > 0) score += 15;
		if (assignment.Chapters.Count >= 3) score += 10;
		if (weeklyPeriods > 0) score += 5;
		if (assignment.Documents.Count > 0) score += 5;

		return Math.Min(score, 100);
	}

	private static bool HasMeaningfulContent(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return false;
		}

		var text = System.Text.RegularExpressions.Regex.Replace(value, "<.*?>", string.Empty);
		text = text.Replace("&nbsp;", " ").Trim();
		return !string.IsNullOrWhiteSpace(text);
	}

	private sealed record SubjectAssignmentReadMetrics(int WeeklyPeriods, int SetupHealthScore)
	{
		public static SubjectAssignmentReadMetrics FromAssignment(AssignedSubject assignment)
		{
			return new SubjectAssignmentReadMetrics(assignment.WeeklyPeriods, assignment.SetupHealthScore);
		}
	}
}
