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

		var schedules = input.Subjects.Select(s => (
			s.SubjectId,
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

		// Fetch assigned subjects for this standard → SubjectCode + ElectiveGroupId
		var assignedDict = (await assignmentRepository.GetListAsync(
				a => a.StandardId == exam.StandardId && subjectIds.Contains(a.SubjectId)))
			.GroupBy(a => a.SubjectId)
			.ToDictionary(g => g.Key, g => g.First());

		// Fetch elective group metadata (Name, Color)
		var electiveGroupIds = assignedDict.Values
			.Where(a => a.ElectiveGroupId.HasValue)
			.Select(a => a.ElectiveGroupId!.Value)
			.Distinct()
			.ToList();
		var electiveGroupDict = electiveGroupIds.Count > 0
			? (await electiveGroupRepository.GetListAsync(eg => electiveGroupIds.Contains(eg.Id))).ToDictionary(eg => eg.Id)
			: new Dictionary<Guid, ElectiveGroup>();

		// Build elective group → subjects map from the exam's own assigned subjects
		var electiveGroupSubjectsMap = assignedDict.Values
			.Where(a => a.ElectiveGroupId.HasValue)
			.GroupBy(a => a.ElectiveGroupId!.Value)
			.ToDictionary(
				g => g.Key,
				g => g.Select(a =>
				{
					subjectDict.TryGetValue(a.SubjectId, out var s);
					return new ExamElectiveGroupSubjectDto
					{
						SubjectId = a.SubjectId,
						SubjectName = s?.Name,
						Color = s?.Color
					};
				}).ToList()
			);

		return new ExamScheduleResponseDto
		{
			Subjects = exam.ExamSubjects.Select(s =>
			{
				subjectDict.TryGetValue(s.SubjectId, out var subject);
				assignedDict.TryGetValue(s.SubjectId, out var assigned);
				ElectiveGroup? eg = null;
				if (assigned?.ElectiveGroupId.HasValue == true)
					electiveGroupDict.TryGetValue(assigned.ElectiveGroupId!.Value, out eg);

				return new ExamScheduleSubjectDto
				{
					SubjectId = s.SubjectId,
					SubjectName = subject?.Name,
					SubjectCode = assigned?.SubjectCode,
					Color = subject?.Color,
					ElectiveGroupId = assigned?.ElectiveGroupId,
					ElectiveGroupName = eg?.Name,
					ElectiveGroupColor = eg?.Color,
					ElectiveGroupSubjects = eg != null && assigned?.ElectiveGroupId.HasValue == true
						? electiveGroupSubjectsMap.GetValueOrDefault(assigned.ElectiveGroupId!.Value, [])
						: [],
					Notes = s.Notes,
					ExamDate = s.ExamDate,
					StartTime = s.StartTime,
					EndTime = s.EndTime,
					TotalMarks = s.TotalMarks,
					PassingMarks = s.PassingMarks,
					GradeScalingId = s.GradeScalingId
				};
			}).ToList()
		};
	}

	public async Task AssignRoomsAsync(Guid examId, ExamRoomAssignmentRequestDto input)
	{
		var exam = await examRepository.GetAsync(examId, includeDetails: true);

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
			Divisions = x.exam.ExamDivisions.Select(d => divisionDict.GetValueOrDefault(d.DivisionId) ?? string.Empty).ToList()
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
				Grade = assigned ? GetGrade(mark!.MarksObtained, examSubject.TotalMarks ?? 0) : null,
				Feedback = mark?.Feedback,
				Status = assigned ? 1 : 0
			};
		}).ToList();

		return new GetExamMarksResponseDto
		{
			ExamId = exam.Id,
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
			existing.Update(input.Marks, isAbsent: false, input.Feedback);
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
				input.Marks,
				isAbsent: false,
				input.Feedback,
				CurrentTenant.Id);
			await examMarkRepository.InsertAsync(mark, autoSave: true);
		}

		var grade = GetGrade(input.Marks, examSubject.TotalMarks ?? input.TotalMarks);

		return new UpdateStudentMarksResponseDto
		{
			Message = "Marks updated successfully",
			Marks = input.Marks,
			TotalMarks = examSubject.TotalMarks ?? input.TotalMarks,
			Grade = grade,
			Feedback = input.Feedback
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
}