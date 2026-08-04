using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Exams.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using System;
using Volo.Abp;
using System.Linq;

using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Academic.Examination.Results;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Identity;
using ShikshaMitra.Academic.Examination.SeatAllocation;

namespace ShikshaMitra.Academic.Examination.Exams;

[RemoteService(false)]
public class ExamAppService(
	IExamRepository examRepository,
	IRepository<Subject, Guid> subjectRepository,
	IRepository<ExamType, Guid> examTypeRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<EducationMediumStage, Guid> educationMediumStageRepository,
	IRepository<Division, Guid> divisionRepository,
	IRepository<IdentityUser, Guid> identityUserRepository,
	IRepository<AssignedSubject, Guid> assignmentRepository,
	IRepository<ElectiveGroup, Guid> electiveGroupRepository,
	IRepository<StudentElectiveSelection, Guid> studentElectiveSelectionRepository,
	IRepository<ExamMark, Guid> examMarkRepository,
	IRepository<Student, Guid> studentRepository,
	IRepository<GradingScale, Guid> gradingScaleRepository,
	IRepository<ExamSubjectDivision, Guid> examSubjectDivisionRepository,
	IRepository<ExamMarksWorkflowState, Guid> examMarksWorkflowStateRepository,
	IRepository<ExamResultHeader, Guid> resultHeaderRepository,
	ExamManager examManager,
	IRepository<ExamSeatAllocation, Guid> examSeatAllocationRepository,
	IRepository<AcademicCalendar, Guid> calendarRepository,
	IRepository<AcademicCalendarDay, Guid> calendarDayRepository) : ApplicationService, IExamAppService
{
	private const string CalendarSyncDescriptionPrefix = "Auto-created from examination schedule. ExamId: ";

	public async Task<Guid> CreateBasicDetailsAsync(ExamBasicDetailsRequestDto input)
	{
		await EnsureElectiveAssignmentsReadyAsync(input.StandardId, input.DivisionIds, input.SubjectIds);
		await EnsureExamSettingsAvailableForFutureUseAsync(input.ExamTypeId, input.GradingScaleId);

		var exam = examManager.Create(
			input.ExamTypeId,
			input.ExamName,
			input.StandardId,
			input.DivisionIds,
			input.SubjectIds,
			input.GradingScaleId
		);

		await examRepository.InsertAsync(exam);
		return exam.Id;
	}

	public async Task UpdateBasicDetailsAsync(Guid examId, ExamBasicDetailsRequestDto input)
	{
		var exam = await examRepository.GetAsync(examId, includeDetails: true);
		EnsureExamSetupEditable(exam);
		await EnsureElectiveAssignmentsReadyAsync(input.StandardId, input.DivisionIds, input.SubjectIds);
		await EnsureExamSettingsAvailableForFutureUseAsync(input.ExamTypeId, input.GradingScaleId);

		examManager.UpdateBasicDetails(
			exam,
			input.ExamName,
			input.ExamTypeId,
			input.StandardId,
			input.DivisionIds,
			input.SubjectIds,
			input.GradingScaleId
		);

		await examRepository.UpdateAsync(exam);
	}

	public async Task<ExamBasicDetailsResponseDto> GetBasicDetailsAsync(Guid examId)
	{
		var exam = await examRepository.GetAsync(examId, includeDetails: true);

		return new ExamBasicDetailsResponseDto
		{
			ExamTypeId = exam.ExamTypeId,
			ExamName = exam.ExamName,
			StandardId = exam.StandardId,
			GradingScaleId = exam.GradingScaleId,
			DivisionIds = exam.ExamDivisions.Select(x => x.DivisionId).ToList(),
			SubjectIds = exam.ExamSubjects.Select(x => x.SubjectId).ToList()
		};
	}

	public async Task SaveScheduleAsync(Guid examId, ExamScheduleRequestDto input)
	{
		var exam = await examRepository.GetAsync(examId, includeDetails: true);
		EnsureExamSetupEditable(exam);
		await AttachMissingElectiveScheduleSubjectsAsync(exam, input);
		var normalizedSubjects = NormalizeScheduleRows(input.Subjects);
		await ValidateScheduleInputAsync(exam, normalizedSubjects);
		await EnsureScheduleGradingScalesAvailableForFutureUseAsync(input);

		await ValidateScheduleConflictsAsync(examId, exam, normalizedSubjects);

		var schedules = normalizedSubjects.Select(s => (
			s.SubjectId,
			s.ElectiveGroupId,
			s.ExamDate,
			s.StartTime,
			s.EndTime,
			s.TotalMarks,
			s.PassingMarks,
			s.Notes,
			s.GradeScalingId
		)).ToList();

		examManager.SaveSchedule(exam, schedules);

		await examRepository.UpdateAsync(exam);

		if (input.SyncToAcademicCalendar)
		{
			await SyncScheduleToAcademicCalendarAsync(exam, normalizedSubjects);
		}
	}

	public async Task<ExamScheduleResponseDto> GetScheduleAsync(Guid examId)
	{
		var exam = await examRepository.GetAsync(examId, includeDetails: true);

		var subjectIds = exam.ExamSubjects.Select(s => s.SubjectId).Distinct().ToList();

		// Fetch subject Name + Color
		var subjectDict = subjectIds.Count > 0
			? (await subjectRepository.GetListAsync(s => subjectIds.Contains(s.Id))).ToDictionary(s => s.Id)
			: new Dictionary<Guid, Subject>();

		// Case 1: SubjectId is an ElectiveGroupId directly (Combined scheduling mode)
		var directElectiveGroupIds = subjectIds.Where(id => !subjectDict.ContainsKey(id)).ToList();
		var directElectiveGroupDict = directElectiveGroupIds.Count > 0
			? (await electiveGroupRepository.GetListAsync(eg => directElectiveGroupIds.Contains(eg.Id))).ToDictionary(eg => eg.Id)
			: new Dictionary<Guid, ElectiveGroup>();

		// For Case 1 groups, fetch member subjects (name + color only — schedule is on the group row)
		var combinedGroupSubjectsMap = new Dictionary<Guid, List<ExamElectiveGroupSubjectDto>>();
		if (directElectiveGroupDict.Count > 0)
		{
			var groupSubjectAssignments = await assignmentRepository.GetListAsync(
				a => a.StandardId == exam.StandardId && directElectiveGroupIds.Contains(a.ElectiveGroupId!.Value));
			var groupSubjectIds = groupSubjectAssignments.Select(a => a.SubjectId).Distinct().ToList();
			var groupSubjectDict = groupSubjectIds.Count > 0
				? (await subjectRepository.GetListAsync(s => groupSubjectIds.Contains(s.Id))).ToDictionary(s => s.Id)
				: new Dictionary<Guid, Subject>();

			combinedGroupSubjectsMap = groupSubjectAssignments
				.Where(a => a.ElectiveGroupId.HasValue)
				.GroupBy(a => a.ElectiveGroupId!.Value)
				.ToDictionary(
					g => g.Key,
					g => g.Select(a =>
					{
						groupSubjectDict.TryGetValue(a.SubjectId, out var s);
						return new ExamElectiveGroupSubjectDto { SubjectId = a.SubjectId, SubjectName = s?.Name, Color = s?.Color };
					}).ToList()
				);
		}

		// Fetch assignment data for individual-subject rows → SubjectCode + ElectiveGroupId
		var assignedDict = (await assignmentRepository.GetListAsync(
				a => a.StandardId == exam.StandardId && subjectIds.Contains(a.SubjectId)))
			.GroupBy(a => a.SubjectId)
			.ToDictionary(g => g.Key, g => g.First());

		// Elective group metadata for Case 2 (Separate mode)
		var separateElectiveGroupIds = assignedDict.Values
			.Where(a => a.ElectiveGroupId.HasValue)
			.Select(a => a.ElectiveGroupId!.Value)
			.Distinct()
			.ToList();
		var electiveGroupDict = separateElectiveGroupIds.Count > 0
			? (await electiveGroupRepository.GetListAsync(eg => separateElectiveGroupIds.Contains(eg.Id))).ToDictionary(eg => eg.Id)
			: new Dictionary<Guid, ElectiveGroup>();

		var selectedElectiveSubjectIdsByGroup = await GetSelectedElectiveSubjectIdsByGroupAsync(
			exam.StandardId,
			exam.ExamDivisions.Select(x => x.DivisionId).ToList(),
			separateElectiveGroupIds);
		var selectedSeparateSubjectIds = selectedElectiveSubjectIdsByGroup.Values
			.SelectMany(x => x)
			.Distinct()
			.ToList();
		if (selectedSeparateSubjectIds.Count > 0)
		{
			var missingSelectedSubjectIds = selectedSeparateSubjectIds
				.Where(id => !subjectDict.ContainsKey(id))
				.ToList();
			if (missingSelectedSubjectIds.Count > 0)
			{
				var selectedSubjects = await subjectRepository.GetListAsync(s => missingSelectedSubjectIds.Contains(s.Id));
				foreach (var subject in selectedSubjects)
				{
					subjectDict[subject.Id] = subject;
				}
			}
		}

		var result = new List<ExamScheduleSubjectDto>();

		// ── Case 1: Combined mode (SubjectId = ElectiveGroupId) ────────────────────────────────
		foreach (var s in exam.ExamSubjects.Where(s => directElectiveGroupDict.ContainsKey(s.SubjectId)))
		{
			directElectiveGroupDict.TryGetValue(s.SubjectId, out var eg);
			result.Add(new ExamScheduleSubjectDto
			{
				SubjectId = s.SubjectId,
				SubjectName = null,
				SubjectCode = null,
				Color = null,
				ElectiveGroupId = s.SubjectId,
				ElectiveGroupName = eg?.Name,
				ElectiveGroupColor = eg?.Color,
				ElectiveGroupSubjects = combinedGroupSubjectsMap.GetValueOrDefault(s.SubjectId, []),
				ExamDate = s.ExamDate,
				StartTime = s.StartTime,
				EndTime = s.EndTime,
				TotalMarks = s.TotalMarks,
				PassingMarks = s.PassingMarks,
				Notes = s.Notes,
				GradeScalingId = s.GradeScalingId
			});
		}

		// ── Case 2: Individual subject rows ───────────────────────────────────────────────────
		var individualSubjects = exam.ExamSubjects
			.Where(s => subjectDict.ContainsKey(s.SubjectId))
			.ToList();

		// Case 2a: Separate-mode elective subjects — group by ElectiveGroupId, nest per-subject schedules
		var emittedSubjectIds = new HashSet<Guid>();
		var separateGroups = individualSubjects
			.Where(s => assignedDict.TryGetValue(s.SubjectId, out var a) && a.ElectiveGroupId.HasValue)
			.GroupBy(s => assignedDict[s.SubjectId].ElectiveGroupId!.Value);

		foreach (var group in separateGroups)
		{
			var egId = group.Key;
			electiveGroupDict.TryGetValue(egId, out var eg);

			selectedElectiveSubjectIdsByGroup.TryGetValue(egId, out var selectedSubjectIds);

			var scheduledSubjectsById = group.ToDictionary(s => s.SubjectId);
			var groupSubjectIds = selectedSubjectIds?.ToList() ?? group.Select(s => s.SubjectId).ToList();

			var groupSubjects = groupSubjectIds
				.Select(subjectId =>
				{
					subjectDict.TryGetValue(subjectId, out var subject);
					scheduledSubjectsById.TryGetValue(subjectId, out var scheduledSubject);
					return new ExamElectiveGroupSubjectDto
					{
						SubjectId = subjectId,
						SubjectName = subject?.Name,
						Color = subject?.Color,
						ExamDate = scheduledSubject?.ExamDate,
						StartTime = scheduledSubject?.StartTime,
						EndTime = scheduledSubject?.EndTime,
						TotalMarks = scheduledSubject?.TotalMarks,
						PassingMarks = scheduledSubject?.PassingMarks,
						Notes = scheduledSubject?.Notes,
						GradeScalingId = scheduledSubject?.GradeScalingId
					};
				}).ToList();

			result.Add(new ExamScheduleSubjectDto
			{
				SubjectId = egId,
				SubjectName = null,
				SubjectCode = null,
				Color = null,
				ElectiveGroupId = egId,
				ElectiveGroupName = eg?.Name,
				ElectiveGroupColor = eg?.Color,
				ElectiveGroupSubjects = groupSubjects,
				// Group-level schedule fields are null in separate mode — schedules live inside ElectiveGroupSubjects
				ExamDate = null,
				StartTime = null,
				EndTime = null,
				TotalMarks = null,
				PassingMarks = null,
				Notes = null,
				GradeScalingId = null
			});

			foreach (var s in group) emittedSubjectIds.Add(s.SubjectId);
		}

		// Case 2b: Regular (non-elective) subjects
		foreach (var s in individualSubjects.Where(s => !emittedSubjectIds.Contains(s.SubjectId)))
		{
			subjectDict.TryGetValue(s.SubjectId, out var subject);
			assignedDict.TryGetValue(s.SubjectId, out var assigned);
			result.Add(new ExamScheduleSubjectDto
			{
				SubjectId = s.SubjectId,
				SubjectName = subject?.Name,
				SubjectCode = assigned?.SubjectCode,
				Color = subject?.Color,
				ElectiveGroupId = null,
				ElectiveGroupName = null,
				ElectiveGroupColor = null,
				ElectiveGroupSubjects = [],
				ExamDate = s.ExamDate,
				StartTime = s.StartTime,
				EndTime = s.EndTime,
				TotalMarks = s.TotalMarks,
				PassingMarks = s.PassingMarks,
				Notes = s.Notes,
				GradeScalingId = s.GradeScalingId
			});
		}

		return new ExamScheduleResponseDto { Subjects = result };
	}

	public async Task AssignRoomsAsync(Guid examId, ExamRoomAssignmentRequestDto input)
	{
		var exam = await examRepository.GetAsync(examId, includeDetails: true);
		EnsureExamSetupEditable(exam);

		if (!input.IsRoomAssigned)
		{
			var toDelete = exam.ExamSubjects
				.SelectMany(s => s.RoomAssignments)
				.ToList();
			if (toDelete.Count > 0)
				await examSubjectDivisionRepository.DeleteManyAsync(toDelete, autoSave: true);
			return;
		}

		var assignments = input.Subjects.Select(s => (
			s.SubjectId,
			s.Divisions.Select(d => (
				d.DivisionId,
				d.RoomIds,
				d.Capacity
			)).ToList()
		)).ToList();

		examManager.AssignRooms(exam, input.IsRoomAssigned, assignments);

		await examRepository.UpdateAsync(exam);
	}

	public async Task<ExamRoomAssignmentResponseDto> GetRoomAssignmentsAsync(Guid examId)
	{
		var exam = await examRepository.GetAsync(examId, includeDetails: true);

		// Assumption: IsRoomAssigned flag is not persisted in Exam entity based on current knowledge.
		// Returning true if there are any room assignments or false otherwise, or strictly following what was decided.
		// Actually, the Manager didn't persist IsRoomAssigned. So I can't return it accurately if I don't store it.
		// However, I can check if any subject has room assignments.
		bool hasAssignments = exam.ExamSubjects.Any(s => s.RoomAssignments.Any());

		return new ExamRoomAssignmentResponseDto
		{
			IsRoomAssigned = hasAssignments,
			Subjects = exam.ExamSubjects.Select(s => new ExamRoomSubjectDto
			{
				SubjectId = s.SubjectId,
				ExamDate = s.ExamDate,
				Divisions = s.RoomAssignments.Select(d => new ExamRoomDivisionDto
				{
					DivisionId = d.DivisionId,
					Capacity = d.Capacity,
					RoomIds = d.RoomIds
				}).ToList()
			}).ToList()
		};
	}

	public async Task<List<ExamSubjectResponseDto>> GetSubjectsAsync(Guid examId)
	{
		var exam = await examRepository.GetAsync(examId, includeDetails: true);

		var subjectIds = exam.ExamSubjects.Select(x => x.SubjectId).ToList();
		var subjects = await subjectRepository.GetListAsync(x => subjectIds.Contains(x.Id));
		var subjectDict = subjects.ToDictionary(x => x.Id, x => x);

		return exam.ExamSubjects.Select(es =>
		{
			var subject = subjectDict.GetValueOrDefault(es.SubjectId);
			return new ExamSubjectResponseDto
			{
				ExamSubjectId = es.Id,
				SubjectId = es.SubjectId,
				SubjectName = subject?.Name,
				SubjectColor = subject?.Color,
				Notes = es.Notes,
				ExamDate = es.ExamDate,
				StartTime = es.StartTime,
				EndTime = es.EndTime,
				TotalMarks = es.TotalMarks,
				PassingMarks = es.PassingMarks
			};
		}).ToList();
	}

	public async Task<List<QuestionPaperExamLookupDto>> GetQuestionPaperLookupAsync(QuestionPaperExamLookupInput input)
	{
		if (input.AcademicMediumId == Guid.Empty || input.StandardId == Guid.Empty || input.SubjectId == Guid.Empty)
		{
			return [];
		}

		var schedulableSubjectIds = new HashSet<Guid> { input.SubjectId };
		var electiveGroupIdsBySubject = await GetElectiveGroupIdsBySubjectAsync(input.StandardId, [input.SubjectId]);
		if (electiveGroupIdsBySubject.TryGetValue(input.SubjectId, out var electiveGroupId))
		{
			schedulableSubjectIds.Add(electiveGroupId);
		}

		var examQuery = await examRepository.WithDetailsAsync();
		var standardQuery = await standardRepository.GetQueryableAsync();
		var stageQuery = await educationMediumStageRepository.GetQueryableAsync();
		var examTypeQuery = await examTypeRepository.GetQueryableAsync();

		var query = from exam in examQuery
					join standard in standardQuery on exam.StandardId equals standard.Id
					join stage in stageQuery on standard.EducationMediumStageId equals stage.Id
					join examType in examTypeQuery on exam.ExamTypeId equals examType.Id
					where stage.EducationMediumId == input.AcademicMediumId &&
						exam.StandardId == input.StandardId &&
						exam.ExamSubjects.Any(subject =>
							schedulableSubjectIds.Contains(subject.SubjectId) &&
							subject.ExamDate.HasValue &&
							subject.StartTime.HasValue &&
							subject.EndTime.HasValue)
					select new { exam, examType };

		var rows = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.exam.ExamName));

		return rows
			.Where(row => GetEffectiveScheduleStatus(row.exam) != ExamStatus.Completed)
			.SelectMany(row => row.exam.ExamSubjects
				.Where(subject =>
					schedulableSubjectIds.Contains(subject.SubjectId) &&
					subject.ExamDate.HasValue &&
					subject.StartTime.HasValue &&
					subject.EndTime.HasValue)
				.Select(subject => new QuestionPaperExamLookupDto
				{
					ExamId = row.exam.Id,
					ExamSubjectId = subject.Id,
					ExamName = row.exam.ExamName,
					ExamTypeName = row.examType.Name,
					ExamDate = subject.ExamDate!.Value,
					StartTime = subject.StartTime,
					EndTime = subject.EndTime,
					TotalMarks = subject.TotalMarks,
					PassingMarks = subject.PassingMarks,
					Status = GetEffectiveScheduleStatus(row.exam)
				}))
			.OrderBy(row => row.ExamDate)
			.ThenBy(row => row.StartTime)
			.ThenBy(row => row.ExamName)
			.ToList();
	}

	public async Task DeleteAsync(Guid examId)
	{
		var exam = await examRepository.GetAsync(examId);

		if (exam.Status != ExamStatus.Pending)
		{
			throw new UserFriendlyException("Completed exams cannot be deleted");
		}

		// Block deletion if the exam is in use
		var usageParts = new System.Collections.Generic.List<string>();

		var marksCount = await examMarkRepository.CountAsync(m => m.ExamId == examId);
		if (marksCount > 0)
			usageParts.Add($"{marksCount} exam mark(s)");

		var workflowStateCount = await examMarksWorkflowStateRepository.CountAsync(w => w.ExamId == examId);
		if (workflowStateCount > 0)
			usageParts.Add($"{workflowStateCount} exam marks workflow state(s)");

		var seatAllocationCount = await examSeatAllocationRepository.CountAsync(s => s.ExamId == examId);
		if (seatAllocationCount > 0)
			usageParts.Add($"{seatAllocationCount} seat allocation(s)");

		if (usageParts.Count > 0)
		{
			throw new BusinessException(ExamErrorCodes.ExamIsInUse)
				.WithData("message", $"Cannot delete exam '{exam.ExamName}' because it is in use: {string.Join(", ", usageParts)}. Please remove these usages first.");
		}

		await examRepository.DeleteAsync(exam);
	}

	public async Task ChangeStatusAsync(Guid examId, ExamStatus status)
	{
		var exam = await examRepository.GetAsync(examId, includeDetails: true);
		await EnsureValidExamStatusTransitionAsync(exam, status);
		exam.ChangeStatus(status);
		await examRepository.UpdateAsync(exam);
	}

	public async Task<ExamListResultDto> GetListAsync(GetExamsInput input)
	{
		var examQuery = await examRepository.WithDetailsAsync();
		var standardQuery = await standardRepository.GetQueryableAsync();
		var stageQuery = await educationMediumStageRepository.GetQueryableAsync();
		var examTypeQuery = await examTypeRepository.GetQueryableAsync();

		var baseQuery = from exam in examQuery
						join standard in standardQuery on exam.StandardId equals standard.Id
						join stage in stageQuery on standard.EducationMediumStageId equals stage.Id
						join examType in examTypeQuery on exam.ExamTypeId equals examType.Id
						where stage.EducationMediumId == input.AcademicMediumId
						select new { exam, standard, examType };

		var query = baseQuery;

		if (input.StandardId.HasValue)
		{
			query = query.Where(x => x.exam.StandardId == input.StandardId.Value);
		}

		if (input.DivisionId.HasValue)
		{
			query = query.Where(x => x.exam.ExamDivisions.Any(d => d.DivisionId == input.DivisionId.Value));
		}

		if (input.SubjectId.HasValue)
		{
			query = query.Where(x => x.exam.ExamSubjects.Any(s => s.SubjectId == input.SubjectId.Value));
		}

		if (input.GradeEntryPublishedOnly == true && input.DivisionId.HasValue)
		{
			var workflowQuery = await examMarksWorkflowStateRepository.GetQueryableAsync();
			query = query.Where(x => 
				x.exam.ExamSubjects.Any() &&
				x.exam.ExamSubjects.All(s => 
					workflowQuery.Any(w => 
						w.ExamId == x.exam.Id && 
						w.SubjectId == s.SubjectId && 
						w.DivisionId == input.DivisionId.Value && 
						w.Status == ExamMarksWorkflowStatus.Published
					)
				)
			);
		}

		var allRows = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.exam.CreationTime));
		var rowsWithEffectiveStatus = allRows
			.Select(x => new
			{
				x.exam,
				x.standard,
				x.examType,
				EffectiveStatus = GetEffectiveScheduleStatus(x.exam)
			})
			.ToList();

		var pendingCount = rowsWithEffectiveStatus.Count(x => x.EffectiveStatus == ExamStatus.Pending);
		var publishedCount = rowsWithEffectiveStatus.Count(x => x.EffectiveStatus == ExamStatus.Published);
		var completedCount = rowsWithEffectiveStatus.Count(x => x.EffectiveStatus == ExamStatus.Completed);

		var filteredRows = rowsWithEffectiveStatus
			.Where(x => x.EffectiveStatus == input.Status)
			.ToList();

		var totalCount = filteredRows.Count;

		var queryResult = filteredRows
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount)
			.ToList();

		// Fetch Divisions
		var allDivisionIds = queryResult.SelectMany(x => x.exam.ExamDivisions.Select(d => d.DivisionId)).Distinct().ToList();
		var divisions = await divisionRepository.GetListAsync(x => allDivisionIds.Contains(x.Id));
		var divisionDict = divisions.ToDictionary(x => x.Id, x => x.Name);

		// Fetch creator names
		var creatorIds = queryResult.Select(x => x.exam.CreatorId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
		var creatorUsers = await identityUserRepository.GetListAsync(x => creatorIds.Contains(x.Id));
		var creatorNameById = creatorUsers.ToDictionary(
			x => x.Id,
			x => !string.IsNullOrWhiteSpace(x.Name) ? x.Name! : !string.IsNullOrWhiteSpace(x.UserName) ? x.UserName! : x.Email ?? "Unknown");

		var listStandardIds = queryResult.Select(x => x.exam.StandardId).Distinct().ToList();
		var listSubjectIds = queryResult
			.SelectMany(x => x.exam.ExamSubjects.Select(subject => subject.SubjectId))
			.Distinct()
			.ToList();
		var electiveGroupByStandardAndSubject = await GetElectiveGroupIdsByStandardAndSubjectAsync(listStandardIds, listSubjectIds);

		var items = queryResult.Select(x =>
		{
			var startDate = GetExamStartDate(x.exam);
			var finalEndAt = GetExamFinalEndAt(x.exam);
			var subjectCount = x.exam.ExamSubjects.Count;
			var roomRequiredSubjects = GetRoomRequiredSubjects(x.exam, x.examType, electiveGroupByStandardAndSubject);
			var missingRoomAssignmentCount = roomRequiredSubjects
				.SelectMany(subject => x.exam.ExamDivisions.Select(division => new { subject, division }))
				.Count(row => !row.subject.RoomAssignments.Any(assignment => assignment.DivisionId == row.division.DivisionId));

			return new ExamTableItemDto
			{
				ExamId = x.exam.Id,
				ExamName = x.exam.ExamName,
				ExamTypeName = x.examType.Name,
				StandardName = x.standard.Name,
				CreatedBy = x.exam.CreatorId,
				CreatorName = x.exam.CreatorId.HasValue ? creatorNameById.GetValueOrDefault(x.exam.CreatorId.Value) : null,
				Status = x.EffectiveStatus,
				SubjectCount = subjectCount,
				StartDate = startDate,
				EndDate = finalEndAt,
				IsScheduleComplete = IsExamScheduleComplete(x.exam, x.examType, electiveGroupByStandardAndSubject),
				HasRoomAssignment = roomRequiredSubjects.Count > 0 && missingRoomAssignmentCount == 0,
					MissingRoomAssignmentCount = missingRoomAssignmentCount,
					IssueCount = 0,
					WarningCount = missingRoomAssignmentCount,
				LastUpdatedAt = x.exam.LastModificationTime ?? x.exam.CreationTime,
				Divisions = x.exam.ExamDivisions.Select(d => divisionDict.GetValueOrDefault(d.DivisionId) ?? string.Empty).OrderBy(name => name).ToList()
			};
		}).ToList();

		return new ExamListResultDto
		{
			TotalCount = totalCount,
			Items = items,
			PendingCount = pendingCount,
			PublishedCount = publishedCount,
			CompletedCount = completedCount
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.View)]
	public async Task<GetExamMarksResponseDto> GetMarksAsync(GetExamMarksInput input)
	{
		var exam = await examRepository.GetAsync(input.ExamId, includeDetails: true);

		var examSubject = exam.ExamSubjects.FirstOrDefault(s => s.SubjectId == input.SubjectId)
			?? throw new UserFriendlyException("Subject not found in this exam");
		await EnsureElectiveSubjectUsableForDivisionAsync(exam, input.SubjectId, input.DivisionId);

		// Students in this division (enrolled + approved), filtered by elective assignment when applicable.
		var allStudents = await GetEligibleStudentsForSubjectAsync(exam, input.SubjectId, input.DivisionId);

		// Apply search filter
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var q = input.Search.Trim().ToLower();
			allStudents = allStudents.Where(s =>
				($"{s.FirstName} {s.MiddleName} {s.LastName}").ToLower().Contains(q)
				|| (s.GrNo?.ToString() ?? string.Empty).Contains(q)
				|| (s.RollNo?.ToString() ?? string.Empty).Contains(q)).ToList();
		}

		allStudents = allStudents.OrderBy(s => s.RollNo).ToList();

		var totalCount = allStudents.Count;
		var totalPages = (int) Math.Ceiling(totalCount / (double) input.PageSize);
		var pagedStudents = allStudents.Skip((input.Page - 1) * input.PageSize).Take(input.PageSize).ToList();

		// Existing marks for this exam+subject+division
		var existingMarks = await examMarkRepository.GetListAsync(m =>
			m.ExamId == input.ExamId && m.SubjectId == input.SubjectId && m.DivisionId == input.DivisionId);
		var marksLookup = existingMarks.ToDictionary(m => m.StudentId);

		var isPublished = existingMarks.Any() && existingMarks.All(m => m.IsPublished);

		// Fetch the specific grading scale configured for this subject
		ICollection<GradeRange> gradeRanges = new List<GradeRange>();
		double scaleTotal = 100.0;
		if (examSubject.GradeScalingId.HasValue)
		{
			var gradingScales = await gradingScaleRepository.WithDetailsAsync(gs => gs.GradeRanges);
			var customScale = gradingScales.FirstOrDefault(gs => gs.Id == examSubject.GradeScalingId.Value);
			if (customScale != null)
			{
				gradeRanges = customScale.GradeRanges.OrderByDescending(gr => gr.MinMarks).ToList();
				if (double.TryParse(customScale.TotalMarks, out var parsedTotal))
				{
					scaleTotal = parsedTotal;
				}
			}
		}

		string? GetGrade(double? marks, double totalMarks)
		{
			if (marks == null || totalMarks == 0) return null;
			if (gradeRanges.Any())
			{
				var scaled = (marks.Value / totalMarks) * scaleTotal;
				return gradeRanges.FirstOrDefault(gr => scaled >= gr.MinMarks && scaled <= gr.MaxMarks)?.Grade;
			}
			var pct = (marks.Value / totalMarks) * 100;
			return pct >= 90 ? "A+" : pct >= 80 ? "A" : pct >= 70 ? "B+" : pct >= 60 ? "B"
				 : pct >= 50 ? "C+" : pct >= 40 ? "C" : pct >= 33 ? "D" : "F";
		}

		var items = pagedStudents.Select(student =>
		{
			marksLookup.TryGetValue(student.Id, out var mark);
			var assigned = mark != null;
			return new StudentExamMarksItemDto
			{
				StudentId = student.Id,
				StudentName = $"{student.FirstName} {student.MiddleName} {student.LastName}".Trim(),
				GrNo = student.GrNo?.ToString(),
				RollNo = student.RollNo?.ToString(),
				ObtainedMarks = mark?.MarksObtained,
				TotalMarks = examSubject.TotalMarks,
				PassingMarks = examSubject.PassingMarks,
				Grade = assigned ? GetGrade(mark!.MarksObtained, examSubject.TotalMarks ?? 0) : null,
				Feedback = mark?.Feedback,
				Status = assigned ? 1 : 0,
				AttendanceStatus = mark != null ? (int) mark.AttendanceStatus : (int) ExamAttendanceStatus.Present,
				GradeScalingId = examSubject.GradeScalingId
			};
		}).ToList();

		// Fetch workflow state for this exam/subject/division
		var workflowState = (await examMarksWorkflowStateRepository.GetListAsync(
			w => w.ExamId == input.ExamId && w.SubjectId == input.SubjectId && w.DivisionId == input.DivisionId))
			.FirstOrDefault();

		var workflowStatus = workflowState?.Status ?? ExamMarksWorkflowStatus.Draft;

		return new GetExamMarksResponseDto
		{
			ExamId = exam.Id,
			WorkflowStatus = (int) workflowStatus,
			IsDraft = workflowStatus == ExamMarksWorkflowStatus.Draft,
			IsSubmitted = workflowStatus == ExamMarksWorkflowStatus.Submitted,
			IsVerified = workflowStatus == ExamMarksWorkflowStatus.Verified,
			IsPublished = workflowStatus == ExamMarksWorkflowStatus.Published,
			IsMarksPublish = isPublished,
			CurrentPage = input.Page,
			PageSize = input.PageSize,
			TotalCount = totalCount,
			TotalPages = totalPages,
			Items = items
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.Add)]
	public async Task<UpdateStudentMarksResponseDto> UpdateStudentMarksAsync(Guid examId, Guid studentId, Guid subjectId, UpdateStudentMarksDto input)
	{
		var exam = await examRepository.GetAsync(examId, includeDetails: true);

		var examSubject = exam.ExamSubjects.FirstOrDefault(s => s.SubjectId == subjectId)
			?? throw new UserFriendlyException("Subject not found in this exam");

		// Verify student exists
		var student = await studentRepository.GetAsync(studentId);
		var divisionId = student.DivisionId
			?? throw new UserFriendlyException("Student is not assigned to a division.");
		EnsureDivisionBelongsToExam(exam, divisionId);
		await EnsureStudentCanTakeSubjectAsync(exam, subjectId, student);
		await EnsureMarksEditableAsync(examId, subjectId, divisionId);

		// Check if marks are already published
		var existing = (await examMarkRepository.GetListAsync(m =>
			m.ExamId == examId && m.StudentId == studentId && m.SubjectId == subjectId))
			.FirstOrDefault();

		if (existing?.IsPublished == true)
			throw new BusinessException("Academic:Exam:MarksAlreadyPublished", "Cannot update marks as they have already been published");

		var attendanceStatus = (ExamAttendanceStatus) input.AttendanceStatus;
		var isPresent = attendanceStatus == ExamAttendanceStatus.Present;

		// Validate: marks required only when student is Present
		if (isPresent && input.Marks == null)
			throw new UserFriendlyException("Obtained marks are required for present students.");

		var maxMarks = examSubject.TotalMarks ?? input.TotalMarks;
		if (input.Marks.HasValue && (input.Marks.Value < 0 || input.Marks.Value > maxMarks))
			throw new UserFriendlyException($"Obtained marks must be between 0 and {maxMarks}.");

		// Fetch the specific grading scale configured for this subject
		ICollection<GradeRange> gradeRanges = new List<GradeRange>();
		double scaleTotal = 100.0;
		if (examSubject.GradeScalingId.HasValue)
		{
			var gradingScales = await gradingScaleRepository.WithDetailsAsync(gs => gs.GradeRanges);
			var customScale = gradingScales.FirstOrDefault(gs => gs.Id == examSubject.GradeScalingId.Value);
			if (customScale != null)
			{
				gradeRanges = customScale.GradeRanges.OrderByDescending(gr => gr.MinMarks).ToList();
				if (double.TryParse(customScale.TotalMarks, out var parsedTotal))
				{
					scaleTotal = parsedTotal;
				}
			}
		}

		string? GetGrade(double marks, double totalMarks)
		{
			if (totalMarks == 0) return null;
			if (gradeRanges.Any())
			{
				var scaled = (marks / totalMarks) * scaleTotal;
				return gradeRanges.FirstOrDefault(gr => scaled >= gr.MinMarks && scaled <= gr.MaxMarks)?.Grade;
			}
			var pct = (marks / totalMarks) * 100;
			return pct >= 90 ? "A+" : pct >= 80 ? "A" : pct >= 70 ? "B+" : pct >= 60 ? "B"
				 : pct >= 50 ? "C+" : pct >= 40 ? "C" : pct >= 33 ? "D" : "F";
		}

		if (existing != null)
		{
			// Always update attendance first
			existing.UpdateAttendance(attendanceStatus);
			// Then overlay marks/feedback for present students
			if (isPresent)
				existing.Update(input.Marks!.Value, isAbsent: false, input.Feedback);
			await examMarkRepository.UpdateAsync(existing, autoSave: true);
		}
		else
		{
			var mark = new ExamMark(
				GuidGenerator.Create(),
				examId,
				studentId,
				subjectId,
				divisionId,
				marksObtained: isPresent ? input.Marks : null,
				isAbsent: !isPresent,
				feedback: isPresent ? input.Feedback : null,
				CurrentTenant.Id);
			mark.UpdateAttendance(attendanceStatus);
			await examMarkRepository.InsertAsync(mark, autoSave: true);
		}

		var grade = isPresent ? GetGrade(input.Marks!.Value, maxMarks) : null;

		return new UpdateStudentMarksResponseDto
		{
			Message = isPresent ? "Marks saved successfully." : "Attendance saved successfully.",
			Marks = isPresent ? input.Marks : null,
			TotalMarks = maxMarks,
			Grade = grade,
			Feedback = isPresent ? input.Feedback : null,
			AttendanceStatus = input.AttendanceStatus,
			GradeScalingId = examSubject.GradeScalingId
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.Add)]
	public async Task<PublishMarksResponseDto> PublishMarksAsync(Guid examId, Guid divisionId)
	{
		var exam = await examRepository.GetAsync(examId, includeDetails: true);
		EnsureDivisionBelongsToExam(exam, divisionId);

		var existingMarks = await examMarkRepository.GetListAsync(m =>
			m.ExamId == examId && m.DivisionId == divisionId);

		if (existingMarks.Count == 0)
			throw new UserFriendlyException("No marks found for this exam and division");

		if (existingMarks.All(m => m.IsPublished))
			throw new BusinessException("Academic:Exam:MarksAlreadyPublished", "Marks have already been published for this exam and division");

		var subjectIds = exam.ExamSubjects.Select(s => s.SubjectId).Distinct().ToList();
		var workflowStates = await examMarksWorkflowStateRepository.GetListAsync(w =>
			w.ExamId == examId &&
			w.DivisionId == divisionId &&
			subjectIds.Contains(w.SubjectId));

		var missingOrUnverifiedSubjectId = subjectIds.FirstOrDefault(subjectId =>
			workflowStates.FirstOrDefault(w => w.SubjectId == subjectId)?.Status != ExamMarksWorkflowStatus.Verified);
		if (missingOrUnverifiedSubjectId != Guid.Empty)
		{
			throw new UserFriendlyException("Marks can only be published after every subject workflow is verified.");
		}

		foreach (var subjectId in subjectIds)
		{
			var validation = await ValidateMarksAsync(examId, new MarksWorkflowActionInput
			{
				SubjectId = subjectId,
				DivisionId = divisionId
			});

			if (!validation.IsValid)
			{
				throw new UserFriendlyException("Please resolve blocking marks validation issues before publishing.");
			}
		}

		foreach (var mark in existingMarks)
			mark.Publish();

		await examMarkRepository.UpdateManyAsync(existingMarks, autoSave: true);

		foreach (var state in workflowStates)
		{
			state.TransitionTo(ExamMarksWorkflowStatus.Published);
		}

		await examMarksWorkflowStateRepository.UpdateManyAsync(workflowStates, autoSave: true);

		var studentCount = existingMarks.Select(m => m.StudentId).Distinct().Count();

		return new PublishMarksResponseDto
		{
			Message = "Marks published successfully for the division",
			ExamId = examId,
			DivisionId = divisionId,
			PublishedAt = DateTime.UtcNow,
			StudentsCount = studentCount
		};
	}

	/// <summary>
	/// Validates that no subject in the incoming schedule overlaps in time with any
	/// subject already scheduled in another exam for the same class and a shared division.
	/// Also detects exact-duplicate subject schedules (same subject, same time slot).
	/// </summary>
	private static void EnsureExamSetupEditable(Exam exam)
	{
		if (exam.Status != ExamStatus.Pending)
		{
			throw new UserFriendlyException("Published or completed exams cannot be edited. Create a correction workflow before changing setup, schedule, or rooms.");
		}
	}

	private async Task EnsureExamSettingsAvailableForFutureUseAsync(Guid examTypeId, Guid? gradingScaleId)
	{
		var examType = await examTypeRepository.FindAsync(examTypeId);
		if (examType == null || !IsSettingAvailableForFutureUse(examType.Status))
		{
			throw new BusinessException("AC:Exam:SettingsUnavailable")
				.WithData("message", "Selected exam type is archived or inactive and cannot be used for new exam setup.")
				.WithData("ExamTypeId", examTypeId);
		}

		if (!gradingScaleId.HasValue)
		{
			throw new BusinessException("ShikshaMitra.Exam:GradingScaleRequired")
				.WithData("ExamTypeId", examTypeId);
		}

		var gradingScale = await gradingScaleRepository.FindAsync(gradingScaleId.Value);
		if (gradingScale == null || !IsSettingAvailableForFutureUse(gradingScale.Status))
		{
			throw new BusinessException("AC:Exam:SettingsUnavailable")
				.WithData("message", "Selected grading scale is archived or inactive and cannot be used for new exam setup.")
				.WithData("GradingScaleId", gradingScaleId.Value);
		}
	}

	private async Task EnsureScheduleGradingScalesAvailableForFutureUseAsync(ExamScheduleRequestDto input)
	{
		var gradingScaleIds = input.Subjects
			.Where(subject => subject.GradeScalingId.HasValue)
			.Select(subject => subject.GradeScalingId!.Value)
			.Distinct()
			.ToList();

		if (gradingScaleIds.Count == 0)
		{
			return;
		}

		var gradingScales = await gradingScaleRepository.GetListAsync(scale => gradingScaleIds.Contains(scale.Id));
		var availableScaleIds = gradingScales
			.Where(scale => IsSettingAvailableForFutureUse(scale.Status))
			.Select(scale => scale.Id)
			.ToHashSet();

		var unavailableScaleId = gradingScaleIds.FirstOrDefault(id => !availableScaleIds.Contains(id));
		if (unavailableScaleId != Guid.Empty)
		{
			throw new BusinessException("AC:Exam:SettingsUnavailable")
				.WithData("message", "Selected subject grading scale is archived or inactive and cannot be used for new exam scheduling.")
				.WithData("GradingScaleId", unavailableScaleId);
		}
	}

	private static bool IsSettingAvailableForFutureUse(SettingStatus status)
	{
		return status is SettingStatus.Active or SettingStatus.SystemDefault;
	}

	private async Task EnsureValidExamStatusTransitionAsync(Exam exam, ExamStatus requestedStatus)
	{
		if (!Enum.IsDefined(requestedStatus))
		{
			throw new BusinessException("ShikshaMitra.Exam:InvalidStatus");
		}

		if (exam.Status == requestedStatus)
		{
			return;
		}

		switch (exam.Status, requestedStatus)
		{
			case (ExamStatus.Pending, ExamStatus.Published):
				await EnsureExamReadyToPublishAsync(exam);
				break;
			case (ExamStatus.Published, ExamStatus.Completed):
				await EnsureExamReadyToCompleteAsync(exam);
				break;
			default:
				throw new BusinessException("ShikshaMitra.Exam:InvalidStatusTransition")
					.WithData("CurrentStatus", exam.Status)
					.WithData("RequestedStatus", requestedStatus);
		}
	}

	private async Task EnsureExamReadyToPublishAsync(Exam exam)
	{
		var examType = await examTypeRepository.GetAsync(exam.ExamTypeId);

		if (exam.ExamDivisions.Count == 0)
		{
			throw new BusinessException("ShikshaMitra.Exam:DivisionRequired");
		}

		if (exam.ExamSubjects.Count == 0)
		{
			throw new BusinessException("ShikshaMitra.Exam:SubjectRequired");
		}

		var electiveGroupBySubjectId = await GetElectiveGroupIdsBySubjectAsync(
			exam.StandardId,
			exam.ExamSubjects.Select(subject => subject.SubjectId).ToList());

		foreach (var subject in exam.ExamSubjects.Where(subject => !electiveGroupBySubjectId.ContainsKey(subject.SubjectId)))
		{
			if (!subject.ExamDate.HasValue || !subject.StartTime.HasValue || !subject.EndTime.HasValue)
			{
				throw new BusinessException("ShikshaMitra.Exam:ScheduleIncomplete")
					.WithData("SubjectId", subject.SubjectId);
			}

			EnsureRequiredScheduleFieldsPresent(subject);
			ValidateSubjectScheduleValues(subject.SubjectId, subject.ExamDate, subject.StartTime, subject.EndTime, subject.TotalMarks, subject.PassingMarks);
			await EnsureExamDateAllowedAsync(exam.StandardId, exam.ExamDivisions.Select(x => x.DivisionId), subject.ExamDate.Value);
		}

		var electiveGroups = exam.ExamSubjects
			.Where(subject => electiveGroupBySubjectId.ContainsKey(subject.SubjectId))
			.GroupBy(subject => electiveGroupBySubjectId[subject.SubjectId]);

		foreach (var electiveGroup in electiveGroups)
		{
			var scheduledSubjects = electiveGroup
				.Where(subject => subject.ExamDate.HasValue || subject.StartTime.HasValue || subject.EndTime.HasValue)
				.ToList();

			if (scheduledSubjects.Count == 0)
			{
				throw new BusinessException("ShikshaMitra.Exam:ScheduleIncomplete")
					.WithData("ElectiveGroupId", electiveGroup.Key);
			}

			foreach (var subject in scheduledSubjects)
			{
				if (!subject.ExamDate.HasValue || !subject.StartTime.HasValue || !subject.EndTime.HasValue)
				{
					throw new BusinessException("ShikshaMitra.Exam:ScheduleIncomplete")
						.WithData("SubjectId", subject.SubjectId);
				}

				EnsureRequiredScheduleFieldsPresent(subject);
				ValidateSubjectScheduleValues(subject.SubjectId, subject.ExamDate, subject.StartTime, subject.EndTime, subject.TotalMarks, subject.PassingMarks);
				await EnsureExamDateAllowedAsync(exam.StandardId, exam.ExamDivisions.Select(x => x.DivisionId), subject.ExamDate.Value);
			}
		}
	}

	private async Task EnsureExamReadyToCompleteAsync(Exam exam)
	{
		var finalEndAt = GetExamFinalEndAt(exam);
		if (!finalEndAt.HasValue)
		{
			throw new BusinessException("ShikshaMitra.Exam:ScheduleIncomplete")
				.WithData("ExamId", exam.Id);
		}

		if (Clock.Now <= finalEndAt.Value)
		{
			throw new BusinessException("ShikshaMitra.Exam:CompletionBeforeScheduleEnd")
				.WithData("ExamId", exam.Id)
				.WithData("FinalSubjectEndAt", finalEndAt.Value.ToString("yyyy-MM-dd HH:mm"))
				.WithData("CurrentAt", Clock.Now.ToString("yyyy-MM-dd HH:mm"));
		}

		var divisionIds = exam.ExamDivisions.Select(x => x.DivisionId).ToList();
		foreach (var subject in exam.ExamSubjects)
		{
			foreach (var divisionId in divisionIds)
			{
				var state = (await examMarksWorkflowStateRepository.GetListAsync(w =>
					w.ExamId == exam.Id &&
					w.SubjectId == subject.SubjectId &&
					w.DivisionId == divisionId))
					.FirstOrDefault();

				if (state?.Status != ExamMarksWorkflowStatus.Published)
				{
					throw new BusinessException("ShikshaMitra.Exam:MarksNotPublished")
						.WithData("SubjectId", subject.SubjectId)
						.WithData("DivisionId", divisionId);
				}
			}
		}

		foreach (var divisionId in divisionIds)
		{
			var resultHeader = await resultHeaderRepository.FirstOrDefaultAsync(header =>
				header.ExamId == exam.Id &&
				header.DivisionId == divisionId);

			if (resultHeader?.Status is not (ExamResultStatus.Published or ExamResultStatus.Frozen))
			{
				throw new BusinessException("ShikshaMitra.Exam:ResultNotPublished")
					.WithData("DivisionId", divisionId);
			}
		}
	}

	private ExamStatus GetEffectiveScheduleStatus(Exam exam)
	{
		if (exam.Status != ExamStatus.Completed)
		{
			return exam.Status;
		}

		return HasExamScheduleEnded(exam, Clock.Now)
			? ExamStatus.Completed
			: ExamStatus.Published;
	}

	private static bool HasExamScheduleEnded(Exam exam, DateTime currentAt)
	{
		var finalEndAt = GetExamFinalEndAt(exam);
		return finalEndAt.HasValue && currentAt > finalEndAt.Value;
	}

	private static DateTime? GetExamStartDate(Exam exam)
	{
		var dates = exam.ExamSubjects
			.Where(subject => subject.ExamDate.HasValue)
			.Select(subject => subject.ExamDate!.Value.Date)
			.ToList();

		return dates.Count == 0 ? null : dates.Min();
	}

	private static DateTime? GetExamFinalEndAt(Exam exam)
	{
		var endTimes = exam.ExamSubjects
			.Where(subject => subject.ExamDate.HasValue)
			.Select(subject => subject.EndTime.HasValue
				? subject.ExamDate!.Value.Date.Add(subject.EndTime!.Value.TimeOfDay)
				: subject.ExamDate!.Value.Date)
			.ToList();

		return endTimes.Count == 0 ? null : endTimes.Max();
	}

	private static bool HasCompleteSchedule(ExamSubject subject)
	{
		var hasRequiredTime = subject.ExamDate.HasValue &&
			subject.StartTime.HasValue &&
			subject.EndTime.HasValue;
		var hasRequiredMarks = subject.TotalMarks.HasValue && subject.PassingMarks.HasValue;
		var hasRequiredGradeScale = subject.GradeScalingId.HasValue;

		return hasRequiredTime && hasRequiredMarks && hasRequiredGradeScale;
	}

	private static void EnsureRequiredScheduleFieldsPresent(ExamSubject subject)
	{
		if (!subject.TotalMarks.HasValue || !subject.PassingMarks.HasValue)
		{
			throw new BusinessException("ShikshaMitra.Exam:ScheduleIncomplete")
				.WithData("SubjectId", subject.SubjectId);
		}

		if (!subject.GradeScalingId.HasValue)
		{
			throw new BusinessException("ShikshaMitra.Exam:ScheduleIncomplete")
				.WithData("SubjectId", subject.SubjectId);
		}
	}

	private static bool IsExamScheduleComplete(
		Exam exam,
		ExamType examType,
		IReadOnlyDictionary<(Guid StandardId, Guid SubjectId), Guid> electiveGroupByStandardAndSubject)
	{
		if (exam.ExamSubjects.Count == 0)
		{
			return false;
		}

		var coreSubjects = exam.ExamSubjects
			.Where(subject => !electiveGroupByStandardAndSubject.ContainsKey((exam.StandardId, subject.SubjectId)))
			.ToList();

		if (coreSubjects.Any(subject => !HasCompleteSchedule(subject)))
		{
			return false;
		}

		var electiveGroups = exam.ExamSubjects
			.Where(subject => electiveGroupByStandardAndSubject.ContainsKey((exam.StandardId, subject.SubjectId)))
			.GroupBy(subject => electiveGroupByStandardAndSubject[(exam.StandardId, subject.SubjectId)]);

		return electiveGroups.All(group => group.Any(HasCompleteSchedule));
	}

	private static List<ExamSubject> GetRoomRequiredSubjects(
		Exam exam,
		ExamType examType,
		IReadOnlyDictionary<(Guid StandardId, Guid SubjectId), Guid> electiveGroupByStandardAndSubject)
	{
		return exam.ExamSubjects
			.Where(subject =>
				!electiveGroupByStandardAndSubject.ContainsKey((exam.StandardId, subject.SubjectId)) ||
				HasCompleteSchedule(subject))
			.ToList();
	}

	private async Task<Dictionary<Guid, Guid>> GetElectiveGroupIdsBySubjectAsync(Guid standardId, IReadOnlyCollection<Guid> subjectIds)
	{
		var standardMap = await GetElectiveGroupIdsByStandardAndSubjectAsync([standardId], subjectIds);
		return standardMap
			.Where(item => item.Key.StandardId == standardId)
			.ToDictionary(item => item.Key.SubjectId, item => item.Value);
	}

	private async Task<Dictionary<(Guid StandardId, Guid SubjectId), Guid>> GetElectiveGroupIdsByStandardAndSubjectAsync(
		IReadOnlyCollection<Guid> standardIds,
		IReadOnlyCollection<Guid> subjectIds)
	{
		var validStandardIds = standardIds.Where(id => id != Guid.Empty).Distinct().ToList();
		var validSubjectIds = subjectIds.Where(id => id != Guid.Empty).Distinct().ToList();
		if (validStandardIds.Count == 0 || validSubjectIds.Count == 0)
		{
			return [];
		}

		var assignments = await assignmentRepository.GetListAsync(a =>
			validStandardIds.Contains(a.StandardId) &&
			validSubjectIds.Contains(a.SubjectId) &&
			a.ElectiveGroupId.HasValue);

		return assignments
			.GroupBy(a => (a.StandardId, a.SubjectId))
			.ToDictionary(group => group.Key, group => group.First().ElectiveGroupId!.Value);
	}

	private static List<NormalizedExamSubjectSchedule> NormalizeScheduleRows(IEnumerable<ExamSubjectScheduleDto> rows)
	{
		return rows.Select(row =>
		{
			var examDate = row.ExamDate?.Date;
			return new NormalizedExamSubjectSchedule(
				row.SubjectId,
				row.ElectiveGroupId,
				row.Notes,
				examDate,
				CombineExamDateAndTime(examDate, row.StartTime),
				CombineExamDateAndTime(examDate, row.EndTime),
				row.TotalMarks,
				row.PassingMarks,
				row.GradeScalingId);
		}).ToList();
	}

	private static DateTime? CombineExamDateAndTime(DateTime? examDate, DateTimeOffset? time)
	{
		if (!examDate.HasValue || !time.HasValue)
		{
			return null;
		}

		return examDate.Value.Date.Add(time.Value.TimeOfDay);
	}

	private async Task ValidateScheduleInputAsync(Exam exam, List<NormalizedExamSubjectSchedule> rows)
	{
		var examSubjectIds = exam.ExamSubjects.Select(x => x.SubjectId).ToHashSet();
		rows = rows
			.Where(x => x.SubjectId != Guid.Empty || x.ElectiveGroupId.HasValue)
			.ToList();

		if (rows.Count == 0)
		{
			throw new BusinessException("ShikshaMitra.Exam:ScheduleSubjectRequired");
		}

		var emptyElectiveGroup = rows
			.Where(row => row.ElectiveGroupId.HasValue)
			.GroupBy(row => row.ElectiveGroupId!.Value)
			.FirstOrDefault(group => group.All(row =>
				!row.ExamDate.HasValue ||
				!row.StartTime.HasValue ||
				!row.EndTime.HasValue));

		if (emptyElectiveGroup != null)
		{
			throw new BusinessException("ShikshaMitra.Exam:ScheduleSubjectRequired")
				.WithData("ElectiveGroupId", emptyElectiveGroup.Key);
		}

		foreach (var row in rows)
		{
			var subjectId = row.ElectiveGroupId ?? row.SubjectId;
			if (!examSubjectIds.Contains(subjectId) && !examSubjectIds.Contains(row.SubjectId))
			{
				throw new BusinessException("ShikshaMitra.Exam:SubjectNotInExam")
					.WithData("SubjectId", subjectId);
			}

			ValidateSubjectScheduleValues(subjectId, row.ExamDate, row.StartTime, row.EndTime, row.TotalMarks, row.PassingMarks);
			if (row.ExamDate.HasValue)
			{
				await EnsureExamDateAllowedAsync(exam.StandardId, exam.ExamDivisions.Select(x => x.DivisionId), row.ExamDate.Value);
			}
		}

		EnsureNoIncomingScheduleOverlap(rows);
	}

	private static void ValidateSubjectScheduleValues(
		Guid subjectId,
		DateTime? examDate,
		DateTime? startTime,
		DateTime? endTime,
		double? totalMarks,
		double? passingMarks)
	{
		var hasAnyTime = examDate.HasValue || startTime.HasValue || endTime.HasValue;
		if (hasAnyTime && (!examDate.HasValue || !startTime.HasValue || !endTime.HasValue))
		{
			throw new BusinessException("ShikshaMitra.Exam:ScheduleTimeIncomplete")
				.WithData("SubjectId", subjectId);
		}

		if (startTime.HasValue && endTime.HasValue && startTime.Value >= endTime.Value)
		{
			throw new BusinessException("ShikshaMitra.Exam:InvalidScheduleTime")
				.WithData("SubjectId", subjectId);
		}

		if (totalMarks.HasValue && totalMarks.Value <= 0)
		{
			throw new BusinessException("ShikshaMitra.Exam:InvalidTotalMarks")
				.WithData("SubjectId", subjectId);
		}

		if (passingMarks.HasValue && (passingMarks.Value < 0 || (totalMarks.HasValue && passingMarks.Value > totalMarks.Value)))
		{
			throw new BusinessException("ShikshaMitra.Exam:InvalidPassingMarks")
				.WithData("SubjectId", subjectId);
		}
	}

	private static void EnsureNoIncomingScheduleOverlap(List<NormalizedExamSubjectSchedule> rows)
	{
		var scheduled = rows
			.Where(x => x.ExamDate.HasValue && x.StartTime.HasValue && x.EndTime.HasValue)
			.Select(x => new
			{
				x.SubjectId,
				x.ElectiveGroupId,
				Date = x.ExamDate!.Value.Date,
				Start = x.StartTime!.Value,
				End = x.EndTime!.Value
			})
			.ToList();

		for (var i = 0; i < scheduled.Count; i++)
		{
			for (var j = i + 1; j < scheduled.Count; j++)
			{
				var left = scheduled[i];
				var right = scheduled[j];
				if (left.Date == right.Date && left.Start < right.End && left.End > right.Start)
				{
					if (IsSameElectiveGroup(left.ElectiveGroupId, right.ElectiveGroupId))
					{
						continue;
					}

					throw new BusinessException("ShikshaMitra.Exam:ScheduleOverlap")
						.WithData("SubjectId", right.SubjectId)
						.WithData("ExamDate", left.Date.ToString("yyyy-MM-dd"));
				}
			}
		}
	}

	private static bool IsSameElectiveGroup(Guid? leftElectiveGroupId, Guid? rightElectiveGroupId)
	{
		return leftElectiveGroupId.HasValue &&
			rightElectiveGroupId.HasValue &&
			leftElectiveGroupId.Value == rightElectiveGroupId.Value;
	}

	private async Task EnsureExamDateAllowedAsync(Guid standardId, IEnumerable<Guid> divisionIds, DateTime examDate)
	{
		var date = examDate.Date;
		var divisionSet = divisionIds.ToHashSet();
		var blockedDays = await calendarDayRepository.GetListAsync(day =>
			day.Status == CalendarDayStatus.Active &&
			day.Date.Date == date &&
			(day.AppliesToAll ||
				(day.StandardId.HasValue && day.StandardId.Value == standardId) ||
				(day.DivisionId.HasValue && divisionSet.Contains(day.DivisionId.Value))));

		var blockingDay = blockedDays.FirstOrDefault(day => CalendarDayBlockingRules.BlocksExamSchedule(day.DayType));
		if (blockingDay != null)
		{
			throw new BusinessException("ShikshaMitra.Exam:ScheduleDateBlockedByCalendar")
				.WithData("ExamDate", date.ToString("yyyy-MM-dd"))
				.WithData("CalendarDay", blockingDay.Title)
				.WithData("DayType", blockingDay.DayType);
		}
	}

	private async Task SyncScheduleToAcademicCalendarAsync(Exam exam, IReadOnlyCollection<NormalizedExamSubjectSchedule> subjectSchedules)
	{
		var scheduledRows = subjectSchedules
			.Where(row => row.ExamDate.HasValue)
			.Select(row => (
				SubjectId: row.ElectiveGroupId ?? row.SubjectId,
				Date: row.ExamDate!.Value.Date,
				row.StartTime,
				row.EndTime
			))
			.GroupBy(row => new { row.SubjectId, row.Date, row.StartTime, row.EndTime })
			.Select(group => group.First())
			.ToList();

		if (scheduledRows.Count == 0)
		{
			return;
		}

		var minDate = scheduledRows.Min(row => row.Date);
		var maxDate = scheduledRows.Max(row => row.Date);
		var calendars = await calendarRepository.GetListAsync(calendar =>
			calendar.Status != CalendarStatus.Archived &&
			calendar.StartDate.Date <= maxDate &&
			calendar.EndDate.Date >= minDate);

		if (calendars.Count == 0)
		{
			throw new BusinessException("ShikshaMitra.Exam:AcademicCalendarNotFound")
				.WithData("StartDate", minDate.ToString("yyyy-MM-dd"))
				.WithData("EndDate", maxDate.ToString("yyyy-MM-dd"));
		}

		var syncMarker = $"{CalendarSyncDescriptionPrefix}{exam.Id}";
		var calendarIds = calendars.Select(calendar => calendar.Id).ToList();
		var existingSyncedDays = await calendarDayRepository.GetListAsync(day =>
			calendarIds.Contains(day.AcademicCalendarId) &&
			day.DayType == DayType.Exam &&
			day.Status == CalendarDayStatus.Active &&
			day.Description != null &&
			day.Description.Contains(syncMarker));

		foreach (var day in existingSyncedDays)
		{
			day.Cancel("Refreshed from examination schedule.");
			await calendarDayRepository.UpdateAsync(day);
		}

		var subjectNames = await ResolveExamScheduleSubjectNamesAsync(scheduledRows.Select(row => row.SubjectId).Distinct().ToList());

		foreach (var row in scheduledRows)
		{
			var calendar = calendars
				.Where(candidate => candidate.StartDate.Date <= row.Date && candidate.EndDate.Date >= row.Date)
				.OrderByDescending(candidate => candidate.Status == CalendarStatus.Published)
				.ThenByDescending(candidate => candidate.IsDefault)
				.FirstOrDefault();

			if (calendar == null)
			{
				throw new BusinessException("ShikshaMitra.Exam:AcademicCalendarNotFound")
					.WithData("ExamDate", row.Date.ToString("yyyy-MM-dd"));
			}

			var subjectName = subjectNames.GetValueOrDefault(row.SubjectId, "Subject");
			var calendarDay = new AcademicCalendarDay(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				calendar.Id,
				row.Date,
				DayType.Exam,
				$"{exam.ExamName} ({subjectName})",
				syncMarker,
				appliesToAll: false,
				standardId: exam.StandardId,
				divisionId: null,
				holidayCategory: null,
				startTime: row.StartTime?.TimeOfDay,
				endTime: row.EndTime?.TimeOfDay);

			await calendarDayRepository.InsertAsync(calendarDay);
		}
	}

	private async Task<Dictionary<Guid, string>> ResolveExamScheduleSubjectNamesAsync(List<Guid> subjectIds)
	{
		var subjectNames = new Dictionary<Guid, string>();
		if (subjectIds.Count == 0)
		{
			return subjectNames;
		}

		var subjects = await subjectRepository.GetListAsync(subject => subjectIds.Contains(subject.Id));
		foreach (var subject in subjects)
		{
			subjectNames[subject.Id] = string.IsNullOrWhiteSpace(subject.DisplayName)
				? subject.Name
				: subject.DisplayName!;
		}

		var unresolvedIds = subjectIds.Where(id => !subjectNames.ContainsKey(id)).ToList();
		if (unresolvedIds.Count == 0)
		{
			return subjectNames;
		}

		var electiveGroups = await electiveGroupRepository.GetListAsync(group => unresolvedIds.Contains(group.Id));
		foreach (var group in electiveGroups)
		{
			subjectNames[group.Id] = group.Name;
		}

		return subjectNames;
	}

	private async Task EnsureMarksEditableAsync(Guid examId, Guid subjectId, Guid divisionId)
	{
		var state = (await examMarksWorkflowStateRepository.GetListAsync(w =>
			w.ExamId == examId &&
			w.SubjectId == subjectId &&
			w.DivisionId == divisionId))
			.FirstOrDefault();

		if (state != null)
		{
			EnsureMarksEditableState(state);
		}
	}

	private async Task EnsureElectiveAssignmentsReadyAsync(Guid standardId, IReadOnlyCollection<Guid> divisionIds, IReadOnlyCollection<Guid> subjectIds)
	{
		if (divisionIds.Count == 0 || subjectIds.Count == 0)
		{
			return;
		}

		var electiveAssignments = await assignmentRepository.GetListAsync(a =>
			a.StandardId == standardId &&
			subjectIds.Contains(a.SubjectId) &&
			a.ElectiveGroupId.HasValue);

		var electiveGroupIds = electiveAssignments
			.Select(a => a.ElectiveGroupId!.Value)
			.Distinct()
			.ToList();

		if (electiveGroupIds.Count == 0)
		{
			return;
		}

		var academicMediumId = await GetAcademicMediumIdForStandardAsync(standardId);
		var validDivisionIds = divisionIds.Where(id => id != Guid.Empty).Distinct().ToList();
		var students = await studentRepository.GetListAsync(s =>
			s.AcademicMediumId == academicMediumId &&
			s.StandardId == standardId &&
			s.DivisionId.HasValue &&
			validDivisionIds.Contains(s.DivisionId.Value) &&
			s.Status == StudentLifeCycleStatusEnum.Enrolled &&
			s.AdmissionApprovalStatus == StudentEnrollmentApprovalState.Approved &&
			!s.IsDeleted);

		if (students.Count == 0)
		{
			return;
		}

		var studentIds = students.Select(s => s.Id).ToList();
		var selections = await studentElectiveSelectionRepository.GetListAsync(s =>
			studentIds.Contains(s.StudentId) &&
			electiveGroupIds.Contains(s.ElectiveGroupId));

		foreach (var groupId in electiveGroupIds)
		{
			foreach (var divisionId in validDivisionIds)
			{
				var divisionStudentIds = students
					.Where(s => s.DivisionId == divisionId)
					.Select(s => s.Id)
					.ToList();

				if (divisionStudentIds.Count == 0)
				{
					continue;
				}

				var divisionSelections = selections
					.Where(s => s.ElectiveGroupId == groupId && divisionStudentIds.Contains(s.StudentId))
					.ToList();

				var isReady = divisionSelections.Count == divisionStudentIds.Count && divisionSelections.All(s => s.IsLocked);
				if (!isReady)
				{
					throw new UserFriendlyException("Elective subject assignment is pending for this class and division. Please assign every student and lock the elective subject selection before scheduling this elective subject.");
				}
			}
		}
	}

	private async Task AttachMissingElectiveScheduleSubjectsAsync(Exam exam, ExamScheduleRequestDto input)
	{
		var existingSubjectIds = exam.ExamSubjects.Select(s => s.SubjectId).ToHashSet();
		var missingElectiveRows = input.Subjects
			.Where(s => s.SubjectId != Guid.Empty &&
				s.ElectiveGroupId.HasValue &&
				!existingSubjectIds.Contains(s.SubjectId))
			.GroupBy(s => new { s.SubjectId, ElectiveGroupId = s.ElectiveGroupId!.Value })
			.Select(g => g.Key)
			.ToList();

		if (missingElectiveRows.Count == 0)
		{
			return;
		}

		var missingSubjectIds = missingElectiveRows.Select(x => x.SubjectId).Distinct().ToList();
		var electiveGroupIds = missingElectiveRows.Select(x => x.ElectiveGroupId).Distinct().ToList();
		var assignments = await assignmentRepository.GetListAsync(a =>
			a.StandardId == exam.StandardId &&
			missingSubjectIds.Contains(a.SubjectId) &&
			a.ElectiveGroupId.HasValue &&
			electiveGroupIds.Contains(a.ElectiveGroupId.Value));
		var assignmentKeys = assignments
			.Select(a => (a.SubjectId, ElectiveGroupId: a.ElectiveGroupId!.Value))
			.ToHashSet();
		var selectedSubjectIdsByGroup = await GetSelectedElectiveSubjectIdsByGroupAsync(
			exam.StandardId,
			exam.ExamDivisions.Select(x => x.DivisionId).ToList(),
			electiveGroupIds);

		foreach (var row in missingElectiveRows)
		{
			if (!assignmentKeys.Contains((row.SubjectId, row.ElectiveGroupId)))
			{
				continue;
			}

			if (!selectedSubjectIdsByGroup.TryGetValue(row.ElectiveGroupId, out var selectedSubjectIds) ||
				!selectedSubjectIds.Contains(row.SubjectId))
			{
				continue;
			}

			exam.AddSubject(new ExamSubject(GuidGenerator.Create(), exam.Id, row.SubjectId));
			existingSubjectIds.Add(row.SubjectId);
		}
	}

	private async Task<Dictionary<Guid, HashSet<Guid>>> GetSelectedElectiveSubjectIdsByGroupAsync(
		Guid standardId,
		IReadOnlyCollection<Guid> divisionIds,
		IReadOnlyCollection<Guid> electiveGroupIds)
	{
		var validDivisionIds = divisionIds.Where(id => id != Guid.Empty).Distinct().ToList();
		var validElectiveGroupIds = electiveGroupIds.Where(id => id != Guid.Empty).Distinct().ToList();
		if (validDivisionIds.Count == 0 || validElectiveGroupIds.Count == 0)
		{
			return new Dictionary<Guid, HashSet<Guid>>();
		}

		var academicMediumId = await GetAcademicMediumIdForStandardAsync(standardId);
		var students = await studentRepository.GetListAsync(s =>
			s.AcademicMediumId == academicMediumId &&
			s.StandardId == standardId &&
			s.DivisionId.HasValue &&
			validDivisionIds.Contains(s.DivisionId.Value) &&
			s.Status == StudentLifeCycleStatusEnum.Enrolled &&
			s.AdmissionApprovalStatus == StudentEnrollmentApprovalState.Approved &&
			!s.IsDeleted);

		var studentIds = students.Select(s => s.Id).ToList();
		if (studentIds.Count == 0)
		{
			return new Dictionary<Guid, HashSet<Guid>>();
		}

		var selections = await studentElectiveSelectionRepository.GetListAsync(s =>
			studentIds.Contains(s.StudentId) &&
			validElectiveGroupIds.Contains(s.ElectiveGroupId) &&
			s.IsLocked);

		return selections
			.GroupBy(s => s.ElectiveGroupId)
			.ToDictionary(
				g => g.Key,
				g => g.Select(s => s.SelectedSubjectId).ToHashSet());
	}

	private async Task<Guid> GetAcademicMediumIdForStandardAsync(Guid standardId)
	{
		var standard = await standardRepository.GetAsync(standardId);
		var stage = await educationMediumStageRepository.GetAsync(standard.EducationMediumStageId);
		return stage.EducationMediumId;
	}

	private async Task<Guid?> GetElectiveGroupIdForSubjectAsync(Guid standardId, Guid subjectId)
	{
		var assignment = (await assignmentRepository.GetListAsync(a =>
			a.StandardId == standardId &&
			a.SubjectId == subjectId &&
			a.ElectiveGroupId.HasValue))
			.FirstOrDefault();

		return assignment?.ElectiveGroupId;
	}

	private async Task<List<Student>> GetEligibleStudentsForSubjectAsync(Exam exam, Guid subjectId, Guid divisionId)
	{
		var students = await studentRepository.GetListAsync(s =>
			s.DivisionId == divisionId &&
			s.Status == StudentLifeCycleStatusEnum.Enrolled &&
			s.AdmissionApprovalStatus == StudentEnrollmentApprovalState.Approved &&
			!s.IsDeleted);

		var electiveGroupId = await GetElectiveGroupIdForSubjectAsync(exam.StandardId, subjectId);
		if (!electiveGroupId.HasValue || students.Count == 0)
		{
			return students;
		}

		var studentIds = students.Select(s => s.Id).ToList();
		var selections = await studentElectiveSelectionRepository.GetListAsync(s =>
			studentIds.Contains(s.StudentId) &&
			s.ElectiveGroupId == electiveGroupId.Value &&
			s.IsLocked);
		var selectedStudentIds = selections
			.Where(s => s.SelectedSubjectId == subjectId)
			.Select(s => s.StudentId)
			.ToHashSet();

		return students.Where(s => selectedStudentIds.Contains(s.Id)).ToList();
	}

	private async Task<List<Guid>> GetRequiredSubjectIdsForDivisionAsync(Exam exam, Guid divisionId)
	{
		var subjectIds = new List<Guid>();
		foreach (var examSubject in exam.ExamSubjects)
		{
			var electiveGroupId = await GetElectiveGroupIdForSubjectAsync(exam.StandardId, examSubject.SubjectId);
			if (!electiveGroupId.HasValue)
			{
				subjectIds.Add(examSubject.SubjectId);
				continue;
			}

			var eligibleStudents = await GetEligibleStudentsForSubjectAsync(exam, examSubject.SubjectId, divisionId);
			if (eligibleStudents.Count > 0)
			{
				subjectIds.Add(examSubject.SubjectId);
			}
		}

		return subjectIds.Distinct().ToList();
	}

	private async Task EnsureElectiveSubjectUsableForDivisionAsync(Exam exam, Guid subjectId, Guid divisionId)
	{
		var electiveGroupId = await GetElectiveGroupIdForSubjectAsync(exam.StandardId, subjectId);
		if (!electiveGroupId.HasValue)
		{
			return;
		}

		var eligibleStudents = await GetEligibleStudentsForSubjectAsync(exam, subjectId, divisionId);
		if (eligibleStudents.Count == 0)
		{
			throw new UserFriendlyException("No student in this class and division is assigned to the selected elective subject. Configure marks for the elective subject selected by students.");
		}
	}

	private async Task EnsureStudentCanTakeSubjectAsync(Exam exam, Guid subjectId, Student student)
	{
		var electiveGroupId = await GetElectiveGroupIdForSubjectAsync(exam.StandardId, subjectId);
		if (!electiveGroupId.HasValue)
		{
			return;
		}

		var selection = (await studentElectiveSelectionRepository.GetListAsync(s =>
			s.StudentId == student.Id &&
			s.ElectiveGroupId == electiveGroupId.Value))
			.FirstOrDefault();

		if (selection == null || !selection.IsLocked)
		{
			throw new UserFriendlyException("Elective subject assignment is pending for this student. Please lock elective subject selection before entering marks.");
		}

		if (selection.SelectedSubjectId != subjectId)
		{
			throw new UserFriendlyException("This student is not assigned to the selected elective subject.");
		}
	}

	private static void EnsureMarksEditableState(ExamMarksWorkflowState state)
	{
		if (state.Status is not (ExamMarksWorkflowStatus.Draft or ExamMarksWorkflowStatus.ChangesRequested))
		{
			throw new UserFriendlyException("Marks can be edited only in Draft or Changes Requested status.");
		}
	}

	private static void EnsureDivisionBelongsToExam(Exam exam, Guid divisionId)
	{
		if (exam.ExamDivisions.All(x => x.DivisionId != divisionId))
		{
			throw new UserFriendlyException("Selected division does not belong to this exam.");
		}
	}

	private async Task ValidateScheduleConflictsAsync(Guid examId, Exam currentExam, List<NormalizedExamSubjectSchedule> subjects)
	{
		// Only validate entries that have a fully-specified time window
		var incomingScheduled = subjects
			.Where(s => s.StartTime.HasValue && s.EndTime.HasValue)
			.ToList();

		if (incomingScheduled.Count == 0)
			return;

		var currentDivisionIds = currentExam.ExamDivisions
			.Select(d => d.DivisionId)
			.ToHashSet();

		if (currentDivisionIds.Count == 0)
			return;

		// Load all other exams for the same standard (with divisions + subjects)
		var otherExamsQuery = (await examRepository.WithDetailsAsync())
			.Where(e => e.Id != examId && e.StandardId == currentExam.StandardId);

		var otherExams = await AsyncExecuter.ToListAsync(otherExamsQuery);

		// Keep only exams that share at least one division with the current exam
		var conflictCandidateSubjects = otherExams
			.Where(e => e.ExamDivisions.Any(d => currentDivisionIds.Contains(d.DivisionId)))
			.SelectMany(e => e.ExamSubjects)
			.Where(es => es.ExamDate.HasValue && es.StartTime.HasValue && es.EndTime.HasValue)
			.ToList();

		if (conflictCandidateSubjects.Count == 0)
			return;

		foreach (var incoming in incomingScheduled)
		{
			var incomingStart = incoming.StartTime!.Value;
			var incomingEnd = incoming.EndTime!.Value;

			// Find all existing subjects on the same day whose window overlaps
			var dayOverlaps = conflictCandidateSubjects
				.Where(es =>
				{
					var existingStart = es.ExamDate!.Value.Date.Add(es.StartTime!.Value.TimeOfDay);
					var existingEnd = es.ExamDate.Value.Date.Add(es.EndTime!.Value.TimeOfDay);
					return existingStart.Date == incomingStart.Date &&
						existingStart < incomingEnd &&
						existingEnd > incomingStart;
				})
				.ToList();

			if (dayOverlaps.Count == 0)
				continue;

			// More specific check first: same subject already scheduled in the overlapping slot
			var subjectId = incoming.ElectiveGroupId ?? incoming.SubjectId;
			var duplicateSubject = dayOverlaps
				.FirstOrDefault(es => es.SubjectId == subjectId);

			if (duplicateSubject != null)
			{
				throw new BusinessException(ExamErrorCodes.DuplicateSubjectSchedule)
					.WithData("SubjectId", subjectId)
					.WithData("ExamDate", incomingStart.Date.ToString("yyyy-MM-dd"))
					.WithData("message", $"This subject already has an exam scheduled on {incomingStart.Date:dd MMM yyyy}. Please choose a different date.");
			}

			// General overlap: a different subject in the same class+division occupies this time slot
			var conflictingSubject = dayOverlaps.First();
			throw new BusinessException(ExamErrorCodes.ScheduleOverlap)
				.WithData("ExamDate", incomingStart.Date.ToString("yyyy-MM-dd"))
				.WithData("StartTime", conflictingSubject.StartTime!.Value.ToString("HH:mm"))
				.WithData("EndTime", conflictingSubject.EndTime!.Value.ToString("HH:mm"))
				.WithData("message", $"Another exam is already scheduled on {incomingStart.Date:dd MMM yyyy} between {conflictingSubject.StartTime!.Value:HH:mm} and {conflictingSubject.EndTime!.Value:HH:mm}. Please choose a different time.");
		}
	}
	// ─── Workflow helpers ──────────────────────────────────────────────────────

	private async Task<ExamMarksWorkflowState> GetOrCreateWorkflowStateAsync(Guid examId, Guid subjectId, Guid divisionId)
	{
		var existing = (await examMarksWorkflowStateRepository.GetListAsync(
			w => w.ExamId == examId && w.SubjectId == subjectId && w.DivisionId == divisionId))
			.FirstOrDefault();

		if (existing != null) return existing;

		var state = new ExamMarksWorkflowState(GuidGenerator.Create(), examId, subjectId, divisionId, CurrentTenant.Id);
		await examMarksWorkflowStateRepository.InsertAsync(state, autoSave: true);
		return state;
	}

	private async Task<Exam> EnsureMarksWorkflowTargetAsync(Guid examId, Guid subjectId, Guid divisionId)
	{
		var exam = await examRepository.GetAsync(examId, includeDetails: true);
		EnsureDivisionBelongsToExam(exam, divisionId);

		if (exam.ExamSubjects.All(s => s.SubjectId != subjectId))
		{
			throw new UserFriendlyException("Selected subject does not belong to this exam.");
		}

		await EnsureElectiveSubjectUsableForDivisionAsync(exam, subjectId, divisionId);

		return exam;
	}

	private static MarksWorkflowActionResponseDto BuildWorkflowResponse(string message, Guid examId, MarksWorkflowActionInput input, ExamMarksWorkflowStatus status)
	{
		return new MarksWorkflowActionResponseDto
		{
			Message = message,
			ExamId = examId,
			DivisionId = input.DivisionId,
			SubjectId = input.SubjectId,
			WorkflowStatus = (int) status,
			UpdatedAt = DateTime.UtcNow
		};
	}

	// ─── 1. Save Draft ─────────────────────────────────────────────────────────

	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.Add)]
	public async Task<MarksWorkflowActionResponseDto> SaveDraftAsync(Guid examId, MarksWorkflowActionInput input)
	{
		await EnsureMarksWorkflowTargetAsync(examId, input.SubjectId, input.DivisionId);
		var state = await GetOrCreateWorkflowStateAsync(examId, input.SubjectId, input.DivisionId);

		EnsureMarksEditableState(state);

		// Draft keeps the status at Draft (0); if already beyond Draft, do not regress.
		if (state.Status == ExamMarksWorkflowStatus.Draft)
			await examMarksWorkflowStateRepository.UpdateAsync(state, autoSave: true);

		return BuildWorkflowResponse("Draft saved successfully.", examId, input, state.Status);
	}

	// ─── 2. Submit Marks ───────────────────────────────────────────────────────

	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.Add)]
	public async Task<MarksWorkflowActionResponseDto> SubmitMarksAsync(Guid examId, MarksWorkflowActionInput input)
	{
		await EnsureMarksWorkflowTargetAsync(examId, input.SubjectId, input.DivisionId);
		var state = await GetOrCreateWorkflowStateAsync(examId, input.SubjectId, input.DivisionId);

		if (state.Status is not (ExamMarksWorkflowStatus.Draft or ExamMarksWorkflowStatus.ChangesRequested))
			throw new UserFriendlyException("Marks can only be submitted from Draft or Changes Requested status.");

		// Validate only students who are required to take this subject.
		var exam = await examRepository.GetAsync(examId, includeDetails: true);
		var enrolledStudents = await GetEligibleStudentsForSubjectAsync(exam, input.SubjectId, input.DivisionId);

		var enteredMarks = await examMarkRepository.GetListAsync(m =>
			m.ExamId == examId && m.SubjectId == input.SubjectId && m.DivisionId == input.DivisionId);

		var enteredStudentIds = new HashSet<Guid>(enteredMarks.Select(m => m.StudentId));
		var allEntered = enrolledStudents.All(s => enteredStudentIds.Contains(s.Id));

		if (!allEntered)
			throw new UserFriendlyException("All students must have marks entered before submission.");

		var validation = await ValidateMarksAsync(examId, input);
		if (!validation.IsValid)
			throw new UserFriendlyException("Please resolve blocking marks validation issues before submission.");

		state.TransitionTo(ExamMarksWorkflowStatus.Submitted);
		await examMarksWorkflowStateRepository.UpdateAsync(state, autoSave: true);

		return BuildWorkflowResponse("Marks submitted for verification.", examId, input, ExamMarksWorkflowStatus.Submitted);
	}

	// ─── 3. Verify Marks ───────────────────────────────────────────────────────

	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.Add)]
	public async Task<MarksWorkflowActionResponseDto> VerifyMarksAsync(Guid examId, MarksWorkflowActionInput input)
	{
		await EnsureMarksWorkflowTargetAsync(examId, input.SubjectId, input.DivisionId);
		var state = await GetOrCreateWorkflowStateAsync(examId, input.SubjectId, input.DivisionId);

		if (state.Status != ExamMarksWorkflowStatus.Submitted)
			throw new UserFriendlyException("Marks can only be verified from Submitted status.");

		var validation = await ValidateMarksAsync(examId, input);
		if (!validation.IsValid)
			throw new UserFriendlyException("Please resolve blocking marks validation issues before verification.");

		state.TransitionTo(ExamMarksWorkflowStatus.Verified);
		await examMarksWorkflowStateRepository.UpdateAsync(state, autoSave: true);

		return BuildWorkflowResponse("Marks verified successfully.", examId, input, ExamMarksWorkflowStatus.Verified);
	}

	// ─── 4. Publish and Lock Marks ─────────────────────────────────────────────

	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.Add)]
	public async Task<MarksWorkflowActionResponseDto> PublishAndLockAsync(Guid examId, MarksWorkflowActionInput input)
	{
		await EnsureMarksWorkflowTargetAsync(examId, input.SubjectId, input.DivisionId);
		var state = await GetOrCreateWorkflowStateAsync(examId, input.SubjectId, input.DivisionId);

		if (state.Status != ExamMarksWorkflowStatus.Verified)
			throw new UserFriendlyException("Marks can only be published from Verified status.");

		var validation = await ValidateMarksAsync(examId, input);
		if (!validation.IsValid)
			throw new UserFriendlyException("Please resolve blocking marks validation issues before publishing.");

		// Publish all marks for this exam/subject/division
		var marks = await examMarkRepository.GetListAsync(m =>
			m.ExamId == examId && m.SubjectId == input.SubjectId && m.DivisionId == input.DivisionId);

		if (marks.Count == 0)
			throw new UserFriendlyException("No marks found to publish.");

		foreach (var mark in marks)
			mark.Publish();

		await examMarkRepository.UpdateManyAsync(marks, autoSave: true);

		state.TransitionTo(ExamMarksWorkflowStatus.Published);
		await examMarksWorkflowStateRepository.UpdateAsync(state, autoSave: true);

		return BuildWorkflowResponse("Marks published and locked successfully.", examId, input, ExamMarksWorkflowStatus.Published);
	}

	// ─── 6. Update Student Attendance ──────────────────────────────────────────

	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.Add)]
	public async Task<UpdateStudentAttendanceResponseDto> UpdateStudentAttendanceAsync(
		Guid examId, Guid studentId, Guid subjectId, UpdateStudentAttendanceDto input)
	{
		var student = await studentRepository.GetAsync(studentId);

		var divisionId = student.DivisionId
			?? throw new UserFriendlyException("Student is not assigned to a division.");

		await EnsureMarksWorkflowTargetAsync(examId, subjectId, divisionId);
		var exam = await examRepository.GetAsync(examId, includeDetails: true);
		await EnsureStudentCanTakeSubjectAsync(exam, subjectId, student);

		// Check for locked state
		var state = (await examMarksWorkflowStateRepository.GetListAsync(
			w => w.ExamId == examId && w.SubjectId == subjectId && w.DivisionId == divisionId))
			.FirstOrDefault();

		if (state != null)
		{
			EnsureMarksEditableState(state);
		}

		if (!Enum.IsDefined(typeof(ExamAttendanceStatus), input.AttendanceStatus))
			throw new UserFriendlyException("Invalid attendance status.");

		var attendanceStatus = (ExamAttendanceStatus) input.AttendanceStatus;

		var existing = (await examMarkRepository.GetListAsync(m =>
			m.ExamId == examId && m.StudentId == studentId && m.SubjectId == subjectId))
			.FirstOrDefault();

		if (existing != null)
		{
			existing.UpdateAttendance(attendanceStatus);
			await examMarkRepository.UpdateAsync(existing, autoSave: true);
		}
		else
		{
			var mark = new ExamMark(
				GuidGenerator.Create(),
				examId,
				studentId,
				subjectId,
				divisionId,
				marksObtained: null,
				isAbsent: attendanceStatus != ExamAttendanceStatus.Present,
				feedback: null,
				CurrentTenant.Id);
			mark.UpdateAttendance(attendanceStatus);
			await examMarkRepository.InsertAsync(mark, autoSave: true);
		}

		return new UpdateStudentAttendanceResponseDto
		{
			Message = "Attendance updated successfully.",
			ExamId = examId,
			StudentId = studentId,
			SubjectId = subjectId,
			AttendanceStatus = input.AttendanceStatus
		};
	}

	// ─── 7. Get Marks Summary ──────────────────────────────────────────────────

	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.View)]
	public async Task<GetExamMarksSummaryResponseDto> GetMarksSummaryAsync(GetExamMarksSummaryInput input)
	{
		var exam = await examRepository.GetAsync(input.ExamId, includeDetails: true);

		var examSubject = exam.ExamSubjects.FirstOrDefault(s => s.SubjectId == input.SubjectId)
			?? throw new UserFriendlyException("Subject not found in this exam.");

		var subjectList = await subjectRepository.GetListAsync(s => s.Id == input.SubjectId);
		var subject = subjectList.FirstOrDefault();

		var divisionList = await divisionRepository.GetListAsync(d => d.Id == input.DivisionId);
		var division = divisionList.FirstOrDefault();

		var standardList = await standardRepository.GetListAsync(s => s.Id == exam.StandardId);
		var standard = standardList.FirstOrDefault();

		var enrolledStudents = await GetEligibleStudentsForSubjectAsync(exam, input.SubjectId, input.DivisionId);

		var existingMarks = await examMarkRepository.GetListAsync(m =>
			m.ExamId == input.ExamId && m.SubjectId == input.SubjectId && m.DivisionId == input.DivisionId);

		var marksEntered = existingMarks.Count(m => m.MarksObtained.HasValue);
		var totalStudents = enrolledStudents.Count;
		var pending = totalStudents - marksEntered;

		var allWorkflowStates = await examMarksWorkflowStateRepository.GetListAsync(
			w => w.ExamId == input.ExamId && w.DivisionId == input.DivisionId);
		var requiredSubjectIds = await GetRequiredSubjectIdsForDivisionAsync(exam, input.DivisionId);

		var publishedSubjectIds = allWorkflowStates
			.Where(w => w.Status == ExamMarksWorkflowStatus.Published)
			.Select(w => w.SubjectId)
			.ToHashSet();

		var pendingPublishedCount = requiredSubjectIds.Count(subjectId => !publishedSubjectIds.Contains(subjectId));

		var workflowState = allWorkflowStates.FirstOrDefault(w => w.SubjectId == input.SubjectId);

		return new GetExamMarksSummaryResponseDto
		{
			ExamId = exam.Id,
			ExamName = exam.ExamName,
			SubjectName = subject?.Name ?? string.Empty,
			ClassName = standard?.Name ?? string.Empty,
			DivisionName = division?.Name ?? string.Empty,
			MaxMarks = examSubject.TotalMarks ?? 0,
			PassingMarks = examSubject.PassingMarks ?? 0,
			ExamDate = examSubject.ExamDate,
			TotalStudents = totalStudents,
			MarksEntered = marksEntered,
			Pending = pending < 0 ? 0 : pending,
			WorkflowStatus = workflowState != null ? (int) workflowState.Status : (int) ExamMarksWorkflowStatus.Draft,
			PendingPublishedCount = pendingPublishedCount
		};
	}

	// ─── 8. Bulk Save Marks ────────────────────────────────────────────────────

	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.Add)]
	public async Task<MarksWorkflowActionResponseDto> BulkSaveMarksAsync(Guid examId, BulkSaveMarksInputDto input)
	{
		var exam = await EnsureMarksWorkflowTargetAsync(examId, input.SubjectId, input.DivisionId);
		var state = await GetOrCreateWorkflowStateAsync(examId, input.SubjectId, input.DivisionId);

		EnsureMarksEditableState(state);

		var examSubject = exam.ExamSubjects.FirstOrDefault(s => s.SubjectId == input.SubjectId)
			?? throw new UserFriendlyException("Subject not found in this exam");

		if (input.Rows.Count == 0)
			throw new UserFriendlyException("At least one marks row is required.");

		var duplicateStudentIds = input.Rows
			.GroupBy(r => r.StudentId)
			.Where(g => g.Count() > 1)
			.Select(g => g.Key)
			.ToList();

		if (duplicateStudentIds.Count > 0)
			throw new UserFriendlyException("Duplicate student marks rows are not allowed.");

		var existingMarks = await examMarkRepository.GetListAsync(m =>
			m.ExamId == examId && m.SubjectId == input.SubjectId && m.DivisionId == input.DivisionId);
		var marksLookup = existingMarks.ToDictionary(m => m.StudentId);

		foreach (var row in input.Rows)
		{
			var student = await studentRepository.GetAsync(row.StudentId);
			if (student.DivisionId != input.DivisionId)
				throw new UserFriendlyException("Student does not belong to the selected division.");
			await EnsureStudentCanTakeSubjectAsync(exam, input.SubjectId, student);

			var attendanceStatus = (ExamAttendanceStatus) row.AttendanceStatus;
			var isPresent = attendanceStatus == ExamAttendanceStatus.Present;

			if (isPresent && row.ObtainedMarks == null)
				throw new UserFriendlyException("Obtained marks are required for present students.");

			if (row.ObtainedMarks.HasValue && (row.ObtainedMarks.Value < 0 || row.ObtainedMarks.Value > (examSubject.TotalMarks ?? 100)))
				throw new UserFriendlyException($"Obtained marks must be between 0 and {examSubject.TotalMarks ?? 100}");

			marksLookup.TryGetValue(row.StudentId, out var existing);

			if (existing != null)
			{
				existing.UpdateAttendance(attendanceStatus);
				if (isPresent)
					existing.Update(row.ObtainedMarks, isAbsent: false, row.Feedback);
				await examMarkRepository.UpdateAsync(existing, autoSave: true);
			}
			else
			{
				var mark = new ExamMark(
					GuidGenerator.Create(),
					examId,
					row.StudentId,
					input.SubjectId,
					input.DivisionId,
					marksObtained: isPresent ? row.ObtainedMarks : null,
					isAbsent: !isPresent,
					feedback: isPresent ? row.Feedback : null,
					CurrentTenant.Id);
				mark.UpdateAttendance(attendanceStatus);
				await examMarkRepository.InsertAsync(mark, autoSave: true);
			}
		}

		return BuildWorkflowResponse("Bulk marks saved successfully.", examId, new MarksWorkflowActionInput { SubjectId = input.SubjectId, DivisionId = input.DivisionId }, state.Status);
	}

	// ─── 9. Validate Marks ─────────────────────────────────────────────────────

	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.View)]
	public async Task<MarksValidationResponseDto> ValidateMarksAsync(Guid examId, MarksWorkflowActionInput input)
	{
		var response = new MarksValidationResponseDto { IsValid = true };

		var exam = await EnsureMarksWorkflowTargetAsync(examId, input.SubjectId, input.DivisionId);
		var examSubject = exam.ExamSubjects.FirstOrDefault(s => s.SubjectId == input.SubjectId)
			?? throw new UserFriendlyException("Subject not found in this exam");

		var enrolledStudents = await GetEligibleStudentsForSubjectAsync(exam, input.SubjectId, input.DivisionId);

		var existingMarks = await examMarkRepository.GetListAsync(m =>
			m.ExamId == examId && m.SubjectId == input.SubjectId && m.DivisionId == input.DivisionId);
		var marksLookup = existingMarks.ToDictionary(m => m.StudentId);

		var maxMarks = examSubject.TotalMarks ?? 100;
		var passThreshold = examSubject.PassingMarks ?? (maxMarks * 0.33);

		foreach (var student in enrolledStudents)
		{
			marksLookup.TryGetValue(student.Id, out var mark);
			var studentName = $"{student.FirstName} {student.MiddleName} {student.LastName}".Trim();

			if (mark == null)
			{
				response.Blockers.Add(new MarksValidationErrorDto
				{
					Code = "MISSING_ATTENDANCE",
					Message = $"Attendance status is missing for {studentName}.",
					StudentId = student.Id,
					StudentName = studentName
				});
				continue;
			}

			if (mark.AttendanceStatus == ExamAttendanceStatus.Present)
			{
				if (!mark.MarksObtained.HasValue)
				{
					response.Blockers.Add(new MarksValidationErrorDto
					{
						Code = "MISSING_MARKS",
						Message = $"Marks are missing for present student {studentName}.",
						StudentId = student.Id,
						StudentName = studentName
					});
				}
				else
				{
					if (mark.MarksObtained.Value > maxMarks)
					{
						response.Blockers.Add(new MarksValidationErrorDto
						{
							Code = "MARKS_EXCEED_TOTAL",
							Message = $"Marks for {studentName} ({mark.MarksObtained.Value}) exceed total marks ({maxMarks}).",
							StudentId = student.Id,
							StudentName = studentName
						});
					}

					if (mark.MarksObtained.Value < 0)
					{
						response.Blockers.Add(new MarksValidationErrorDto
						{
							Code = "MARKS_NEGATIVE",
							Message = $"Marks for {studentName} ({mark.MarksObtained.Value}) cannot be negative.",
							StudentId = student.Id,
							StudentName = studentName
						});
					}

					if (mark.MarksObtained.Value == 0)
					{
						response.Warnings.Add(new MarksValidationErrorDto
						{
							Code = "ZERO_MARKS_PRESENT",
							Message = $"Present student {studentName} scored zero marks.",
							StudentId = student.Id,
							StudentName = studentName
						});
					}
				}
			}
			else
			{
				if (mark.MarksObtained.HasValue)
				{
					response.Warnings.Add(new MarksValidationErrorDto
					{
						Code = "MARKS_FOR_ABSENT_STUDENT",
						Message = $"Student {studentName} is absent but has obtained marks recorded.",
						StudentId = student.Id,
						StudentName = studentName
					});
				}
			}
		}

		// Analytics checks
		var gradedPresent = existingMarks.Where(m => m.MarksObtained.HasValue && m.AttendanceStatus == ExamAttendanceStatus.Present).ToList();
		if (gradedPresent.Count > 0)
		{
			var fails = gradedPresent.Count(m => m.MarksObtained!.Value < passThreshold);
			var failPercentage = (double) fails / gradedPresent.Count * 100;
			if (failPercentage > 50)
			{
				response.Warnings.Add(new MarksValidationErrorDto
				{
					Code = "HIGH_FAIL_PERCENTAGE",
					Message = $"Unusually high failure rate detected: {Math.Round(failPercentage, 1)}% of students failed this subject."
				});
			}

			var distinctMarks = gradedPresent.Select(m => m.MarksObtained!.Value).Distinct().ToList();
			if (gradedPresent.Count > 1 && distinctMarks.Count == 1)
			{
				response.Warnings.Add(new MarksValidationErrorDto
				{
					Code = "IDENTICAL_MARKS",
					Message = "All students graded have identical marks recorded."
				});
			}
		}

		response.IsValid = response.Blockers.Count == 0;
		return response;
	}

	// ─── 10. Request Changes ───────────────────────────────────────────────────

	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.Add)]
	public async Task<MarksWorkflowActionResponseDto> RequestChangesAsync(Guid examId, RequestChangesInputDto input)
	{
		await EnsureMarksWorkflowTargetAsync(examId, input.SubjectId, input.DivisionId);
		var state = await GetOrCreateWorkflowStateAsync(examId, input.SubjectId, input.DivisionId);

		if (state.Status != ExamMarksWorkflowStatus.Submitted)
			throw new UserFriendlyException("Changes can only be requested from Submitted status.");

		if (string.IsNullOrWhiteSpace(input.ReviewerNote))
			throw new UserFriendlyException("Reviewer note is required when requesting marks correction.");

		state.TransitionTo(ExamMarksWorkflowStatus.ChangesRequested);
		state.SetReviewerNote(input.ReviewerNote.Trim());
		await examMarksWorkflowStateRepository.UpdateAsync(state, autoSave: true);

		return BuildWorkflowResponse("Correction requested and note recorded.", examId, new MarksWorkflowActionInput { SubjectId = input.SubjectId, DivisionId = input.DivisionId }, ExamMarksWorkflowStatus.ChangesRequested);
	}

	// ─── 11. Unlock Marks ──────────────────────────────────────────────────────

	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.Add)]
	public async Task<MarksWorkflowActionResponseDto> UnlockMarksAsync(Guid examId, UnlockWorkflowInputDto input)
	{
		await EnsureMarksWorkflowTargetAsync(examId, input.SubjectId, input.DivisionId);
		var state = await GetOrCreateWorkflowStateAsync(examId, input.SubjectId, input.DivisionId);

		if (state.Status != ExamMarksWorkflowStatus.Published)
			throw new UserFriendlyException("Marks can only be unlocked from Published status.");

		if (string.IsNullOrWhiteSpace(input.Reason))
			throw new UserFriendlyException("Unlock reason is required.");

		// Reset publish state for all marks
		var marks = await examMarkRepository.GetListAsync(m =>
			m.ExamId == examId && m.SubjectId == input.SubjectId && m.DivisionId == input.DivisionId);

		foreach (var mark in marks)
		{
			mark.Unpublish();
			await examMarkRepository.UpdateAsync(mark, autoSave: true);
		}

		state.TransitionTo(ExamMarksWorkflowStatus.Draft);
		state.SetUnlockReason(input.Reason.Trim());
		await examMarksWorkflowStateRepository.UpdateAsync(state, autoSave: true);

		return BuildWorkflowResponse("Marks unlocked successfully back to Draft.", examId, new MarksWorkflowActionInput { SubjectId = input.SubjectId, DivisionId = input.DivisionId }, ExamMarksWorkflowStatus.Draft);
	}

	// ─── 12. Get Audit Logs ────────────────────────────────────────────────────

	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.View)]
	public async Task<List<GradeEntryAuditLogDto>> GetAuditLogsAsync(Guid examId, Guid subjectId, Guid divisionId)
	{
		var result = new List<GradeEntryAuditLogDto>();

		var marks = await examMarkRepository.GetListAsync(m =>
			m.ExamId == examId && m.SubjectId == subjectId && m.DivisionId == divisionId);

		var state = (await examMarksWorkflowStateRepository.GetListAsync(
			w => w.ExamId == examId && w.SubjectId == subjectId && w.DivisionId == divisionId))
			.FirstOrDefault();

		// Fetch user details for modifier/creator
		var userIds = marks.Select(m => m.LastModifierId ?? m.CreatorId)
			.Where(id => id.HasValue)
			.Select(id => id!.Value)
			.Distinct()
			.ToList();

		if (state?.LastModifierId.HasValue == true)
			userIds.Add(state.LastModifierId.Value);

		var users = userIds.Count > 0
			? await identityUserRepository.GetListAsync(u => userIds.Contains(u.Id))
			: new List<IdentityUser>();
		var userDict = users.ToDictionary(u => u.Id, u => u.Name ?? u.UserName);

		string GetUserName(Guid? id) => id.HasValue && userDict.TryGetValue(id.Value, out var name) ? name : "System";

		if (state != null)
		{
			result.Add(new GradeEntryAuditLogDto
			{
				Timestamp = state.LastModificationTime ?? state.CreationTime,
				Action = "Workflow Status Transition",
				ChangedBy = GetUserName(state.LastModifierId ?? state.CreatorId),
				Details = $"Workflow transitioned to status {state.Status}. Notes: {state.ReviewerNote ?? state.UnlockReason ?? "None"}"
			});
		}

		var lastMark = marks.OrderByDescending(m => m.LastModificationTime ?? m.CreationTime).FirstOrDefault();
		if (lastMark != null)
		{
			result.Add(new GradeEntryAuditLogDto
			{
				Timestamp = lastMark.LastModificationTime ?? lastMark.CreationTime,
				Action = "Marks Modified",
				ChangedBy = GetUserName(lastMark.LastModifierId ?? lastMark.CreatorId),
				Details = $"Latest changes applied to student records. Active count of records: {marks.Count}."
			});
		}

		return result;
	}

	private sealed record NormalizedExamSubjectSchedule(
		Guid SubjectId,
		Guid? ElectiveGroupId,
		string? Notes,
		DateTime? ExamDate,
		DateTime? StartTime,
		DateTime? EndTime,
		double? TotalMarks,
		double? PassingMarks,
		Guid? GradeScalingId);
}
