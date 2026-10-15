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
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Common;
using ShikshaMitra.TenantSettings.Divisions;

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
	IRepository<TeacherDiary, Guid> teacherDiaryRepository,
	IClassTeacherAccessService classTeacherAccessService,
	IRepository<Division, Guid> divisionRepository,
	IRepository<Employee, Guid> employeeRepository,
	IRepository<TeacherSubjectAllocation, Guid> teacherSubjectAllocationRepository,
	IReportingManagerAccessService reportingManagerAccessService,
	SubjectTeacherReassignmentManager teacherReassignmentManager,
	SubjectScheduleBreakdownQueryService scheduleBreakdownQueryService,
	SubjectAssignmentTeacherEligibilityManager teacherEligibilityManager
) : ApplicationService, ISubjectAssignmentAppService
{
	public async Task CreateAsync(
		Guid academicMediumId,
		CreateUpdateSubjectAssignmentDto createSubjectAssignmentDto
	)
	{
		try
		{
			NormalizeUnifiedTeacherAssignments(createSubjectAssignmentDto.Subjects, []);
			EnsureSubjectAssignmentInputIsConsistent(createSubjectAssignmentDto);
			await EnsureNewTeacherAssignmentsAreActiveAsync(createSubjectAssignmentDto.Subjects, []);
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
				await EnsureCurrentUserCanAccessAssignmentAsync(
					assignedSubject,
					operation: "subject-assignment-create");
				assignedSubjects.Add(assignedSubject);
			}

			await EnsureTeachersBelongToClassAsync(createSubjectAssignmentDto.ClassId, createSubjectAssignmentDto.Subjects);
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
		string? teacherSearch = null,
		Guid? divisionId = null
	)
	{
		var query = await assignmentRepository.WithDetailsAsync(a => a.Chapters, a => a.Teachers);
		var hasTenantWideScope = await userScopeAccessService.CurrentUserHasTenantWideScopeAsync();
		var hasClassTeacherAccess = await HasClassTeacherAccessAsync();
		var reportingEmployeeIds = CurrentUser.Id.HasValue
			? await reportingManagerAccessService.GetReporteeEmployeeIdsAsync(CurrentUser.Id.Value)
			: [];
		Guid? currentEmployeeId = null;

		// AssignedSubjectTeacher.TeacherId is an HR Employee ID, not an identity User ID.
		// A subject teacher may read their own assignments even when they have no broader
		// class/division scope; all other records still use the normal scope filter.
		if (!hasTenantWideScope)
		{
			currentEmployeeId = await GetCurrentEmployeeIdAsync();
			var eligibleTeacherIds = reportingEmployeeIds.ToHashSet();
			if (currentEmployeeId.HasValue)
			{
				eligibleTeacherIds.Add(currentEmployeeId.Value);
			}
			var allocatedSubjectIds = new List<Guid>();
			if (eligibleTeacherIds.Count > 0)
			{
				// Teacher Assignment stores the effective class/division/subject allocation
				// separately from the optional teacher list on AssignedSubject. Both sources
				// must grant read access to the assigned subject.
				var allocationQuery = await teacherSubjectAllocationRepository.GetQueryableAsync();
				allocatedSubjectIds = await AsyncExecuter.ToListAsync(
					allocationQuery
						.Where(allocation =>
							allocation.StandardId == classId &&
							(!divisionId.HasValue || allocation.DivisionId == divisionId.Value) &&
							allocation.Teachers.Any(teacher =>
								Enumerable.Contains(eligibleTeacherIds, teacher.TeacherId)))
						.Select(allocation => allocation.SubjectId)
						.Distinct());
			}

			if (!hasClassTeacherAccess)
			{
				query = query.Where(assignment =>
					assignment.Teachers.Any(teacher =>
						Enumerable.Contains(eligibleTeacherIds, teacher.TeacherId)) ||
					Enumerable.Contains(allocatedSubjectIds, assignment.SubjectId));
			}
			else
			{
				// A teacher can simultaneously own a class and teach a subject in another
				// class. Class-teacher access and subject-teacher access are additive; a
				// class-teacher assignment must not hide explicitly allocated subjects.
				var classTeacherQuery = await ApplyCurrentUserAssignmentScopeAsync(query);
				var classTeacherAssignmentIds = await AsyncExecuter.ToListAsync(
					classTeacherQuery.Select(assignment => assignment.Id));

				query = query.Where(assignment =>
					Enumerable.Contains(classTeacherAssignmentIds, assignment.Id) ||
					assignment.Teachers.Any(teacher =>
						Enumerable.Contains(eligibleTeacherIds, teacher.TeacherId)) ||
					Enumerable.Contains(allocatedSubjectIds, assignment.SubjectId));
			}
		}
		else
		{
			query = await ApplyCurrentUserAssignmentScopeAsync(query);
		}

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
		var assignments = await AsyncExecuter.ToListAsync(query);
		var weeklySlot = await weeklyLectureSlotRepository.FindByStandardIdAsync(classId);
		var readMetrics = BuildSubjectAssignmentReadMetrics(assignments, weeklySlot);

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
		var usage = await LoadAssignmentListUsageAsync(classId, subjectIds, electiveGroupIds, timetables);

		// Bulk-load every assigned teacher once for the flat teacher list and legacy primary fields.
		var allAssignmentTeacherIds = assignments
			.SelectMany(a => a.Teachers)
			.Select(t => t.TeacherId)
			.Distinct()
			.ToList();
		var teacherNamesDict = allAssignmentTeacherIds.Count > 0
			? (await userRepository.GetListByIdsAsync(allAssignmentTeacherIds))
				.ToDictionary(u => u.Id, u => $"{u.Name} {u.Surname}".Trim())
			: new Dictionary<Guid, string>();
		var teacherDisplayNamesDict = allAssignmentTeacherIds.Count > 0
			? (await employeeRepository.GetListAsync(employee => allAssignmentTeacherIds.Contains(employee.Id)))
				.ToDictionary(
					employee => employee.Id,
					employee => string.IsNullOrWhiteSpace(employee.EmployeeCode)
						? employee.EmployeeName
						: $"{employee.EmployeeCode} - {employee.EmployeeName}")
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
			var teacherNames = coreAssignment.Teachers
				.Select(teacher => teacherDisplayNamesDict.GetValueOrDefault(teacher.TeacherId)
					?? teacherNamesDict.GetValueOrDefault(teacher.TeacherId))
				.Where(name => !string.IsNullOrWhiteSpace(name))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();
			var usageParts = BuildAssignmentUsageParts(usage,
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
				TeacherNames = teacherNames,
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
			var teacherNames = group
				.SelectMany(assignment => assignment.Teachers)
				.Select(teacher => teacherDisplayNamesDict.GetValueOrDefault(teacher.TeacherId)
					?? teacherNamesDict.GetValueOrDefault(teacher.TeacherId))
				.Where(name => !string.IsNullOrWhiteSpace(name))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();
			var subjectIdsInGroup = group.Select(a => a.SubjectId).ToList();
			var usageParts = BuildAssignmentUsageParts(usage,
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
				TeacherNames = teacherNames,
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
					a.TeacherNames.Concat(a.PrimaryTeacherNames).Any(name =>
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
		await EnsureCurrentUserCanAccessAssignmentAsync(assignment, operation: "subject-assignment-read");

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

		// AssignedSubjectTeacher stores HR employee IDs. Resolve employees first,
		// then their linked identity users, so profile data is joined using the
		// correct identifier domain without introducing per-teacher queries.
		var allTeacherIds = groupAssignments
			.SelectMany(a => a.Teachers)
			.Select(t => t.TeacherId)
			.Distinct()
			.ToList();
		var employeesById = allTeacherIds.Count > 0
			? (await employeeRepository.GetListAsync(employee => allTeacherIds.Contains(employee.Id)))
				.ToDictionary(employee => employee.Id)
			: new Dictionary<Guid, Employee>();
		var identityUserIds = employeesById.Values
			.Where(employee => employee.UserId.HasValue)
			.Select(employee => employee.UserId!.Value)
			.Concat(allTeacherIds)
			.Distinct()
			.ToList();
		var identityUsersById = identityUserIds.Count > 0
			? (await userRepository.GetListByIdsAsync(identityUserIds)).ToDictionary(user => user.Id)
			: new Dictionary<Guid, IdentityUser>();
		var experienceAsOf = DateOnly.FromDateTime(Clock.Now);

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
				var teachers = a.Teachers
					.OrderBy(teacher => teacher.TeacherType)
					.ThenBy(teacher => teacher.TeacherId)
					.Select(teacher => BuildTeacherInfo(
						teacher.TeacherId,
						employeesById,
						identityUsersById,
						experienceAsOf))
					.ToList();
				return new SubjectAssignmentDetailResponseDto
				{
					AssignedSubjectId = a.Id,
					SubjectId = a.SubjectId,
					SubjectName = a.Subject?.Name,
					SubjectCode = a.SubjectCode,
					Color = a.Subject?.Color,
					SubjectType = a.SubjectType,
					ElectiveGroupId = a.ElectiveGroupId,
					Teachers = teachers,
					PrimaryTeacher = primaryTeacherEntry == null
						? null
						: BuildTeacherInfo(
							primaryTeacherEntry.TeacherId,
							employeesById,
							identityUsersById,
							experienceAsOf),
					SecondaryTeachers = secondaryTeacherEntries
						.Select(teacher => BuildTeacherInfo(
							teacher.TeacherId,
							employeesById,
							identityUsersById,
							experienceAsOf))
						.ToList(),
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

	public async Task<SubjectScheduleBreakdownDto> GetScheduleBreakdownAsync(Guid assignmentId)
	{
		var query = await assignmentRepository.WithDetailsAsync(assignment => assignment.Teachers);
		var assignment = await AsyncExecuter.FirstOrDefaultAsync(
			query.Where(candidate => candidate.Id == assignmentId))
			?? throw new Volo.Abp.Domain.Entities.EntityNotFoundException(
				typeof(AssignedSubject), assignmentId);

		await EnsureCurrentUserCanAccessAssignmentAsync(
			assignment,
			operation: "subject-assignment-read");

		if (await userScopeAccessService.CurrentUserHasTenantWideScopeAsync())
		{
			return await scheduleBreakdownQueryService.GetAsync(assignment);
		}

		var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
		var allowedTeacherIds = new HashSet<Guid>();
		if (CurrentUser.Id.HasValue)
		{
			allowedTeacherIds.UnionWith(
				await reportingManagerAccessService.GetReporteeEmployeeIdsAsync(CurrentUser.Id.Value));
		}
		var currentEmployeeId = await GetCurrentEmployeeIdAsync();
		if (currentEmployeeId.HasValue)
		{
			allowedTeacherIds.Add(currentEmployeeId.Value);
		}

		return await scheduleBreakdownQueryService.GetAsync(
			assignment,
			scope.DivisionIds,
			allowedTeacherIds);
	}

	private static TeacherInfoDto BuildTeacherInfo(
		Guid teacherId,
		IReadOnlyDictionary<Guid, Employee> employeesById,
		IReadOnlyDictionary<Guid, IdentityUser> identityUsersById,
		DateOnly experienceAsOf)
	{
		employeesById.TryGetValue(teacherId, out var employee);
		var identityUserId = employee?.UserId ?? teacherId;
		identityUsersById.TryGetValue(identityUserId, out var identityUser);
		var employeeName = employee?.EmployeeName;

		return new TeacherInfoDto
		{
			Id = teacherId.ToString(),
			Name = !string.IsNullOrWhiteSpace(employeeName)
				? employeeName
				: $"{identityUser?.Name} {identityUser?.Surname}".Trim(),
			EmployeeCode = employee?.EmployeeCode,
			Email = !string.IsNullOrWhiteSpace(employee?.Email)
				? employee.Email
				: identityUser?.Email,
			Experience = FormatExperience(employee, experienceAsOf)
		};
	}

	private static string? FormatExperience(Employee? employee, DateOnly asOf)
	{
		if (employee == null)
		{
			return null;
		}

		var periods = employee.WorkExperiences
			.Where(experience => experience.FromDate.HasValue)
			.Select(experience => (
				Start: experience.FromDate!.Value,
				End: experience.ToDate.HasValue && experience.ToDate.Value < asOf
					? experience.ToDate.Value
					: asOf))
			.ToList();
		if (employee.DateOfJoining.HasValue)
		{
			periods.Add((employee.DateOfJoining.Value, asOf));
		}

		var validPeriods = periods.Where(period => period.End >= period.Start).ToList();
		if (validPeriods.Count == 0)
		{
			return null;
		}

		var totalMonths = validPeriods.Sum(period => GetCompletedMonths(period.Start, period.End));
		if (totalMonths == 0)
		{
			return "Less than 1 month";
		}

		var years = totalMonths / 12;
		var months = totalMonths % 12;
		var parts = new List<string>();
		if (years > 0)
		{
			parts.Add($"{years} {(years == 1 ? "year" : "years")}");
		}
		if (months > 0)
		{
			parts.Add($"{months} {(months == 1 ? "month" : "months")}");
		}

		return string.Join(" ", parts);
	}

	private static int GetCompletedMonths(DateOnly start, DateOnly end)
	{
		var months = ((end.Year - start.Year) * 12) + end.Month - start.Month;
		if (end.Day < start.Day)
		{
			months--;
		}

		return Math.Max(months, 0);
	}

	public async Task<SubjectAssignmentDto> UpdateAsync(
		Guid assignmentId,
		Guid academicMediumId,
		CreateUpdateSubjectAssignmentDto input
	)
	{
		// 1. Identify the group being updated (with navigation properties loaded)
		var baseQuery = await assignmentRepository.WithDetailsAsync(a => a.Teachers, a => a.Documents);

		var existingEntity = await AsyncExecuter.FirstOrDefaultAsync(
			baseQuery.Where(a => a.Id == assignmentId)
		) ?? throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(AssignedSubject), assignmentId);
		await EnsureCurrentUserCanAccessAssignmentAsync(existingEntity, operation: "subject-assignment-update");

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

		NormalizeUnifiedTeacherAssignments(input.Subjects, existingGroup);
		EnsureSubjectAssignmentInputIsConsistent(input);
		await EnsureNewTeacherAssignmentsAreActiveAsync(input.Subjects, existingGroup);
		await EnsureTeachersBelongToClassAsync(existingEntity.StandardId, input.Subjects);

		// 2. Process updates
		// We match existing subjects with input subjects by SubjectId
		var subjectsToDelete = new List<AssignedSubject>();
		var teacherChanges = new List<SubjectTeacherChange>();
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
				var previousPrimaryTeacherId = existingSubject.Teachers
					.FirstOrDefault(teacher => teacher.TeacherType == SubjectTeacherType.Primary)?.TeacherId;
				var previousStandardId = existingSubject.StandardId;

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
				existingSubject.ClearCopiedFromPreviousYearMarker();

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
				if (previousStandardId == existingSubject.StandardId)
				{
					teacherChanges.Add(new SubjectTeacherChange(existingSubject, previousPrimaryTeacherId));
				}
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
				await EnsureCurrentUserCanAccessAssignmentAsync(newSubject, operation: "subject-assignment-create");
				await assignmentRepository.InsertAsync(newSubject);
				existingGroup.Add(newSubject); // Add to local list for return mapping
			}
		}

		await teacherReassignmentManager.SynchronizeAsync(teacherChanges);

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
		await EnsureCurrentUserCanAccessAssignmentAsync(assignment, operation: "subject-assignment-status");

		if (!Enum.IsDefined(typeof(SubjectStatus), input.Status))
		{
			throw new UserFriendlyException("Invalid status value");
		}

		var status = (SubjectStatus) input.Status;
		var assignments = assignment.ElectiveGroupId.HasValue
			? await AsyncExecuter.ToListAsync(query.Where(a =>
				a.StandardId == assignment.StandardId &&
				a.ElectiveGroupId == assignment.ElectiveGroupId))
			: new List<AssignedSubject> { assignment };

		foreach (var groupAssignment in assignments)
		{
			EnsureSubjectAssignmentStatusTransitionAllowed(groupAssignment, status);
		}

		if (status == SubjectStatus.Published)
		{
			await SynchronizeWeeklyPeriodsAndRecalculateHealthAsync(assignments, assignment.StandardId);
			foreach (var groupAssignment in assignments)
			{
				EnsureSubjectAssignmentReadyToPublish(groupAssignment);
			}
		}

		foreach (var groupAssignment in assignments)
		{
			groupAssignment.ChangeStatus(status);
			if (status == SubjectStatus.Published)
			{
				groupAssignment.PublishedAt = DateTimeOffset.UtcNow;
				groupAssignment.ClearCopiedFromPreviousYearMarker();
			}
		}

		await assignmentRepository.UpdateManyAsync(assignments, autoSave: true);
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

			foreach (var assignment in assignments)
			{
				await EnsureCurrentUserCanAccessAssignmentAsync(assignment, operation: "subject-assignment-publish");
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
				assignment.ClearCopiedFromPreviousYearMarker();
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
		await EnsureCurrentUserCanAccessAssignmentAsync(assignment, operation: "subject-assignment-delete");

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

		var query = await assignmentRepository.GetQueryableAsync();
		var assignedSubjects = await AsyncExecuter.ToListAsync(
			query.Where(a => a.Teachers.Any(t => t.TeacherId == teacherId && t.TeacherType == SubjectTeacherType.Primary))
				.OrderBy(a => a.Id)
				.Select(a => new TeacherSubjectSummaryDto
				{
					AssignedSubjectId = a.Id,
					SubjectName = a.Subject != null ? a.Subject.Name : null,
					ClassName = a.Standard != null ? a.Standard.Name : null,
					WeeklyPeriods = a.WeeklyPeriods
				})
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
			AssignedSubjects = assignedSubjects
		};
	}

	public async Task<SubjectKpiDto> GetKpiAsync(Guid classId)
	{
		var query = await assignmentRepository.GetQueryableAsync();
		var summary = await AsyncExecuter.FirstOrDefaultAsync(query.Where(assignment => assignment.StandardId == classId)
			.GroupBy(_ => 1)
			.Select(group => new
			{
				Total = group.Count(),
				Core = group.Count(assignment => assignment.SubjectType == SubjectType.Core),
				Elective = group.Count(assignment => assignment.SubjectType == SubjectType.Elective),
				Optional = group.Count(assignment => assignment.SubjectType == SubjectType.Optional),
				WithTeacher = group.Count(assignment => assignment.Teachers.Any(teacher => teacher.TeacherType == SubjectTeacherType.Primary)),
				Published = group.Count(assignment => assignment.Status == SubjectStatus.Published),
				Draft = group.Count(assignment => assignment.Status == SubjectStatus.Draft),
				Health = group.Average(assignment => assignment.SetupHealthScore)
			}));
		if (summary == null) return new SubjectKpiDto();

		return new SubjectKpiDto
		{
			TotalSubjects = summary.Total,
			CoreSubjects = summary.Core,
			ElectiveSubjects = summary.Elective,
			OptionalSubjects = summary.Optional,
			SubjectsWithTeacher = summary.WithTeacher,
			SubjectsMissingTeacher = summary.Total - summary.WithTeacher,
			PublishedSubjects = summary.Published,
			DraftSubjects = summary.Draft,
			OverallHealthScore = (int)summary.Health
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
			a => a.Documents);

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

		// An elective group is stored as one AssignedSubject row per constituent
		// subject. Older/corrupted copied groups can contain only the first row,
		// while the group definition still contains every constituent subject.
		// Expand that definition here so copying the group repairs the missing
		// assignment rows instead of reproducing the incomplete group.
		var sourceElectiveGroupIds = sourceAssignments
			.Where(a => a.ElectiveGroupId.HasValue)
			.Select(a => a.ElectiveGroupId!.Value)
			.Distinct()
			.ToList();
		var sourceElectiveGroups = sourceElectiveGroupIds.Count == 0
			? new List<ElectiveGroup>()
			: await AsyncExecuter.ToListAsync(
				(await electiveGroupRepository.WithDetailsAsync(g => g.Subjects))
					.Where(g => sourceElectiveGroupIds.Contains(g.Id)));
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

		foreach (var group in sourceElectiveGroups)
		{
			foreach (var component in group.Subjects)
			{
				if (requestedSubjectIds is { Count: > 0 } && !requestedSubjectIds.Contains(component.SubjectId))
				{
					continue;
				}

				if (targetBySubjectId.ContainsKey(component.SubjectId))
				{
					continue;
				}

				var sourceGroupAssignment = sourceAssignments.FirstOrDefault(a =>
					a.ElectiveGroupId == group.Id);
				if (sourceGroupAssignment == null)
				{
					continue;
				}

				var subjectName = component.Subject?.Name;
				var syntheticSource = new AssignedSubject(
					component.SubjectId,
					sourceGroupAssignment.TenantId,
					input.SourceClassId,
					component.SubjectId,
					$"AUTO-{component.SubjectId:N}",
					SubjectStatus.Draft,
					SubjectType.Elective,
					group.Id);

				var newAssignment = CreateCopiedSubject(syntheticSource, input.TargetClassId, input);
				copied.Add(newAssignment);
				targetBySubjectId[component.SubjectId] = newAssignment;
				logger.LogInformation(
					"Expanded missing elective subject {SubjectId} ({SubjectName}) while copying group {ElectiveGroupId}",
					component.SubjectId,
					subjectName,
					group.Id);
			}
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
			foreach (var document in target.Documents.Where(d => !d.ChapterId.HasValue).ToList())
			{
				target.Documents.Remove(document);
			}

			foreach (var document in source.Documents.Where(d => !d.ChapterId.HasValue))
			{
				target.AddDocument(guidGenerator.Create(), document.BlobName);
			}
		}

		// Chapters are maintained independently in Syllabus and are never copied with an allocation.
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

	private static void NormalizeUnifiedTeacherAssignments(
		IEnumerable<SubjectAssignmentDetailDto> subjects,
		IReadOnlyCollection<AssignedSubject> existingAssignments)
	{
		var existingBySubjectId = existingAssignments.ToDictionary(assignment => assignment.SubjectId);
		foreach (var subject in subjects)
		{
			if (subject.TeacherIds == null)
			{
				continue;
			}

			var suppliedTeacherIds = subject.TeacherIds
				.Where(teacherId => teacherId != Guid.Empty)
				.ToList();
			var duplicateTeacherIds = suppliedTeacherIds
				.GroupBy(teacherId => teacherId)
				.Where(group => group.Count() > 1)
				.Select(group => group.Key)
				.ToList();
			if (duplicateTeacherIds.Count > 0)
			{
				throw new BusinessException("SubjectAssignment:DuplicateTeacher")
					.WithData("SubjectId", subject.SubjectId)
					.WithData("TeacherIds", JsonSerializer.Serialize(duplicateTeacherIds));
			}

			existingBySubjectId.TryGetValue(subject.SubjectId, out var existingAssignment);
			var existingPrimaryTeacherId = existingAssignment?.Teachers
				.FirstOrDefault(teacher => teacher.TeacherType == SubjectTeacherType.Primary)
				?.TeacherId;
			var internalPrimaryTeacherId = existingPrimaryTeacherId.HasValue &&
				suppliedTeacherIds.Contains(existingPrimaryTeacherId.Value)
					? existingPrimaryTeacherId
					: suppliedTeacherIds.Cast<Guid?>().FirstOrDefault();

			subject.PrimaryTeacherId = internalPrimaryTeacherId;
			subject.SecondaryTeacherIds = suppliedTeacherIds
				.Where(teacherId => teacherId != internalPrimaryTeacherId)
				.ToList();
		}
	}

	private Task EnsureTeachersBelongToClassAsync(Guid standardId, IEnumerable<SubjectAssignmentDetailDto> subjects) =>
		teacherEligibilityManager.EnsureEligibleAsync(standardId, subjects.ToDictionary(
			subject => subject.SubjectId,
			subject => (subject.PrimaryTeacherId.HasValue ? new[] { subject.PrimaryTeacherId.Value } : Array.Empty<Guid>())
				.Concat(subject.SecondaryTeacherIds ?? []).Where(id => id != Guid.Empty).Distinct().ToList()));

	private async Task EnsureNewTeacherAssignmentsAreActiveAsync(
		IEnumerable<SubjectAssignmentDetailDto> subjects,
		IReadOnlyCollection<AssignedSubject> existingAssignments)
	{
		var existingBySubjectId = existingAssignments.ToDictionary(assignment => assignment.SubjectId);
		var newTeacherIds = new HashSet<Guid>();
		foreach (var subject in subjects)
		{
			existingBySubjectId.TryGetValue(subject.SubjectId, out var existingAssignment);
			var existingPrimaryTeacherId = existingAssignment?.Teachers
				.FirstOrDefault(teacher => teacher.TeacherType == SubjectTeacherType.Primary)?.TeacherId;
			if (subject.PrimaryTeacherId.HasValue &&
				subject.PrimaryTeacherId.Value != Guid.Empty &&
				subject.PrimaryTeacherId.Value != existingPrimaryTeacherId)
			{
				newTeacherIds.Add(subject.PrimaryTeacherId.Value);
			}

			var existingSecondaryTeacherIds = existingAssignment?.Teachers
				.Where(teacher => teacher.TeacherType == SubjectTeacherType.Secondary)
				.Select(teacher => teacher.TeacherId)
				.ToHashSet() ?? [];
			foreach (var secondaryTeacherId in subject.SecondaryTeacherIds ?? [])
			{
				if (secondaryTeacherId != Guid.Empty && !existingSecondaryTeacherIds.Contains(secondaryTeacherId))
				{
					newTeacherIds.Add(secondaryTeacherId);
				}
			}
		}

		if (newTeacherIds.Count == 0)
		{
			return;
		}

		var today = DateOnly.FromDateTime(Clock.Now);
		var activeTeacherIds = (await employeeRepository.GetListAsync(employee =>
			Enumerable.Contains(newTeacherIds, employee.Id) &&
			employee.EmployeeGroup == EmployeeGroupType.Teaching &&
			employee.EmployeeStatusType == EmployeeStatusType.Active &&
			(employee.DateOfJoining == null || employee.DateOfJoining <= today)))
			.Select(employee => employee.Id)
			.ToHashSet();
		var invalidTeacherIds = newTeacherIds
			.Where(teacherId => !activeTeacherIds.Contains(teacherId))
			.ToList();

		if (invalidTeacherIds.Count > 0)
		{
			throw new BusinessException(CurriculumErrorCodes.SubjectAssignmentTeacherMustBeActive)
				.WithData("TeacherIds", JsonSerializer.Serialize(invalidTeacherIds));
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

	private async Task<AssignmentListUsage> LoadAssignmentListUsageAsync(
		Guid standardId,
		List<Guid> subjectIds,
		List<Guid> electiveGroupIds,
		IReadOnlyCollection<ShikshaMitra.Academic.Timetable.Timetables.Timetable> timetables)
	{
		var subjectOrGroupIds = subjectIds.Concat(electiveGroupIds).Distinct().ToList();
		var examQuery = await examRepository.GetQueryableAsync();
		var examSubjects = await AsyncExecuter.ToListAsync(examQuery.Where(exam => exam.StandardId == standardId)
			.SelectMany(exam => exam.ExamSubjects, (exam, subject) => new { ExamId = exam.Id, subject.SubjectId })
			.Where(subject => subjectOrGroupIds.Contains(subject.SubjectId)).Distinct());
		var homeworkQuery = await homeworkRepository.GetQueryableAsync();
		var homeworkCounts = await AsyncExecuter.ToListAsync(homeworkQuery
			.Where(homework => homework.StandardId == standardId && subjectIds.Contains(homework.SubjectId))
			.GroupBy(homework => homework.SubjectId).Select(group => new { SubjectId = group.Key, Count = group.Count() }));
		var assignmentQuery = await learningTaskAssignmentRepository.GetQueryableAsync();
		var assignmentCounts = await AsyncExecuter.ToListAsync(assignmentQuery
			.Where(assignment => assignment.StandardId == standardId && subjectIds.Contains(assignment.SubjectId))
			.GroupBy(assignment => assignment.SubjectId).Select(group => new { SubjectId = group.Key, Count = group.Count() }));
		var slots = timetables.SelectMany(timetable => timetable.Slots).ToList();
		return new AssignmentListUsage(
			slots.SelectMany(slot => slot.Subjects).GroupBy(subject => subject.SubjectId).ToDictionary(group => group.Key, group => group.Count()),
			slots.Where(slot => slot.ElectiveGroupId.HasValue).GroupBy(slot => slot.ElectiveGroupId!.Value).ToDictionary(group => group.Key, group => group.Count()),
			examSubjects.ToLookup(subject => subject.SubjectId, subject => subject.ExamId),
			homeworkCounts.ToDictionary(group => group.SubjectId, group => group.Count),
			assignmentCounts.ToDictionary(group => group.SubjectId, group => group.Count));
	}

	private static List<string> BuildAssignmentUsageParts(
		AssignmentListUsage usage, IEnumerable<Guid> subjectIds, Guid? electiveGroupId, int chapterCount)
	{
		var ids = subjectIds.Distinct().ToList();
		var parts = new List<string>();
		if (chapterCount > 0) parts.Add($"{chapterCount} syllabus chapter(s)");
		var slots = ids.Sum(id => usage.TimetableSubjects.GetValueOrDefault(id)) +
			(electiveGroupId.HasValue ? usage.TimetableGroups.GetValueOrDefault(electiveGroupId.Value) : 0);
		if (slots > 0) parts.Add($"{slots} timetable slot(s)");
		var examSubjectIds = electiveGroupId.HasValue ? ids.Append(electiveGroupId.Value) : ids;
		var exams = examSubjectIds.SelectMany(id => usage.ExamsBySubject[id]).Distinct().Count();
		if (exams > 0) parts.Add($"{exams} exam(s)");
		var homeworks = ids.Sum(id => usage.Homeworks.GetValueOrDefault(id));
		if (homeworks > 0) parts.Add($"{homeworks} homework(s)");
		var assignments = ids.Sum(id => usage.Assignments.GetValueOrDefault(id));
		if (assignments > 0) parts.Add($"{assignments} assignment(s)");
		return parts;
	}

	private sealed record AssignmentListUsage(
		Dictionary<Guid, int> TimetableSubjects,
		Dictionary<Guid, int> TimetableGroups,
		ILookup<Guid, Guid> ExamsBySubject,
		Dictionary<Guid, int> Homeworks,
		Dictionary<Guid, int> Assignments);

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
		// Replacing a primary teacher transfers current unrecorded plans and timetable slots.
		// Historical/recorded lessons keep their author and must not prevent a staff change.
		if (inputSubject.PrimaryTeacherId.HasValue && inputSubject.PrimaryTeacherId != Guid.Empty)
		{
			var previousPrimary = existingSubject.Teachers
				.FirstOrDefault(teacher => teacher.TeacherType == SubjectTeacherType.Primary)?.TeacherId;
			removedTeacherIds.RemoveAll(teacherId => teacherId == previousPrimary);
		}
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

		return BuildSubjectAssignmentReadMetrics(assignments, await weeklyLectureSlotRepository.FindByStandardIdAsync(classId));
	}

	private static Dictionary<Guid, SubjectAssignmentReadMetrics> BuildSubjectAssignmentReadMetrics(
		IReadOnlyCollection<AssignedSubject> assignments, WeeklyLectureSlot? weeklySlot)
	{
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

	private async Task<IQueryable<AssignedSubject>> ApplyCurrentUserAssignmentScopeAsync(
		IQueryable<AssignedSubject> query)
	{
		if (!await HasClassTeacherAccessAsync())
		{
			return await userScopeAccessService.ApplyCurrentUserScopeAsync(query);
		}

		var standardIds = await GetCurrentUserClassTeacherStandardIdsAsync();
		return standardIds.Count == 0
			? query.Where(_ => false)
			: query.Where(assignment => standardIds.Contains(assignment.StandardId));
	}

	private async Task EnsureCurrentUserCanAccessAssignmentAsync(
		AssignedSubject assignment,
		string operation)
	{
		var hasTenantWideScope = await userScopeAccessService.CurrentUserHasTenantWideScopeAsync();
		var hasClassTeacherAccess = await HasClassTeacherAccessAsync();

		// Subject-teacher ownership remains valid even when the same employee also owns a
		// class. The list query combines these access paths, so detail reads must use the
		// same additive rule instead of treating class-teacher access as an exclusive mode.
		if (!hasTenantWideScope && operation == "subject-assignment-read")
		{
			var currentEmployeeId = await GetCurrentEmployeeIdAsync();
			if (currentEmployeeId.HasValue && await IsTeacherAssignedToSubjectAsync(assignment, currentEmployeeId.Value))
			{
				return;
			}

			if (CurrentUser.Id.HasValue)
			{
				var reporteeEmployeeIds = await reportingManagerAccessService.GetReporteeEmployeeIdsAsync(CurrentUser.Id.Value);
				foreach (var reporteeEmployeeId in reporteeEmployeeIds)
				{
					if (await IsTeacherAssignedToSubjectAsync(assignment, reporteeEmployeeId))
					{
						return;
					}
				}
			}
		}

		if (!hasClassTeacherAccess)
		{
			await userScopeAccessService.EnsureCurrentUserCanAccessAsync(assignment, operation: operation);
			return;
		}

		var standardIds = await GetCurrentUserClassTeacherStandardIdsAsync();
		if (!standardIds.Contains(assignment.StandardId))
		{
			throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		}
	}

	private async Task<HashSet<Guid>> GetCurrentUserClassTeacherStandardIdsAsync()
	{
		var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
		if (scope.IsAdmin || scope.HasTenantWideScope())
		{
			return (await divisionRepository.GetListAsync()).Select(division => division.StandardId).ToHashSet();
		}

		var divisions = await divisionRepository.GetListAsync(
			division => scope.DivisionIds.Contains(division.Id));
		return divisions.Select(division => division.StandardId).ToHashSet();
	}

	private Task<bool> HasClassTeacherAccessAsync() =>
		CurrentUser.Id.HasValue
			? classTeacherAccessService.HasAssignmentAsync(CurrentUser.Id.Value)
			: Task.FromResult(false);

	private async Task<Guid?> GetCurrentEmployeeIdAsync()
	{
		if (!CurrentUser.Id.HasValue)
		{
			return null;
		}

		var employee = await employeeRepository.FirstOrDefaultAsync(
			candidate => candidate.UserId == CurrentUser.Id.Value);
		return employee?.Id;
	}

	private async Task<bool> IsTeacherAssignedToSubjectAsync(
		AssignedSubject assignment,
		Guid employeeId)
	{
		if (assignment.Teachers.Any(teacher => teacher.TeacherId == employeeId))
		{
			return true;
		}

		return await teacherSubjectAllocationRepository.AnyAsync(allocation =>
			allocation.StandardId == assignment.StandardId &&
			allocation.SubjectId == assignment.SubjectId &&
			allocation.Teachers.Any(teacher => teacher.TeacherId == employeeId));
	}

	private sealed record SubjectAssignmentReadMetrics(int WeeklyPeriods, int SetupHealthScore)
	{
		public static SubjectAssignmentReadMetrics FromAssignment(AssignedSubject assignment)
		{
			return new SubjectAssignmentReadMetrics(assignment.WeeklyPeriods, assignment.SetupHealthScore);
		}
	}
}
