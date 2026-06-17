using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Exams.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using System;
using Volo.Abp;
using System.Linq;

using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Identity;

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
	IRepository<ExamMark, Guid> examMarkRepository,
	IRepository<Student, Guid> studentRepository,
	IRepository<GradingScale, Guid> gradingScaleRepository,
	IRepository<ExamSubjectDivision, Guid> examSubjectDivisionRepository,
	IRepository<ExamMarksWorkflowState, Guid> examMarksWorkflowStateRepository,
	ExamManager examManager) : ApplicationService, IExamAppService
{
	public async Task<Guid> CreateBasicDetailsAsync(ExamBasicDetailsRequestDto input)
	{
		var exam = examManager.Create(
			input.ExamTypeId,
			input.ExamName,
			input.StandardId,
			input.DivisionIds,
			input.SubjectIds
		);

		await examRepository.InsertAsync(exam);
		return exam.Id;
	}

	public async Task UpdateBasicDetailsAsync(Guid examId, ExamBasicDetailsRequestDto input)
	{
		var exam = await examRepository.GetAsync(examId, includeDetails: true);

		examManager.UpdateBasicDetails(
			exam,
			input.ExamName,
			input.ExamTypeId,
			input.StandardId,
			input.DivisionIds,
			input.SubjectIds
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
			DivisionIds = exam.ExamDivisions.Select(x => x.DivisionId).ToList(),
			SubjectIds = exam.ExamSubjects.Select(x => x.SubjectId).ToList()
		};
	}

	public async Task SaveScheduleAsync(Guid examId, ExamScheduleRequestDto input)
	{
		var exam = await examRepository.GetAsync(examId, includeDetails: true);

		await ValidateScheduleConflictsAsync(examId, exam, input);

		var schedules = input.Subjects.Select(s => (
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

			var groupSubjects = group.Select(s =>
			{
				subjectDict.TryGetValue(s.SubjectId, out var subject);
				return new ExamElectiveGroupSubjectDto
				{
					SubjectId = s.SubjectId,
					SubjectName = subject?.Name,
					Color = subject?.Color,
					ExamDate = s.ExamDate,
					StartTime = s.StartTime,
					EndTime = s.EndTime,
					TotalMarks = s.TotalMarks,
					PassingMarks = s.PassingMarks,
					Notes = s.Notes,
					GradeScalingId = s.GradeScalingId
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

	public async Task DeleteAsync(Guid examId)
	{
		var exam = await examRepository.GetAsync(examId);

		if (exam.Status != ExamStatus.Pending)
		{
			throw new UserFriendlyException("Completed exams cannot be deleted");
		}

		await examRepository.DeleteAsync(exam);
	}

	public async Task ChangeStatusAsync(Guid examId, ExamStatus status)
	{
		var exam = await examRepository.GetAsync(examId);
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

		var pendingCount = await AsyncExecuter.CountAsync(baseQuery.Where(x => x.exam.Status == ExamStatus.Pending));
		var publishedCount = await AsyncExecuter.CountAsync(baseQuery.Where(x => x.exam.Status == ExamStatus.Published));
		var completedCount = await AsyncExecuter.CountAsync(baseQuery.Where(x => x.exam.Status == ExamStatus.Completed));

		var query = baseQuery.Where(x => x.exam.Status == input.Status);

		if (input.SubjectId.HasValue)
		{
			query = query.Where(x => x.exam.ExamSubjects.Any(s => s.SubjectId == input.SubjectId.Value));
		}

		var totalCount = await AsyncExecuter.CountAsync(query);

		var pagedQuery = query.OrderBy(x => x.exam.CreationTime)
							  .PageBy(input);

		var queryResult = await AsyncExecuter.ToListAsync(pagedQuery);

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

		var items = queryResult.Select(x => new ExamTableItemDto
		{
			ExamId = x.exam.Id,
			ExamName = x.exam.ExamName,
			ExamTypeName = x.examType.Name,
			StandardName = x.standard.Name,
			CreatedBy = x.exam.CreatorId,
			CreatorName = x.exam.CreatorId.HasValue ? creatorNameById.GetValueOrDefault(x.exam.CreatorId.Value) : null,
			Status = x.exam.Status,
			Divisions = x.exam.ExamDivisions.Select(d => divisionDict.GetValueOrDefault(d.DivisionId) ?? string.Empty).OrderBy(name => name).ToList()
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

		// Students in this division (enrolled + approved)
		var allStudents = await studentRepository.GetListAsync(s =>
			s.DivisionId == input.DivisionId
			&& s.Status == StudentLifeCycleStatusEnum.Enrolled
			&& s.AdmissionApprovalStatus == StudentEnrollmentApprovalState.Approved
			&& !s.IsDeleted);

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

		// Grading scales
		var gradingScales = await gradingScaleRepository.WithDetailsAsync(gs => gs.GradeRanges);
		var gradeRanges = (await AsyncExecuter.ToListAsync(gradingScales))
			.SelectMany(gs => gs.GradeRanges)
			.OrderByDescending(gr => gr.MinMarks)
			.ToList();

		string? GetGrade(double? marks, double totalMarks)
		{
			if (marks == null || totalMarks == 0) return null;
			var pct = (marks.Value / totalMarks) * 100;
			return gradeRanges.FirstOrDefault(gr => pct >= gr.MinMarks && pct <= gr.MaxMarks)?.Grade;
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
				AttendanceStatus = mark != null ? (int) mark.AttendanceStatus : (int) ExamAttendanceStatus.Present
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

		// Grading
		var gradingScales = await gradingScaleRepository.WithDetailsAsync(gs => gs.GradeRanges);
		var gradeRanges = (await AsyncExecuter.ToListAsync(gradingScales))
			.SelectMany(gs => gs.GradeRanges)
			.OrderByDescending(gr => gr.MinMarks)
			.ToList();

		string? GetGrade(double marks, double totalMarks)
		{
			if (totalMarks == 0) return null;
			var pct = (marks / totalMarks) * 100;
			return gradeRanges.FirstOrDefault(gr => pct >= gr.MinMarks && pct <= gr.MaxMarks)?.Grade;
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
				student.DivisionId!.Value,
				marksObtained: isPresent ? input.Marks : null,
				isAbsent: !isPresent,
				feedback: isPresent ? input.Feedback : null,
				CurrentTenant.Id);
			mark.UpdateAttendance(attendanceStatus);
			await examMarkRepository.InsertAsync(mark, autoSave: true);
		}

		var grade = isPresent ? GetGrade(input.Marks!.Value, examSubject.TotalMarks ?? input.TotalMarks) : null;

		return new UpdateStudentMarksResponseDto
		{
			Message = isPresent ? "Marks saved successfully." : "Attendance saved successfully.",
			Marks = isPresent ? input.Marks : null,
			TotalMarks = examSubject.TotalMarks ?? input.TotalMarks,
			Grade = grade,
			Feedback = isPresent ? input.Feedback : null,
			AttendanceStatus = input.AttendanceStatus
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.Add)]
	public async Task<PublishMarksResponseDto> PublishMarksAsync(Guid examId, Guid divisionId)
	{
		var exam = await examRepository.GetAsync(examId);

		var existingMarks = await examMarkRepository.GetListAsync(m =>
			m.ExamId == examId && m.DivisionId == divisionId);

		if (existingMarks.Count == 0)
			throw new UserFriendlyException("No marks found for this exam and division");

		if (existingMarks.All(m => m.IsPublished))
			throw new BusinessException("Academic:Exam:MarksAlreadyPublished", "Marks have already been published for this exam and division");

		foreach (var mark in existingMarks)
			mark.Publish();

		await examMarkRepository.UpdateManyAsync(existingMarks, autoSave: true);

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
	private async Task ValidateScheduleConflictsAsync(Guid examId, Exam currentExam, ExamScheduleRequestDto input)
	{
		// Only validate entries that have a fully-specified time window
		var incomingScheduled = input.Subjects
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
			.Where(es => es.StartTime.HasValue && es.EndTime.HasValue)
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
					es.StartTime!.Value.Date == incomingStart.Date &&
					es.StartTime < incomingEnd &&
					es.EndTime > incomingStart)
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
		var state = await GetOrCreateWorkflowStateAsync(examId, input.SubjectId, input.DivisionId);

		if (state.Status == ExamMarksWorkflowStatus.Published)
			throw new UserFriendlyException("Marks are published/locked and cannot be edited.");

		// Draft keeps the status at Draft (0); if already beyond Draft, do not regress.
		if (state.Status == ExamMarksWorkflowStatus.Draft)
			await examMarksWorkflowStateRepository.UpdateAsync(state, autoSave: true);

		return BuildWorkflowResponse("Draft saved successfully.", examId, input, state.Status);
	}

	// ─── 2. Submit Marks ───────────────────────────────────────────────────────

	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.Add)]
	public async Task<MarksWorkflowActionResponseDto> SubmitMarksAsync(Guid examId, MarksWorkflowActionInput input)
	{
		var state = await GetOrCreateWorkflowStateAsync(examId, input.SubjectId, input.DivisionId);

		if (state.Status != ExamMarksWorkflowStatus.Draft)
			throw new UserFriendlyException("Marks can only be submitted from Draft status.");

		// Validate: all enrolled students must have marks (status = Assigned)
		var enrolledStudents = await studentRepository.GetListAsync(s =>
			s.DivisionId == input.DivisionId
			&& s.Status == StudentLifeCycleStatusEnum.Enrolled
			&& s.AdmissionApprovalStatus == StudentEnrollmentApprovalState.Approved
			&& !s.IsDeleted);

		var enteredMarks = await examMarkRepository.GetListAsync(m =>
			m.ExamId == examId && m.SubjectId == input.SubjectId && m.DivisionId == input.DivisionId);

		var enteredStudentIds = new HashSet<Guid>(enteredMarks.Select(m => m.StudentId));
		var allEntered = enrolledStudents.All(s => enteredStudentIds.Contains(s.Id));

		if (!allEntered)
			throw new UserFriendlyException("All students must have marks entered before submission.");

		state.TransitionTo(ExamMarksWorkflowStatus.Submitted);
		await examMarksWorkflowStateRepository.UpdateAsync(state, autoSave: true);

		return BuildWorkflowResponse("Marks submitted for verification.", examId, input, ExamMarksWorkflowStatus.Submitted);
	}

	// ─── 3. Verify Marks ───────────────────────────────────────────────────────

	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.Add)]
	public async Task<MarksWorkflowActionResponseDto> VerifyMarksAsync(Guid examId, MarksWorkflowActionInput input)
	{
		var state = await GetOrCreateWorkflowStateAsync(examId, input.SubjectId, input.DivisionId);

		if (state.Status != ExamMarksWorkflowStatus.Submitted)
			throw new UserFriendlyException("Marks can only be verified from Submitted status.");

		state.TransitionTo(ExamMarksWorkflowStatus.Verified);
		await examMarksWorkflowStateRepository.UpdateAsync(state, autoSave: true);

		return BuildWorkflowResponse("Marks verified successfully.", examId, input, ExamMarksWorkflowStatus.Verified);
	}

	// ─── 4. Publish and Lock Marks ─────────────────────────────────────────────

	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.Add)]
	public async Task<MarksWorkflowActionResponseDto> PublishAndLockAsync(Guid examId, MarksWorkflowActionInput input)
	{
		var state = await GetOrCreateWorkflowStateAsync(examId, input.SubjectId, input.DivisionId);

		if (state.Status != ExamMarksWorkflowStatus.Verified)
			throw new UserFriendlyException("Marks can only be published from Verified status.");

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

		// Check for locked state
		var state = (await examMarksWorkflowStateRepository.GetListAsync(
			w => w.ExamId == examId && w.SubjectId == subjectId && w.DivisionId == divisionId))
			.FirstOrDefault();

		if (state?.Status == ExamMarksWorkflowStatus.Published)
			throw new UserFriendlyException("Attendance cannot be changed when marks are published/locked.");

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

		var enrolledStudents = await studentRepository.GetListAsync(s =>
			s.DivisionId == input.DivisionId
			&& s.Status == StudentLifeCycleStatusEnum.Enrolled
			&& s.AdmissionApprovalStatus == StudentEnrollmentApprovalState.Approved
			&& !s.IsDeleted);

		var existingMarks = await examMarkRepository.GetListAsync(m =>
			m.ExamId == input.ExamId && m.SubjectId == input.SubjectId && m.DivisionId == input.DivisionId);

		var marksEntered = existingMarks.Count(m => m.MarksObtained.HasValue);
		var totalStudents = enrolledStudents.Count;
		var pending = totalStudents - marksEntered;

		var workflowState = (await examMarksWorkflowStateRepository.GetListAsync(
			w => w.ExamId == input.ExamId && w.SubjectId == input.SubjectId && w.DivisionId == input.DivisionId))
			.FirstOrDefault();

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
			WorkflowStatus = workflowState != null ? (int) workflowState.Status : (int) ExamMarksWorkflowStatus.Draft
		};
	}
}