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
		int pageSize
	)
	{
		var query = await assignmentRepository.WithDetailsAsync(a => a.Chapters, a => a.Teachers);

		query = query.Where(a => a.StandardId == classId);

		// Non-admin users only see subjects they are personally assigned to teach.
		if (!await userScopeAccessService.CurrentUserHasTenantWideScopeAsync())
		{
			var userId = CurrentUser.Id!.Value;
			query = query.Where(a => a.Teachers.Any(t => t.TeacherId == userId));
		}

		var assignments = await AsyncExecuter.ToListAsync(query);
		await SynchronizeWeeklyPeriodsAndRecalculateHealthAsync(assignments, classId);

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
			var totalSlots = weeklyLecturesBySubject.GetValueOrDefault(coreAssignment.SubjectId, 0);
			var occupiedSlots = occupiedBySubject.GetValueOrDefault(coreAssignment.SubjectId, 0);
			var primaryTeacherEntry = coreAssignment.Teachers.FirstOrDefault(t => t.TeacherType == SubjectTeacherType.Primary);

			groupedAssignments.Add(new SubjectAssignmentGroupDto
			{
				Id = coreAssignment.Id,
				SubjectType = coreAssignment.SubjectType,
				Status = coreAssignment.Status,
				Color = subject?.Color,
				TotalSlots = totalSlots,
				OccupiedSlots = occupiedSlots,
				UnoccupiedSlots = totalSlots - occupiedSlots,
				SetupHealthScore = coreAssignment.SetupHealthScore,
				ChaptersCount = coreAssignment.Chapters.Count,
				WeeklyPeriods = coreAssignment.WeeklyPeriods,
				PrimaryTeacherId = primaryTeacherEntry?.TeacherId.ToString(),
				PrimaryTeacherName = primaryTeacherEntry != null
					? teacherNamesDict.GetValueOrDefault(primaryTeacherEntry.TeacherId)
					: null,
				LastUpdatedAt = coreAssignment.LastModificationTime ?? coreAssignment.CreationTime,
				IsAiSuggested = coreAssignment.IsAiSuggested,
				IsCopiedFromPreviousYear = coreAssignment.IsCopiedFromPreviousYear,
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
			var electiveTotalSlots = weeklyLecturesByElective.GetValueOrDefault(first.ElectiveGroupId.Value, 0);
			var electiveOccupiedSlots = group.Sum(a => occupiedBySubject.GetValueOrDefault(a.SubjectId, 0));
			var avgHealth = group.Any() ? (int)group.Average(a => a.SetupHealthScore) : 0;
			var firstPrimary = first.Teachers.FirstOrDefault(t => t.TeacherType == SubjectTeacherType.Primary);

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
				WeeklyPeriods = first.WeeklyPeriods,
				PrimaryTeacherId = firstPrimary?.TeacherId.ToString(),
				PrimaryTeacherName = firstPrimary != null
					? teacherNamesDict.GetValueOrDefault(firstPrimary.TeacherId)
					: null,
				LastUpdatedAt = first.LastModificationTime ?? first.CreationTime,
				IsAiSuggested = first.IsAiSuggested,
				IsCopiedFromPreviousYear = first.IsCopiedFromPreviousYear,
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

		await SynchronizeWeeklyPeriodsAndRecalculateHealthAsync(groupAssignments, assignment.StandardId);

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
					WeeklyPeriods = a.WeeklyPeriods,
					DifficultyLevel = a.DifficultyLevel,
					SubjectGroup = a.SubjectGroup,
					IsAiSuggested = a.IsAiSuggested,
					IsCopiedFromPreviousYear = a.IsCopiedFromPreviousYear,
					SetupHealthScore = a.SetupHealthScore,
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

		var usageParts = new System.Collections.Generic.List<string>();

		// 1. Timetable Slots
		var timetables = await timetableRepository.GetByStandardIdAsync(assignment.StandardId);
		var timetableSlotSubjectsCount = timetables
			.SelectMany(t => t.Slots)
			.SelectMany(s => s.Subjects)
			.Count(ss => subjectIds.Contains(ss.SubjectId));
		var timetableSlotElectivesCount = assignment.ElectiveGroupId.HasValue
			? timetables.SelectMany(t => t.Slots).Count(s => s.ElectiveGroupId == assignment.ElectiveGroupId.Value)
			: 0;
		var totalTimetableSlots = timetableSlotSubjectsCount + timetableSlotElectivesCount;
		if (totalTimetableSlots > 0)
			usageParts.Add($"{totalTimetableSlots} timetable slot(s)");

		// 2. Exams
		var examCount = await examRepository.CountAsync(e =>
			e.StandardId == assignment.StandardId &&
			e.ExamSubjects.Any(es => subjectIds.Contains(es.SubjectId) || (assignment.ElectiveGroupId.HasValue && es.SubjectId == assignment.ElectiveGroupId.Value)));
		if (examCount > 0)
			usageParts.Add($"{examCount} exam(s)");

		// 3. Homeworks
		var homeworkCount = await homeworkRepository.CountAsync(h =>
			h.StandardId == assignment.StandardId &&
			subjectIds.Contains(h.SubjectId));
		if (homeworkCount > 0)
			usageParts.Add($"{homeworkCount} homework(s)");

		// 4. Assignments
		var learningTaskCount = await learningTaskAssignmentRepository.CountAsync(a =>
			a.StandardId == assignment.StandardId &&
			subjectIds.Contains(a.SubjectId));
		if (learningTaskCount > 0)
			usageParts.Add($"{learningTaskCount} assignment(s)");

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
				s.EducationMediumId == academicMediumId
			);
			var electiveGroups = await electiveGroupRepository.GetListAsync(eg =>
				eg.AcademicMediumId == academicMediumId
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
					Subjects = electiveGroup.Subjects.Select(es => new CoreElectiveSubjectDetailDto
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

		await SynchronizeWeeklyPeriodsAndRecalculateHealthAsync(new List<AssignedSubject> { assignment }, assignment.StandardId);

		var missing = new List<string>();
		if (string.IsNullOrWhiteSpace(assignment.SubjectCode)) missing.Add("Subject code is missing");
		if (!assignment.Teachers.Any(t => t.TeacherType == SubjectTeacherType.Primary)) missing.Add("Primary teacher is not assigned");
		if (string.IsNullOrWhiteSpace(assignment.LearningGoals)) missing.Add("Learning goals are missing");
		if (string.IsNullOrWhiteSpace(assignment.Description)) missing.Add("Description is missing");
		if (assignment.Chapters.Count == 0) missing.Add("No syllabus chapters added");
		else if (assignment.Chapters.Count < 3) missing.Add("Add at least 3 chapters for a complete syllabus");
		if (assignment.WeeklyPeriods == 0) missing.Add("Weekly periods not set");
		if (assignment.Documents.Count == 0) missing.Add("No documents attached");

		return new SetupHealthDto
		{
			AssignedSubjectId = assignment.Id,
			SubjectName = assignment.Subject?.Name,
			HealthScore = assignment.SetupHealthScore,
			MissingItems = missing
		};
	}

	public async Task<PublishReadinessDto> GetPublishReadinessAsync(Guid classId)
	{
		var query = await assignmentRepository.WithDetailsAsync(a => a.Teachers, a => a.Documents, a => a.Chapters, a => a.Subject);
		var assignments = await AsyncExecuter.ToListAsync(query.Where(a => a.StandardId == classId));

		await SynchronizeWeeklyPeriodsAndRecalculateHealthAsync(assignments, classId);

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

		// Persist recalculated scores
		await assignmentRepository.UpdateManyAsync(assignments, autoSave: true);

		var subjectsWithIssues = issues.DistinctBy(i => i.SubjectId).Count();
		var overallHealth = assignments.Count > 0
			? (int)assignments.Average(a => a.SetupHealthScore)
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
			OverallHealthScore = assignments.Count > 0 ? (int)assignments.Average(a => a.SetupHealthScore) : 0
		};
	}

	public async Task ArchiveAsync(Guid assignmentId)
	{
		var assignment = await assignmentRepository.GetAsync(assignmentId);
		await EnsureAssignmentRemovalAllowedAsync(assignment);
		assignment.ChangeStatus(SubjectStatus.Archived);
		await assignmentRepository.UpdateAsync(assignment, autoSave: true);
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
		if (!assignment.Teachers.Any(t => t.TeacherType == SubjectTeacherType.Primary)) blockers.Add("Primary teacher is missing.");
		if (string.IsNullOrWhiteSpace(assignment.LearningGoals)) blockers.Add("Learning goals are missing.");
		if (string.IsNullOrWhiteSpace(assignment.Description)) blockers.Add("Description is missing.");
		if (assignment.Chapters.Count == 0) blockers.Add("No syllabus chapters added.");
		if (assignment.WeeklyPeriods <= 0) blockers.Add("Weekly periods are not configured.");

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
}
