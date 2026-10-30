using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Exams.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Entities;
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
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.HR.Employees;

namespace ShikshaMitra.Academic.Examination.Exams;

[RemoteService(false)]
public partial class ExamAppService(
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
	IRepository<AcademicCalendarDay, Guid> calendarDayRepository,
	IRepository<Employee, Guid> employeeRepository,
	IRepository<TeacherSubjectAllocation, Guid> teacherSubjectAllocationRepository,
	IUserScopeAccessService userScopeAccessService) : ApplicationService, IExamAppService
{
	public async Task<Guid> CreateBasicDetailsAsync(ExamBasicDetailsRequestDto input)
	{
		await EnsureCanAccessClassDivisionsAsync(input.StandardId, input.DivisionIds, input.SubjectIds);
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
		await EnsureCanAccessExamAsync(exam);
		EnsureExamSetupEditable(exam);
		await EnsureCanAccessClassDivisionsAsync(input.StandardId, input.DivisionIds, input.SubjectIds);
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
		await EnsureCanAccessExamAsync(exam);

		return new ExamBasicDetailsResponseDto
		{
			Id = exam.Id,
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
		await EnsureCanAccessExamAsync(exam);
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
		await EnsureCanAccessExamAsync(exam);

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

			var scheduledSubjectsById = group.ToDictionary(s => s.SubjectId);
			// The timetable is an exam-scope projection. Student elective selections may contain
			// other peers from the same group, but only ExamSubjects explicitly belong to this exam.
			var groupSubjectIds = group.Select(s => s.SubjectId).ToList();

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
		await EnsureCanAccessExamAsync(exam);
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
		await EnsureCanAccessExamAsync(exam);

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
		await EnsureCanAccessExamAsync(exam);

		var examSubjectBySubjectId = exam.ExamSubjects
			.GroupBy(x => x.SubjectId)
			.ToDictionary(group => group.Key, group => group.First());
		var examSubjectIds = examSubjectBySubjectId.Keys.ToList();
		var electiveAssignmentsForExamSubjects = examSubjectIds.Count > 0
			? await assignmentRepository.GetListAsync(a =>
				a.StandardId == exam.StandardId &&
				examSubjectIds.Contains(a.SubjectId) &&
				a.ElectiveGroupId.HasValue)
			: [];
		var electiveGroupIds = electiveAssignmentsForExamSubjects
			.Select(a => a.ElectiveGroupId!.Value)
			.Distinct()
			.ToList();
		var electiveGroupAssignments = electiveGroupIds.Count > 0
			? await assignmentRepository.GetListAsync(a =>
				a.StandardId == exam.StandardId &&
				a.ElectiveGroupId.HasValue &&
				electiveGroupIds.Contains(a.ElectiveGroupId.Value))
			: [];
		var assignmentBySubjectId = electiveGroupAssignments
			.Concat(electiveAssignmentsForExamSubjects)
			.GroupBy(a => a.SubjectId)
			.ToDictionary(group => group.Key, group => group.First());
		var electiveGroupById = electiveGroupIds.Count > 0
			? (await electiveGroupRepository.GetListAsync(g => electiveGroupIds.Contains(g.Id))).ToDictionary(g => g.Id)
			: new Dictionary<Guid, ElectiveGroup>();

		var orderedSubjectIds = new List<Guid>();
		foreach (var examSubject in exam.ExamSubjects)
		{
			if (assignmentBySubjectId.TryGetValue(examSubject.SubjectId, out var assignment) &&
				assignment.ElectiveGroupId.HasValue)
			{
				orderedSubjectIds.AddRange(electiveGroupAssignments
					.Where(a => a.ElectiveGroupId == assignment.ElectiveGroupId)
					.OrderBy(a => a.SubjectCode)
					.Select(a => a.SubjectId));
			}
			else
			{
				orderedSubjectIds.Add(examSubject.SubjectId);
			}
		}

		orderedSubjectIds = orderedSubjectIds.Distinct().ToList();
		var subjects = orderedSubjectIds.Count > 0
			? await subjectRepository.GetListAsync(x => orderedSubjectIds.Contains(x.Id))
			: [];
		var subjectDict = subjects.ToDictionary(x => x.Id, x => x);

		return orderedSubjectIds.Select(subjectId =>
		{
			var subject = subjectDict.GetValueOrDefault(subjectId);
			examSubjectBySubjectId.TryGetValue(subjectId, out var examSubject);
			assignmentBySubjectId.TryGetValue(subjectId, out var assignment);
			var electiveGroup = assignment?.ElectiveGroupId.HasValue == true
				? electiveGroupById.GetValueOrDefault(assignment.ElectiveGroupId.Value)
				: null;

			return new ExamSubjectResponseDto
			{
				ExamSubjectId = examSubject?.Id ?? Guid.Empty,
				SubjectId = subjectId,
				SubjectName = subject?.Name,
				SubjectColor = subject?.Color,
				SubjectCode = assignment?.SubjectCode,
				ElectiveGroupId = assignment?.ElectiveGroupId,
				ElectiveGroupName = electiveGroup?.Name,
				ElectiveGroupColor = electiveGroup?.Color,
				Notes = examSubject?.Notes,
				ExamDate = examSubject?.ExamDate,
				StartTime = examSubject?.StartTime,
				EndTime = examSubject?.EndTime,
				TotalMarks = examSubject?.TotalMarks,
				PassingMarks = examSubject?.PassingMarks
			};
		}).ToList();
	}

	public async Task<List<QuestionPaperExamLookupDto>> GetQuestionPaperLookupAsync(QuestionPaperExamLookupInput input)
	{
		if (input.StandardId == Guid.Empty || input.SubjectId == Guid.Empty)
		{
			return [];
		}

		// Exam does not persist AcademicYearId. Its scope is determined by the
		// selected standard's education medium. AcademicYearId is still part of
		// the public lookup contract because it is the context sent by the
		// question-paper wizard.
		var academicMediumId = await GetAcademicMediumIdForStandardAsync(input.StandardId);

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
					where stage.EducationMediumId == academicMediumId &&
						exam.StandardId == input.StandardId &&
						exam.ExamSubjects.Any(subject =>
							schedulableSubjectIds.Contains(subject.SubjectId) &&
							subject.ExamDate.HasValue &&
							subject.StartTime.HasValue &&
							subject.EndTime.HasValue)
					select new { exam, standard, stage, examType };

		var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
		var rows = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.exam.ExamName));
		var primaryTeacherSubjects = await GetCurrentPrimaryTeacherSubjectIdsByStandardAsync(
			rows.Select(row => row.exam.StandardId).Distinct().ToList());
		rows = rows
			.Where(row =>
				CanAccessExam(row.exam, row.standard, row.stage, scope) ||
				PrimaryTeacherCanAccessExam(row.exam, primaryTeacherSubjects))
			.ToList();

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
					StandardId = row.exam.StandardId,
					SubjectId = input.SubjectId,
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
		var exam = await examRepository.GetAsync(examId, includeDetails: true);
		await EnsureCanAccessExamAsync(exam);

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
		await EnsureCanAccessExamAsync(exam);
		await EnsureValidExamStatusTransitionAsync(exam, status);
		exam.ChangeStatus(status);
		await examRepository.UpdateAsync(exam);
	}

	public async Task<ExamListResultDto> GetListAsync(GetExamsInput input)
	{
		if (input.StartDate.HasValue && input.EndDate.HasValue && input.StartDate > input.EndDate)
			throw new UserFriendlyException("StartDate cannot be greater than EndDate.");

		var isDefaultDateRange = !input.StartDate.HasValue && !input.EndDate.HasValue;
		var filterStartDate = input.StartDate?.DateTime;
		var filterEndDate = input.EndDate?.DateTime;
		if (isDefaultDateRange)
		{
			var today = DateTime.Now.Date;
			var daysFromMonday = ((int)today.DayOfWeek + 6) % 7;
			filterStartDate = today.AddDays(-daysFromMonday);
			filterEndDate = filterStartDate.Value.AddDays(7).AddTicks(-1);
		}
		else
		{
			// Be tolerant of a one-sided range while retaining the explicit bound.
			filterStartDate ??= DateTime.MinValue;
			filterEndDate ??= DateTime.MaxValue;
		}

		var examQuery = await examRepository.WithDetailsAsync();
		var standardQuery = await standardRepository.GetQueryableAsync();
		var stageQuery = await educationMediumStageRepository.GetQueryableAsync();
		var examTypeQuery = await examTypeRepository.GetQueryableAsync();

		var baseQuery = from exam in examQuery
						join standard in standardQuery on exam.StandardId equals standard.Id
						join stage in stageQuery on standard.EducationMediumStageId equals stage.Id
						join examType in examTypeQuery on exam.ExamTypeId equals examType.Id
						where !input.AcademicMediumId.HasValue || stage.EducationMediumId == input.AcademicMediumId.Value
						select new { exam, standard, stage, examType };

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

		var utcNow = DateTime.UtcNow;
		var institutionNow = ExamScheduleStatusResolver.GetInstitutionNow(utcNow);
		var todayInInstitution = DateTime.SpecifyKind(institutionNow.Date, DateTimeKind.Utc);
		var currentSecond = institutionNow.Hour * 3600 + institutionNow.Minute * 60 + institutionNow.Second;
		var rangeStartDate = DateTime.SpecifyKind(filterStartDate!.Value.Date, DateTimeKind.Utc);
		var rangeStartSecond = filterStartDate.Value.Hour * 3600 + filterStartDate.Value.Minute * 60 + filterStartDate.Value.Second;
		var rangeEndDate = DateTime.SpecifyKind(filterEndDate!.Value, DateTimeKind.Utc);
		var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
		if (!scope.IsAdmin && !scope.HasWildcardAcademicYear)
		{
			var standardIds = await AsyncExecuter.ToListAsync(query.Select(row => row.exam.StandardId).Distinct());
			var primaryTeacherSubjects = await GetCurrentPrimaryTeacherSubjectIdsByStandardAsync(standardIds);
			var permittedIds = query.Where(row =>
				row.exam.ExamDivisions.Any(division => scope.DivisionIds.Contains(division.DivisionId)) ||
				scope.WildcardDivisionStandardIds.Contains(row.exam.StandardId) ||
				(scope.HasWildcardStandard && scope.WildcardStandardMediumStageIds.Contains(row.standard.EducationMediumStageId)) ||
				(scope.HasWildcardMediumStage && scope.WildcardMediumStageMediumIds.Contains(row.stage.EducationMediumId)) ||
				(scope.HasWildcardMedium && scope.MediumIds.Contains(row.stage.EducationMediumId)))
				.Select(row => row.exam.Id);
			foreach (var entry in primaryTeacherSubjects)
			{
				var standardId = entry.Key;
				var subjectIds = entry.Value.ToList();
				permittedIds = permittedIds.Concat(query.Where(row => row.exam.StandardId == standardId &&
					row.exam.ExamSubjects.Any() && row.exam.ExamSubjects.All(subject => subjectIds.Contains(subject.SubjectId)))
					.Select(row => row.exam.Id));
			}
			query = query.Where(row => permittedIds.Contains(row.exam.Id));
		}
		// SQLite cannot compare TimeSpan and rounds sub-millisecond date parts. Read exact
		// schedule keys only near a comparison boundary, then apply their IDs before SQL paging.
		var currentBoundarySeconds = new[] { (currentSecond + 86399) % 86400, currentSecond, (currentSecond + 1) % 86400 };
		var rangeBoundarySeconds = new[] { (rangeStartSecond + 86399) % 86400, rangeStartSecond, (rangeStartSecond + 1) % 86400 };
		var boundaryIds = await AsyncExecuter.ToListAsync(query.Where(row => row.exam.ExamSubjects.Any(subject => subject.ExamDate.HasValue &&
			((subject.ExamDate.Value.Date == todayInInstitution &&
			  ((subject.StartTime.HasValue && currentBoundarySeconds.Contains(subject.StartTime.Value.Hour * 3600 + subject.StartTime.Value.Minute * 60 + subject.StartTime.Value.Second)) ||
			   (subject.EndTime.HasValue && currentBoundarySeconds.Contains(subject.EndTime.Value.Hour * 3600 + subject.EndTime.Value.Minute * 60 + subject.EndTime.Value.Second)))) ||
			 (subject.ExamDate.Value.Date == rangeStartDate && subject.EndTime.HasValue &&
			  rangeBoundarySeconds.Contains(subject.EndTime.Value.Hour * 3600 + subject.EndTime.Value.Minute * 60 + subject.EndTime.Value.Second)))))
			.Select(row => row.exam.Id));
		var boundaryRows = boundaryIds.Count == 0 ? new List<ExamListBoundaryCandidate>() : await AsyncExecuter.ToListAsync(query
			.Where(row => boundaryIds.Contains(row.exam.Id)).Select(row => new ExamListBoundaryCandidate(row.exam.Id, row.exam.Status,
				row.exam.ExamSubjects.Select(subject => new ExamListSubjectSchedule(subject.SubjectId, subject.ExamDate, subject.StartTime, subject.EndTime)).ToList())));
		var excludedBoundaryIds = new List<Guid>();
		var boundaryStatuses = new Dictionary<Guid, ExamStatus>();
		foreach (var row in boundaryRows)
		{
			var window = GetExamScheduleWindow(row.Subjects);
			if (!IsExamInDateRange(row.Status, window, filterStartDate.Value, filterEndDate.Value, isDefaultDateRange)) excludedBoundaryIds.Add(row.Id);
			else boundaryStatuses[row.Id] = ExamScheduleStatusResolver.Resolve(row.Status, window.StartAt, window.EndAt, utcNow);
		}
		var pendingBoundaryIds = boundaryStatuses.Where(entry => entry.Value == ExamStatus.Pending).Select(entry => entry.Key).ToList();
		var publishedBoundaryIds = boundaryStatuses.Where(entry => entry.Value == ExamStatus.Published).Select(entry => entry.Key).ToList();
		var ongoingBoundaryIds = boundaryStatuses.Where(entry => entry.Value == ExamStatus.Ongoing).Select(entry => entry.Key).ToList();
		var completedBoundaryIds = boundaryStatuses.Where(entry => entry.Value == ExamStatus.Completed).Select(entry => entry.Key).ToList();
		query = query.Where(row => !excludedBoundaryIds.Contains(row.exam.Id) && (boundaryIds.Contains(row.exam.Id) ||
			(!row.exam.ExamSubjects.Any(subject => subject.ExamDate.HasValue) && (isDefaultDateRange || row.exam.Status == ExamStatus.Pending)) ||
			(row.exam.ExamSubjects.Any(subject => subject.ExamDate.HasValue && subject.ExamDate.Value.Date <= rangeEndDate) &&
			 row.exam.ExamSubjects.Any(subject => subject.ExamDate.HasValue &&
				 (subject.ExamDate.Value.Date > rangeStartDate || (subject.ExamDate.Value.Date == rangeStartDate &&
					 (!subject.EndTime.HasValue || (subject.EndTime.Value.Hour * 3600 + subject.EndTime.Value.Minute * 60 + subject.EndTime.Value.Second) >= rangeStartSecond)))))));
		var coarseStatusRows = query.Select(row => new
		{
			Id = row.exam.Id,
			row.exam.CreationTime,
			EffectiveStatus = row.exam.Status == ExamStatus.Pending || !row.exam.ExamSubjects.Any(subject => subject.ExamDate.HasValue)
				? ExamStatus.Pending
				: row.exam.ExamSubjects.Any(subject => subject.ExamDate.HasValue &&
					(subject.ExamDate.Value.Date < todayInInstitution || (subject.ExamDate.Value.Date == todayInInstitution &&
					 (!subject.StartTime.HasValue || (subject.StartTime.Value.Hour * 3600 + subject.StartTime.Value.Minute * 60 + subject.StartTime.Value.Second) <= currentSecond)))) &&
				  row.exam.ExamSubjects.Any(subject => subject.ExamDate.HasValue &&
					(subject.ExamDate.Value.Date > todayInInstitution || (subject.ExamDate.Value.Date == todayInInstitution &&
					 (!subject.EndTime.HasValue || (subject.EndTime.Value.Hour * 3600 + subject.EndTime.Value.Minute * 60 + subject.EndTime.Value.Second) >= currentSecond))))
					? ExamStatus.Ongoing
					: !row.exam.ExamSubjects.Any(subject => subject.ExamDate.HasValue &&
						(subject.ExamDate.Value.Date > todayInInstitution || (subject.ExamDate.Value.Date == todayInInstitution &&
						 (!subject.EndTime.HasValue || (subject.EndTime.Value.Hour * 3600 + subject.EndTime.Value.Minute * 60 + subject.EndTime.Value.Second) >= currentSecond))))
						? ExamStatus.Completed : row.exam.Status
		});
		var rowsWithEffectiveStatus = coarseStatusRows.Select(row => new
		{
			row.Id, row.CreationTime,
			EffectiveStatus = pendingBoundaryIds.Contains(row.Id) ? ExamStatus.Pending :
				publishedBoundaryIds.Contains(row.Id) ? ExamStatus.Published :
				ongoingBoundaryIds.Contains(row.Id) ? ExamStatus.Ongoing :
				completedBoundaryIds.Contains(row.Id) ? ExamStatus.Completed : row.EffectiveStatus
		});
		var counts = await AsyncExecuter.ToListAsync(rowsWithEffectiveStatus.GroupBy(row => row.EffectiveStatus)
			.Select(group => new { Status = group.Key, Count = group.Count() }));
		var statusCounts = counts.ToDictionary(count => count.Status, count => count.Count);
		var pendingCount = statusCounts.GetValueOrDefault(ExamStatus.Pending);
		var publishedCount = statusCounts.GetValueOrDefault(ExamStatus.Published);
		var ongoingCount = statusCounts.GetValueOrDefault(ExamStatus.Ongoing);
		var completedCount = statusCounts.GetValueOrDefault(ExamStatus.Completed);
		var filteredRows = rowsWithEffectiveStatus.Where(row => !input.Status.HasValue || row.EffectiveStatus == input.Status.Value);
		var totalCount = input.Status.HasValue ? statusCounts.GetValueOrDefault(input.Status.Value) : counts.Sum(count => count.Count);
		var selectedPage = await AsyncExecuter.ToListAsync(filteredRows.OrderBy(row => row.CreationTime)
			.Skip(input.SkipCount).Take(input.MaxResultCount));
		var pageIds = selectedPage.Select(row => row.Id).ToList();
		// Project setup names instead of entities: Standard and Stage have model auto-includes
		// that otherwise expand a small exam page into the whole academic setup graph.
		var pageDetails = await AsyncExecuter.ToListAsync(query.Where(row => pageIds.Contains(row.exam.Id))
			.Select(row => new { row.exam, StandardName = row.standard.Name, ExamTypeName = row.examType.Name }));
		var detailsById = pageDetails.ToDictionary(row => row.exam.Id);
		var queryResult = selectedPage.Where(row => detailsById.ContainsKey(row.Id)).Select(row => new
		{
			detailsById[row.Id].exam,
			detailsById[row.Id].StandardName,
			detailsById[row.Id].ExamTypeName,
			row.EffectiveStatus
		}).ToList();

		// Fetch Divisions
		var allDivisionIds = queryResult.SelectMany(x => x.exam.ExamDivisions.Select(d => d.DivisionId)).Distinct().ToList();
		var divisions = await AsyncExecuter.ToListAsync((await divisionRepository.GetQueryableAsync())
			.Where(x => allDivisionIds.Contains(x.Id)).Select(x => new { x.Id, x.Name }));
		var divisionDict = divisions.ToDictionary(x => x.Id, x => x.Name);

		// Fetch creator names
		var creatorIds = queryResult.Select(x => x.exam.CreatorId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
		var creatorUsers = await AsyncExecuter.ToListAsync((await identityUserRepository.GetQueryableAsync())
			.Where(x => creatorIds.Contains(x.Id)).Select(x => new { x.Id, x.Name, x.UserName, x.Email }));
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
			var roomRequiredSubjects = GetRoomRequiredSubjects(x.exam, electiveGroupByStandardAndSubject);
			var missingRoomAssignmentCount = roomRequiredSubjects
				.SelectMany(subject => x.exam.ExamDivisions.Select(division => new { subject, division }))
				.Count(row => !row.subject.RoomAssignments.Any(assignment => assignment.DivisionId == row.division.DivisionId));

			return new ExamTableItemDto
			{
                ExamId = x.exam.Id,
                ExamName = x.exam.ExamName,
                ExamTypeName = x.ExamTypeName,
                StandardId = x.exam.StandardId,
                StandardName = x.StandardName,
				CreatedBy = x.exam.CreatorId,
				CreatorName = x.exam.CreatorId.HasValue ? creatorNameById.GetValueOrDefault(x.exam.CreatorId.Value) : null,
				Status = x.EffectiveStatus,
				SubjectCount = subjectCount,
				StartDate = startDate,
				EndDate = finalEndAt,
				IsScheduleComplete = IsExamScheduleComplete(x.exam, electiveGroupByStandardAndSubject),
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
			OngoingCount = ongoingCount,
			CompletedCount = completedCount
		};
	}

	private static bool CanAccessExam(
		Exam exam,
		Standard standard,
		EducationMediumStage stage,
		UserScopeContext scope)
		=> CanAccessExam(exam.StandardId, standard.EducationMediumStageId, stage.EducationMediumId,
			exam.ExamDivisions.Select(division => division.DivisionId), scope);

	private static bool CanAccessExam(Guid standardId, Guid stageId, Guid mediumId,
		IEnumerable<Guid> divisionIds, UserScopeContext scope)
	{
		if (scope.IsAdmin || scope.HasWildcardAcademicYear)
		{
			return true;
		}

		if (divisionIds.Any(divisionId => scope.DivisionIds.Contains(divisionId)))
		{
			return true;
		}

		if (scope.WildcardDivisionStandardIds.Contains(standardId) ||
			(scope.HasWildcardStandard && scope.WildcardStandardMediumStageIds.Contains(stageId)) ||
			(scope.HasWildcardMediumStage && scope.WildcardMediumStageMediumIds.Contains(mediumId)))
		{
			return true;
		}

		return scope.HasWildcardMedium && scope.MediumIds.Contains(mediumId);
	}

	private async Task EnsureCanAccessExamAsync(Exam exam)
	{
		var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
		if (scope.IsAdmin)
		{
			return;
		}

		var (stageId, mediumId) = await GetStandardScopeIdsAsync(exam.StandardId);
		if (CanAccessExam(exam.StandardId, stageId, mediumId, exam.ExamDivisions.Select(division => division.DivisionId), scope) ||
			await CurrentUserIsPrimaryTeacherForAllSubjectsAsync(
				exam.StandardId,
				exam.ExamSubjects.Select(subject => subject.SubjectId).ToList()))
		{
			return;
		}

		throw new BusinessException(UserScopeErrorCodes.AccessDenied);
	}

	private async Task EnsureCanAccessClassDivisionsAsync(
		Guid standardId,
		IReadOnlyCollection<Guid> divisionIds,
		IReadOnlyCollection<Guid> subjectIds)
	{
		var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
		if (scope.IsAdmin)
		{
			return;
		}

		var (stageId, mediumId) = await GetStandardScopeIdsAsync(standardId);
		var canAccessEveryDivision = divisionIds.All(divisionId =>
			CanAccessExam(standardId, stageId, mediumId, [divisionId], scope));

		if (canAccessEveryDivision ||
			await CurrentUserIsPrimaryTeacherForAllSubjectsAsync(standardId, subjectIds))
		{
			return;
		}

		throw new BusinessException(UserScopeErrorCodes.AccessDenied);
	}

	private async Task<bool> CurrentUserIsPrimaryTeacherForAllSubjectsAsync(
		Guid standardId,
		IReadOnlyCollection<Guid> subjectIds)
	{
		var requestedSubjectIds = subjectIds
			.Where(subjectId => subjectId != Guid.Empty)
			.ToHashSet();
		if (requestedSubjectIds.Count == 0)
		{
			return false;
		}

		var primaryTeacherSubjects = await GetCurrentPrimaryTeacherSubjectIdsByStandardAsync([standardId]);
		return primaryTeacherSubjects.TryGetValue(standardId, out var allocatedSubjectIds) &&
			requestedSubjectIds.IsSubsetOf(allocatedSubjectIds);
	}

	private async Task<Dictionary<Guid, HashSet<Guid>>> GetCurrentPrimaryTeacherSubjectIdsByStandardAsync(
		IReadOnlyCollection<Guid> standardIds)
	{
		if (!CurrentUser.Id.HasValue || standardIds.Count == 0)
		{
			return new Dictionary<Guid, HashSet<Guid>>();
		}

		var currentEmployee = await employeeRepository.FirstOrDefaultAsync(
			employee => employee.UserId == CurrentUser.Id.Value);
		if (currentEmployee == null)
		{
			return new Dictionary<Guid, HashSet<Guid>>();
		}

		var uniqueStandardIds = standardIds.Distinct().ToList();
		var assignmentsWithTeachersQuery = await assignmentRepository.WithDetailsAsync(
			assignment => assignment.Teachers);
		var primaryTeacherAssignments = await AsyncExecuter.ToListAsync(
			assignmentsWithTeachersQuery
				.Where(assignment =>
					uniqueStandardIds.Contains(assignment.StandardId) &&
					assignment.Status == SubjectStatus.Published &&
					assignment.Teachers.Any(teacher =>
						teacher.TeacherType == SubjectTeacherType.Primary &&
						teacher.TeacherId == currentEmployee.Id))
				.Select(assignment => new { assignment.StandardId, assignment.SubjectId })
				.Distinct());

		return primaryTeacherAssignments
			.GroupBy(assignment => assignment.StandardId)
			.ToDictionary(
				group => group.Key,
				group => group.Select(assignment => assignment.SubjectId).ToHashSet());
	}

	private static bool PrimaryTeacherCanAccessExam(
		Exam exam,
		IReadOnlyDictionary<Guid, HashSet<Guid>> primaryTeacherSubjects)
		=> PrimaryTeacherCanAccessExam(exam.StandardId, exam.ExamSubjects.Select(subject => subject.SubjectId), primaryTeacherSubjects);

	private static bool PrimaryTeacherCanAccessExam(Guid standardId, IEnumerable<Guid> subjectIds,
		IReadOnlyDictionary<Guid, HashSet<Guid>> primaryTeacherSubjects)
	{
		var examSubjectIds = subjectIds.ToHashSet();
		return examSubjectIds.Count > 0 &&
			primaryTeacherSubjects.TryGetValue(standardId, out var allocatedSubjectIds) &&
			examSubjectIds.IsSubsetOf(allocatedSubjectIds);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.View)]
	public async Task<GetExamMarksResponseDto> GetMarksAsync(GetExamMarksInput input)
	{
		var exam = await examRepository.GetAsync(input.ExamId, includeDetails: true);
		await EnsureCanAccessExamAsync(exam);

		var examSubject = exam.ExamSubjects.FirstOrDefault(s => s.SubjectId == input.SubjectId)
			?? throw new UserFriendlyException("Subject not found in this exam");
		var (studentQuery, electiveGroupId) = await GetEligibleStudentsQueryAsync(exam, input.SubjectId, input.DivisionId);
		// Keep the existing CLR search and null-first roll ordering while loading only display fields.
		var allStudents = await AsyncExecuter.ToListAsync(studentQuery.Select(student => new
		{
			student.Id,
			student.FirstName,
			student.MiddleName,
			student.LastName,
			student.GrNo,
			student.RollNo
		}));
		if (electiveGroupId.HasValue && allStudents.Count == 0)
			throw new UserFriendlyException("No student in this class and division is assigned to the selected elective subject. Configure marks for the elective subject selected by students.");

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

		// Publication applies to the entire subject/division, even when search or paging hides marks.
		var markQuery = (await examMarkRepository.GetQueryableAsync()).Where(mark =>
			mark.ExamId == input.ExamId && mark.SubjectId == input.SubjectId && mark.DivisionId == input.DivisionId);
		var publication = await AsyncExecuter.FirstOrDefaultAsync(markQuery
			.GroupBy(mark => mark.ExamId)
			.Select(group => new { AllPublished = group.All(mark => mark.IsPublished) }));
		var studentIds = pagedStudents.Select(student => student.Id).ToList();
		var existingMarks = await AsyncExecuter.ToListAsync(markQuery.Where(mark => studentIds.Contains(mark.StudentId)));
		var marksLookup = existingMarks.ToDictionary(m => m.StudentId);

		var isPublished = publication?.AllPublished == true;

		// Fetch the specific grading scale configured for this subject
		ICollection<GradeRange> gradeRanges = new List<GradeRange>();
		double scaleTotal = 100.0;
		if (examSubject.GradeScalingId.HasValue)
		{
			var gradingScales = await gradingScaleRepository.WithDetailsAsync(gs => gs.GradeRanges);
			var customScale = await AsyncExecuter.FirstOrDefaultAsync(gradingScales.Where(gs => gs.Id == examSubject.GradeScalingId.Value));
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
		await EnsureCanAccessExamAsync(exam);

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
		await EnsureCanAccessExamAsync(exam);
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
		if (!Enum.IsDefined(requestedStatus) || requestedStatus == ExamStatus.Ongoing)
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

		var incompleteSubjectIds = new List<Guid>();
		var divisionIds = exam.ExamDivisions.Select(x => x.DivisionId).ToList();
		var validateCalendarDate = CreateExamCalendarValidator(exam.StandardId, divisionIds, exam.ExamSubjects.Select(subject => subject.ExamDate));

		foreach (var subject in exam.ExamSubjects.Where(subject => !electiveGroupBySubjectId.ContainsKey(subject.SubjectId)))
		{
			if (!HasCompleteScheduleFields(subject))
			{
				incompleteSubjectIds.Add(subject.SubjectId);
				continue;
			}

			ValidateSubjectScheduleValues(subject.SubjectId, subject.ExamDate, subject.StartTime, subject.EndTime, subject.TotalMarks, subject.PassingMarks);
			await validateCalendarDate(subject.ExamDate.Value);
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
				if (!HasCompleteScheduleFields(subject))
				{
					incompleteSubjectIds.Add(subject.SubjectId);
					continue;
				}

				ValidateSubjectScheduleValues(subject.SubjectId, subject.ExamDate, subject.StartTime, subject.EndTime, subject.TotalMarks, subject.PassingMarks);
				await validateCalendarDate(subject.ExamDate.Value);
			}
		}

		if (incompleteSubjectIds.Count > 0)
		{
			throw new BusinessException("ShikshaMitra.Exam:ScheduleIncomplete")
				.WithData("IncompleteSubjectCount", incompleteSubjectIds.Count)
				.WithData("IncompleteSubjectIds", string.Join(", ", incompleteSubjectIds));
		}
	}

	private static bool HasCompleteScheduleFields(ExamSubject subject)
	{
		return subject.ExamDate.HasValue
			&& subject.StartTime.HasValue
			&& subject.EndTime.HasValue
			&& subject.TotalMarks.HasValue
			&& subject.PassingMarks.HasValue
			&& subject.GradeScalingId.HasValue;
	}

	private async Task EnsureExamReadyToCompleteAsync(Exam exam)
	{
		var finalEndAt = GetExamFinalEndAt(exam);
		if (!finalEndAt.HasValue)
		{
			throw new BusinessException("ShikshaMitra.Exam:ScheduleIncomplete")
				.WithData("ExamId", exam.Id);
		}

		if (DateTime.Now <= finalEndAt.Value)
		{
			throw new BusinessException("ShikshaMitra.Exam:CompletionBeforeScheduleEnd")
				.WithData("ExamId", exam.Id)
				.WithData("FinalSubjectEndAt", finalEndAt.Value.ToString("yyyy-MM-dd HH:mm"))
				.WithData("CurrentAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
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
		var start = GetExamStartDateTime(exam);
		var end = GetExamFinalEndAt(exam);

		// Exam schedule fields contain institution-local wall-clock values. Resolve
		// them against an explicit UTC clock so an Azure/UTC host cannot lag IST by
		// 5.5 hours and leave an active exam in the Published tab.
		return ExamScheduleStatusResolver.Resolve(exam.Status, start, end, DateTime.UtcNow);
	}

	private static DateTime? GetExamStartDateTime(Exam exam)
	{
		var starts = exam.ExamSubjects
			.Where(subject => subject.ExamDate.HasValue)
			.Select(subject => subject.ExamDate!.Value.Date.Add(
				subject.StartTime?.TimeOfDay ?? TimeSpan.Zero))
			.ToList();

		return starts.Count == 0 ? null : starts.Min();
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

	private static ExamScheduleWindow GetExamScheduleWindow(IEnumerable<ExamListSubjectSchedule> subjects)
	{
		var scheduled = subjects.Where(subject => subject.ExamDate.HasValue).ToList();
		if (scheduled.Count == 0) return new ExamScheduleWindow(null, null, null);
		return new ExamScheduleWindow(
			scheduled.Min(subject => subject.ExamDate!.Value.Date),
			scheduled.Min(subject => subject.ExamDate!.Value.Date.Add(subject.StartTime?.TimeOfDay ?? TimeSpan.Zero)),
			scheduled.Max(subject => subject.EndTime.HasValue
				? subject.ExamDate!.Value.Date.Add(subject.EndTime.Value.TimeOfDay)
				: subject.ExamDate!.Value.Date.AddDays(1).AddTicks(-1)));
	}

	private static bool IsExamInDateRange(
		ExamStatus status,
		ExamScheduleWindow window,
		DateTime filterStartDate,
		DateTime filterEndDate,
		bool includeUndatedExams)
	{
		if (!window.FirstDate.HasValue)
		{
			// Pending exams are planning records until step 2 supplies subject
			// dates. Keep them visible in the planning list even when the UI sends
			// the current-week date range explicitly.
			return includeUndatedExams || status == ExamStatus.Pending;
		}

		// Inclusive overlap: an exam that starts or ends on a filter boundary
		// is applicable to that date range.
		return window.FirstDate.Value <= filterEndDate && window.EndAt!.Value >= filterStartDate;
	}

	private sealed record ExamListCandidate(Guid Id, Guid StandardId, Guid StageId, Guid MediumId,
		ExamStatus Status, List<Guid> DivisionIds, List<ExamListSubjectSchedule> Subjects);
	private sealed record ExamListSubjectSchedule(Guid SubjectId, DateTime? ExamDate, DateTime? StartTime, DateTime? EndTime);
	private sealed record ExamListBoundaryCandidate(Guid Id, ExamStatus Status, List<ExamListSubjectSchedule> Subjects);
	private sealed record ExamScheduleWindow(DateTime? FirstDate, DateTime? StartAt, DateTime? EndAt);

	private static DateTime? GetExamFinalEndAt(Exam exam)
	{
		var endTimes = exam.ExamSubjects
			.Where(subject => subject.ExamDate.HasValue)
			.Select(subject => subject.EndTime.HasValue
				? subject.ExamDate!.Value.Date.Add(subject.EndTime!.Value.TimeOfDay)
				: subject.ExamDate!.Value.Date.AddDays(1).AddTicks(-1))
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

		var assignments = await AsyncExecuter.ToListAsync((await assignmentRepository.GetQueryableAsync())
			.Where(a => validStandardIds.Contains(a.StandardId) &&
				validSubjectIds.Contains(a.SubjectId) && a.ElectiveGroupId.HasValue)
			.Select(a => new { a.StandardId, a.SubjectId, a.ElectiveGroupId }));

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

		var validateCalendarDate = CreateExamCalendarValidator(exam.StandardId, exam.ExamDivisions.Select(division => division.DivisionId), rows.Select(row => row.ExamDate));
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
				await validateCalendarDate(row.ExamDate.Value);
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

	private async Task SyncScheduleToAcademicCalendarAsync(Exam exam, IReadOnlyCollection<NormalizedExamSubjectSchedule> subjectSchedules)
	{
		var academicMediumId = await GetAcademicMediumIdForStandardAsync(exam.StandardId);
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
			calendar.AcademicMediumId == academicMediumId &&
			calendar.Status != CalendarStatus.Archived &&
			calendar.StartDate.Date <= maxDate &&
			calendar.EndDate.Date >= minDate);

		if (calendars.Count == 0)
		{
			throw new BusinessException("ShikshaMitra.Exam:AcademicCalendarNotFound")
				.WithData("StartDate", minDate.ToString("yyyy-MM-dd"))
				.WithData("EndDate", maxDate.ToString("yyyy-MM-dd"));
		}

		var syncMarker = $"{AcademicCalendarConsts.ExamSyncDescriptionPrefix}{exam.Id}";
		var existingSyncedDays = await calendarDayRepository.GetListAsync(day =>
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
			var calendar = SelectExamCalendar(calendars, row.Date);

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
				endTime: row.EndTime?.TimeOfDay,
				examTypeId: exam.ExamTypeId,
				linkedExamId: exam.Id,
				examStartTime: row.StartTime?.TimeOfDay,
				examEndTime: row.EndTime?.TimeOfDay);

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
		var (_, mediumId) = await GetStandardScopeIdsAsync(standardId);
		return mediumId;
	}

	private async Task<(Guid StageId, Guid MediumId)> GetStandardScopeIdsAsync(Guid standardId)
	{
		var stageId = await AsyncExecuter.FirstOrDefaultAsync((await standardRepository.GetQueryableAsync())
			.Where(standard => standard.Id == standardId).Select(standard => (Guid?)standard.EducationMediumStageId))
			?? throw new EntityNotFoundException(typeof(Standard), standardId);
		var mediumId = await AsyncExecuter.FirstOrDefaultAsync((await educationMediumStageRepository.GetQueryableAsync())
			.Where(stage => stage.Id == stageId).Select(stage => (Guid?)stage.EducationMediumId))
			?? throw new EntityNotFoundException(typeof(EducationMediumStage), stageId);
		return (stageId, mediumId);
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
		var (query, _) = await GetEligibleStudentsQueryAsync(exam, subjectId, divisionId);
		return await AsyncExecuter.ToListAsync(query);
	}

	private async Task<(IQueryable<Student> Query, Guid? ElectiveGroupId)> GetEligibleStudentsQueryAsync(
		Exam exam, Guid subjectId, Guid divisionId)
	{
		var students = (await studentRepository.GetQueryableAsync()).Where(s =>
			s.DivisionId == divisionId &&
			s.Status == StudentLifeCycleStatusEnum.Enrolled &&
			s.AdmissionApprovalStatus == StudentEnrollmentApprovalState.Approved &&
			!s.IsDeleted);

		var electiveGroupId = await GetElectiveGroupIdForSubjectAsync(exam.StandardId, subjectId);
		if (!electiveGroupId.HasValue)
		{
			return (students, null);
		}

		var selections = (await studentElectiveSelectionRepository.GetQueryableAsync()).Where(s =>
			s.ElectiveGroupId == electiveGroupId.Value &&
			s.SelectedSubjectId == subjectId && s.IsLocked);

		return (students.Where(student => selections.Any(selection => selection.StudentId == student.Id)), electiveGroupId);
	}

	private async Task<List<Guid>> GetRequiredSubjectIdsForDivisionAsync(Exam exam, Guid divisionId)
	{
		var subjectIds = exam.ExamSubjects.Select(subject => subject.SubjectId).Distinct().ToList();
		var assignments = await AsyncExecuter.ToListAsync((await assignmentRepository.GetQueryableAsync())
			.Where(assignment => assignment.StandardId == exam.StandardId && subjectIds.Contains(assignment.SubjectId) && assignment.ElectiveGroupId.HasValue)
			.Select(assignment => new { assignment.SubjectId, GroupId = assignment.ElectiveGroupId!.Value }));
		var groups = assignments.GroupBy(assignment => assignment.SubjectId)
			.ToDictionary(group => group.Key, group => group.First().GroupId);
		if (groups.Count == 0) return subjectIds;
		var students = (await studentRepository.GetQueryableAsync()).Where(student => student.DivisionId == divisionId &&
			student.Status == StudentLifeCycleStatusEnum.Enrolled && student.AdmissionApprovalStatus == StudentEnrollmentApprovalState.Approved && !student.IsDeleted);
		var groupIds = groups.Values.Distinct().ToList();
		var selections = await AsyncExecuter.ToListAsync((await studentElectiveSelectionRepository.GetQueryableAsync())
			.Where(selection => selection.IsLocked && groupIds.Contains(selection.ElectiveGroupId) && students.Any(student => student.Id == selection.StudentId))
			.Select(selection => new { selection.ElectiveGroupId, selection.SelectedSubjectId }).Distinct());
		var selected = selections.Select(selection => (selection.ElectiveGroupId, selection.SelectedSubjectId)).ToHashSet();
		return subjectIds.Where(id => !groups.TryGetValue(id, out var groupId) || selected.Contains((groupId, id))).ToList();
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

	private async Task<ExamMarksWorkflowState> GetOrCreateWorkflowStateAsync(Guid examId, Guid subjectId, Guid divisionId, bool autoSave = true)
	{
		var existing = (await examMarksWorkflowStateRepository.GetListAsync(
			w => w.ExamId == examId && w.SubjectId == subjectId && w.DivisionId == divisionId))
			.FirstOrDefault();

		if (existing != null) return existing;

		var state = new ExamMarksWorkflowState(GuidGenerator.Create(), examId, subjectId, divisionId, CurrentTenant.Id);
		await examMarksWorkflowStateRepository.InsertAsync(state, autoSave: autoSave);
		return state;
	}

	private async Task<Exam> EnsureMarksWorkflowTargetAsync(Guid examId, Guid subjectId, Guid divisionId)
	{
		var exam = await examRepository.GetAsync(examId, includeDetails: true);
		await EnsureCanAccessExamAsync(exam);
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

		var subjectName = await AsyncExecuter.FirstOrDefaultAsync((await subjectRepository.GetQueryableAsync()).Where(s => s.Id == input.SubjectId).Select(s => s.Name));
		var divisionName = await AsyncExecuter.FirstOrDefaultAsync((await divisionRepository.GetQueryableAsync()).Where(d => d.Id == input.DivisionId).Select(d => d.Name));
		var standardName = await AsyncExecuter.FirstOrDefaultAsync((await standardRepository.GetQueryableAsync()).Where(s => s.Id == exam.StandardId).Select(s => s.Name));
		var (enrolledStudents, _) = await GetEligibleStudentsQueryAsync(exam, input.SubjectId, input.DivisionId);
		var totalStudents = await AsyncExecuter.CountAsync(enrolledStudents);
		var marksEntered = await AsyncExecuter.CountAsync((await examMarkRepository.GetQueryableAsync()).Where(m =>
			m.ExamId == input.ExamId && m.SubjectId == input.SubjectId && m.DivisionId == input.DivisionId && m.MarksObtained.HasValue));
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
			SubjectName = subjectName ?? string.Empty,
			ClassName = standardName ?? string.Empty,
			DivisionName = divisionName ?? string.Empty,
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
		var state = await GetOrCreateWorkflowStateAsync(examId, input.SubjectId, input.DivisionId, autoSave: false);

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
		var studentIds = input.Rows.Select(row => row.StudentId).ToList();
		var studentQuery = await studentRepository.GetQueryableAsync();
		var students = await AsyncExecuter.ToListAsync(studentQuery
			.Where(student => studentIds.Contains(student.Id))
			.Select(student => new { student.Id, student.DivisionId }));
		var studentsById = students.ToDictionary(student => student.Id);
		var electiveGroupId = await GetElectiveGroupIdForSubjectAsync(exam.StandardId, input.SubjectId);
		var selections = electiveGroupId.HasValue
			? await studentElectiveSelectionRepository.GetListAsync(selection =>
				studentIds.Contains(selection.StudentId) && selection.ElectiveGroupId == electiveGroupId.Value)
			: new List<StudentElectiveSelection>();
		var selectionsByStudent = selections.GroupBy(selection => selection.StudentId)
			.ToDictionary(group => group.Key, group => group.First());

		// Validate the complete submission before mutating marks or flushing the unit of work.
		foreach (var row in input.Rows)
		{
			if (!studentsById.TryGetValue(row.StudentId, out var student))
				throw new EntityNotFoundException(typeof(Student), row.StudentId);
			if (student.DivisionId != input.DivisionId)
				throw new UserFriendlyException("Student does not belong to the selected division.");
			if (electiveGroupId.HasValue)
			{
				if (!selectionsByStudent.TryGetValue(row.StudentId, out var selection) || !selection.IsLocked)
					throw new UserFriendlyException("Elective subject assignment is pending for this student. Please lock elective subject selection before entering marks.");
				if (selection.SelectedSubjectId != input.SubjectId)
					throw new UserFriendlyException("This student is not assigned to the selected elective subject.");
			}

			var attendanceStatus = (ExamAttendanceStatus) row.AttendanceStatus;
			var isPresent = attendanceStatus == ExamAttendanceStatus.Present;

			if (isPresent && row.ObtainedMarks == null)
				throw new UserFriendlyException("Obtained marks are required for present students.");

			if (row.ObtainedMarks.HasValue && (row.ObtainedMarks.Value < 0 || row.ObtainedMarks.Value > (examSubject.TotalMarks ?? 100)))
				throw new UserFriendlyException($"Obtained marks must be between 0 and {examSubject.TotalMarks ?? 100}");
		}

		var marksToInsert = new List<ExamMark>();
		var marksToUpdate = new List<ExamMark>();
		foreach (var row in input.Rows)
		{
			var attendanceStatus = (ExamAttendanceStatus) row.AttendanceStatus;
			var isPresent = attendanceStatus == ExamAttendanceStatus.Present;
			marksLookup.TryGetValue(row.StudentId, out var existing);

			if (existing != null)
			{
				existing.UpdateAttendance(attendanceStatus);
				if (isPresent)
					existing.Update(row.ObtainedMarks, isAbsent: false, row.Feedback);
				marksToUpdate.Add(existing);
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
				marksToInsert.Add(mark);
			}
		}
		if (marksToInsert.Count > 0)
			await examMarkRepository.InsertManyAsync(marksToInsert, autoSave: false);
		if (marksToUpdate.Count > 0)
			await examMarkRepository.UpdateManyAsync(marksToUpdate, autoSave: false);
		await CurrentUnitOfWork!.SaveChangesAsync();

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
		await EnsureMarksWorkflowTargetAsync(examId, subjectId, divisionId);
		var result = new List<GradeEntryAuditLogDto>();

		var marks = (await examMarkRepository.GetQueryableAsync()).Where(m =>
			m.ExamId == examId && m.SubjectId == subjectId && m.DivisionId == divisionId);
		var markCount = await AsyncExecuter.CountAsync(marks);
		var lastMark = await AsyncExecuter.FirstOrDefaultAsync(marks
			.OrderByDescending(mark => mark.LastModificationTime ?? mark.CreationTime)
			.Select(mark => new { Timestamp = mark.LastModificationTime ?? mark.CreationTime, ActorId = mark.LastModifierId ?? mark.CreatorId }));

		var state = (await examMarksWorkflowStateRepository.GetListAsync(
			w => w.ExamId == examId && w.SubjectId == subjectId && w.DivisionId == divisionId))
			.FirstOrDefault();

		var userIds = new List<Guid>();
		if (lastMark?.ActorId is Guid actorId) userIds.Add(actorId);
		if (state?.LastModifierId is Guid modifierId) userIds.Add(modifierId);
		else if (state?.CreatorId is Guid creatorId && await AsyncExecuter.AnyAsync(marks.Where(mark => (mark.LastModifierId ?? mark.CreatorId) == creatorId)))
			userIds.Add(creatorId);
		var users = await AsyncExecuter.ToListAsync((await identityUserRepository.GetQueryableAsync())
			.Where(user => userIds.Contains(user.Id)).Select(user => new { user.Id, Name = user.Name ?? user.UserName }));
		var userDict = users.ToDictionary(user => user.Id, user => user.Name);

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

		if (lastMark != null)
		{
			result.Add(new GradeEntryAuditLogDto
			{
				Timestamp = lastMark.Timestamp,
				Action = "Marks Modified",
				ChangedBy = GetUserName(lastMark.ActorId),
				Details = $"Latest changes applied to student records. Active count of records: {markCount}."
			});
		}

		return result;
	}

	public async Task<List<SubjectAssignmentGroupDto>> GetSubjectsLookupAsync(Guid classId, List<Guid> divisionIds)
	{
		var assignmentsQuery = await assignmentRepository.GetQueryableAsync();
		var classAssignmentsQuery = assignmentsQuery.Where(a =>
			a.StandardId == classId &&
			a.Status == SubjectStatus.Published);
		var scopedAssignmentsQuery = await userScopeAccessService.ApplyCurrentUserScopeAsync(classAssignmentsQuery);

		var assignedSubjectIds = await GetCurrentTeacherAssignedSubjectIdsAsync(classId, divisionIds);
		if (assignedSubjectIds.Count > 0)
		{
			scopedAssignmentsQuery = scopedAssignmentsQuery
				.Concat(classAssignmentsQuery.Where(assignment => assignedSubjectIds.Contains(assignment.Id)))
				.Distinct();
		}

		var assignments = await AsyncExecuter.ToListAsync(scopedAssignmentsQuery);

		var groupedAssignments = new List<SubjectAssignmentGroupDto>();

		var groupedByElective = assignments
			.Where(a => a.SubjectType == SubjectType.Elective && a.ElectiveGroupId.HasValue)
			.GroupBy(a => a.ElectiveGroupId)
			.ToList();

		var coreSubjects = assignments
			.Where(a => a.SubjectType == SubjectType.Core || (a.SubjectType == SubjectType.Elective && !a.ElectiveGroupId.HasValue))
			.ToList();

		var subjectIds = assignments.Select(x => x.SubjectId).Distinct().ToList();
		var subjectsDict = (await subjectRepository.GetListAsync(s => subjectIds.Contains(s.Id)))
						   .ToDictionary(s => s.Id);

		var electiveGroupIds = assignments.Where(x => x.ElectiveGroupId.HasValue).Select(x => x.ElectiveGroupId.Value).Distinct().ToList();
		var electiveGroupsDict = (await electiveGroupRepository.GetListAsync(g => electiveGroupIds.Contains(g.Id)))
								 .ToDictionary(g => g.Id);

		var academicMediumId = await GetAcademicMediumIdForStandardAsync(classId);
		var validDivisionIds = (divisionIds ?? new List<Guid>()).Where(id => id != Guid.Empty).Distinct().ToList();

		var students = new List<Student>();
		var selections = new List<StudentElectiveSelection>();

		if (validDivisionIds.Count > 0)
		{
			students = await studentRepository.GetListAsync(s =>
				s.AcademicMediumId == academicMediumId &&
				s.StandardId == classId &&
				s.DivisionId.HasValue &&
				validDivisionIds.Contains(s.DivisionId.Value) &&
				s.Status == StudentLifeCycleStatusEnum.Enrolled &&
				s.AdmissionApprovalStatus == StudentEnrollmentApprovalState.Approved &&
				!s.IsDeleted);

			if (students.Count > 0 && electiveGroupIds.Count > 0)
			{
				var studentIds = students.Select(s => s.Id).ToList();
				selections = await studentElectiveSelectionRepository.GetListAsync(s =>
					studentIds.Contains(s.StudentId) &&
					electiveGroupIds.Contains(s.ElectiveGroupId));
			}
		}

		foreach (var coreAssignment in coreSubjects)
		{
			subjectsDict.TryGetValue(coreAssignment.SubjectId, out var subject);

			groupedAssignments.Add(new SubjectAssignmentGroupDto
			{
				Id = coreAssignment.Id,
				SubjectType = coreAssignment.SubjectType,
				Status = coreAssignment.Status,
				Color = subject?.Color,
				IsDisabled = false,
				DisabledReason = null,
				Subjects = new List<SubjectLiteDto>
				{
					new SubjectLiteDto
					{
						SubjectType = coreAssignment.SubjectType,
						SubjectId = coreAssignment.SubjectId,
						SubjectName = subject?.Name ?? "",
						SubjectCode = coreAssignment.SubjectCode ?? "",
						Color = subject?.Color ?? ""
					}
				}
			});
		}

		foreach (var group in groupedByElective)
		{
			var first = group.First();
			var egId = first.ElectiveGroupId!.Value;
			electiveGroupsDict.TryGetValue(egId, out var electiveGroup);

			bool isElectiveReady = true;
			string? disabledReason = null;

			if (validDivisionIds.Count == 0)
			{
				isElectiveReady = false;
				disabledReason = "Please select a division first.";
			}
			else if (students.Count > 0)
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
						.Where(s => s.ElectiveGroupId == egId && divisionStudentIds.Contains(s.StudentId))
						.ToList();

					var isReady = divisionSelections.Count == divisionStudentIds.Count && divisionSelections.All(s => s.IsLocked);
					if (!isReady)
					{
						isElectiveReady = false;
						disabledReason = "This subject is not finalized from class management.";
						break;
					}
				}
			}

			groupedAssignments.Add(new SubjectAssignmentGroupDto
			{
				Id = first.Id,
				SubjectType = SubjectType.Elective,
				Status = first.Status,
				ElectiveGroupId = first.ElectiveGroupId,
				ElectiveGroupName = electiveGroup?.Name,
				Color = electiveGroup?.Color,
				IsDisabled = !isElectiveReady,
				DisabledReason = disabledReason,
				Subjects = group.Select(a =>
				{
					subjectsDict.TryGetValue(a.SubjectId, out var sub);
					return new SubjectLiteDto
					{
						SubjectType = a.SubjectType,
						SubjectId = a.SubjectId,
						SubjectName = sub?.Name ?? "",
						SubjectCode = a.SubjectCode ?? "",
						Color = sub?.Color ?? ""
					};
				}).ToList()
			});
		}

		return groupedAssignments;
	}

	private async Task<HashSet<Guid>> GetCurrentTeacherAssignedSubjectIdsAsync(
		Guid classId,
		IReadOnlyCollection<Guid>? divisionIds)
	{
		if (!CurrentUser.Id.HasValue || CurrentUser.Roles.Any(role =>
			string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(role, "administrator", StringComparison.OrdinalIgnoreCase)))
		{
			return new HashSet<Guid>();
		}

		var currentEmployee = await employeeRepository.FirstOrDefaultAsync(
			employee => employee.UserId == CurrentUser.Id.Value);
		if (currentEmployee == null)
		{
			return new HashSet<Guid>();
		}

		var assignmentsWithTeachersQuery = await assignmentRepository.WithDetailsAsync(
			assignment => assignment.Teachers);
		var assignedSubjectIds = (await AsyncExecuter.ToListAsync(
			assignmentsWithTeachersQuery.Where(assignment =>
				assignment.StandardId == classId &&
				assignment.Status == SubjectStatus.Published &&
				assignment.Teachers.Any(teacher => teacher.TeacherId == currentEmployee.Id))))
			.Select(assignment => assignment.Id)
			.ToHashSet();

		var validDivisionIds = (divisionIds ?? Array.Empty<Guid>())
			.Where(id => id != Guid.Empty)
			.ToHashSet();
		var allocations = await teacherSubjectAllocationRepository.GetListAsync(allocation =>
			allocation.StandardId == classId &&
			(!validDivisionIds.Any() || validDivisionIds.Contains(allocation.DivisionId)) &&
			allocation.Teachers.Any(teacher => teacher.TeacherId == currentEmployee.Id));
		var allocatedSubjectIds = allocations.Select(allocation => allocation.SubjectId).ToHashSet();

		if (allocatedSubjectIds.Count > 0)
		{
			var allocatedAssignmentIds = await AsyncExecuter.ToListAsync(
				assignmentsWithTeachersQuery
					.Where(assignment =>
						assignment.StandardId == classId &&
						assignment.Status == SubjectStatus.Published &&
						allocatedSubjectIds.Contains(assignment.SubjectId))
					.Select(assignment => assignment.Id));
			assignedSubjectIds.UnionWith(allocatedAssignmentIds);
		}

		return assignedSubjectIds;
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
