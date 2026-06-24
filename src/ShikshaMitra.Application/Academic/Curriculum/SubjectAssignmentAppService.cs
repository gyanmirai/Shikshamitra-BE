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
	IRepository<AssignmentEntity, Guid> learningTaskAssignmentRepository
) : ApplicationService, ISubjectAssignmentAppService
{
	public async Task CreateAsync(
		Guid academicMediumId,
		CreateUpdateSubjectAssignmentDto createSubjectAssignmentDto
	)
	{
		try
		{
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
						subject.Description
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
		var query = await assignmentRepository.GetQueryableAsync();

		query = query.Where(a => a.StandardId == classId);

		// Non-admin users only see subjects they are personally assigned to teach.
		if (!await userScopeAccessService.CurrentUserHasTenantWideScopeAsync())
		{
			var userId = CurrentUser.Id!.Value;
			query = query.Where(a => a.Teachers.Any(t => t.TeacherId == userId));
		}

		var assignments = await AsyncExecuter.ToListAsync(query);

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

		foreach (var coreAssignment in coreSubjects)
		{
			subjectsDict.TryGetValue(coreAssignment.SubjectId, out var subject);
			var totalSlots = weeklyLecturesBySubject.GetValueOrDefault(coreAssignment.SubjectId, 0);
			var occupiedSlots = occupiedBySubject.GetValueOrDefault(coreAssignment.SubjectId, 0);

			groupedAssignments.Add(new SubjectAssignmentGroupDto
			{
				Id = coreAssignment.Id,
				SubjectType = coreAssignment.SubjectType,
				Status = coreAssignment.Status,
				Color = subject?.Color,
				TotalSlots = totalSlots,
				OccupiedSlots = occupiedSlots,
				UnoccupiedSlots = totalSlots - occupiedSlots,
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
		var baseQuery = await assignmentRepository.WithDetailsAsync(a => a.Teachers, a => a.Documents, a => a.Subject);

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

		foreach (var inputSubject in input.Subjects)
		{
			processedSubjectIds.Add(inputSubject.SubjectId);
			var existingSubject = existingGroup.FirstOrDefault(x => x.SubjectId == inputSubject.SubjectId);

			if (existingSubject != null)
			{
				// Update existing
				existingSubject.SubjectCode = inputSubject.SubjectCode ?? existingSubject.SubjectCode;
				existingSubject.LearningGoals = inputSubject.LearningGoal;
				existingSubject.Prerequisites = inputSubject.Prerequisites;
				existingSubject.Description = inputSubject.Description;
				existingSubject.ChangeStatus(input.Status);
				existingSubject.StandardId = input.ClassId; // Allow moving standard?

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

				// Update specific fields like Elective Group ID if shifted?
				// For now assuming we keep them in the same group or the input defines the new group ID?
				// The input defines the structure.
				existingSubject.ElectiveGroupId = inputSubject.ElectiveGroupId;
				existingSubject.SubjectType = (inputSubject.ElectiveGroupId == null || inputSubject.ElectiveGroupId == Guid.Empty)
					? SubjectType.Core : SubjectType.Elective;

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
					inputSubject.Description
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

				await assignmentRepository.InsertAsync(newSubject);
				existingGroup.Add(newSubject); // Add to local list for return mapping
			}
		}

		// 3. Delete removed subjects
		foreach (var subject in existingGroup)
		{
			if (!processedSubjectIds.Contains(subject.SubjectId))
			{
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
		var assignment = await assignmentRepository.GetAsync(assignmentId);

		if (!Enum.IsDefined(typeof(SubjectStatus), input.Status))
		{
			throw new UserFriendlyException("Invalid status value");
		}

		var status = (SubjectStatus) input.Status;
		assignment.ChangeStatus(status);

		if (status == SubjectStatus.Published)
		{
			assignment.PublishedAt = DateTimeOffset.UtcNow;
		}

		await assignmentRepository.UpdateAsync(assignment, autoSave: true);
	}

	public async Task<PublishResponseDto> PublishAsync(List<Guid> assignmentIds)
	{
		try
		{
			var assignments = await assignmentRepository.GetListAsync(a =>
				assignmentIds.Contains(a.Id)
			);

			// We should probably find the GROUPS for these IDs and publish the whole groups?
			// Assumption: User selects items in UI, if they select an elective, do they select the whole group?
			// Usually yes. But let's be safe and publish what is requested.
			// If input is list of IDs, we publish those IDs.

			if (assignments.Count == 0)
			{
				throw new BusinessException("SubjectAssignment:NotFound")
					.WithData("AssignmentIds", JsonSerializer.Serialize(assignmentIds));
			}

			foreach (var assignment in assignments)
			{
				assignment.Status = SubjectStatus.Published;
				assignment.PublishedAt = DateTime.UtcNow;
			}

			await assignmentRepository.UpdateManyAsync(assignments, autoSave: true);

			return new PublishResponseDto
			{
				Status = SubjectStatus.Published,
				PublishedAt = DateTime.UtcNow
			};
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
			string name = assignment.ElectiveGroupId.HasValue
				? (await electiveGroupRepository.GetAsync(assignment.ElectiveGroupId.Value)).Name
				: (await subjectRepository.GetAsync(assignment.SubjectId)).Name;

			throw new BusinessException(CurriculumErrorCodes.DuplicateSubjectAssignment)
				.WithData("message", $"Cannot delete subject assignment '{name}' because it is in use: {string.Join(", ", usageParts)}. Please remove these usages first.");
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
}
