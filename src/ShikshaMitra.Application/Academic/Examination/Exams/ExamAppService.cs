using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Exams.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using System;
using Volo.Abp;
using System.Linq;

using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Settings;
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
			s.Notes
		)).ToList();

		examManager.SaveSchedule(exam, schedules);

		await examRepository.UpdateAsync(exam);
	}

	public async Task<ExamScheduleResponseDto> GetScheduleAsync(Guid examId)
	{
		var exam = await examRepository.GetAsync(examId, includeDetails: true);

		return new ExamScheduleResponseDto
		{
			Subjects = exam.ExamSubjects.Select(s => new ExamSubjectScheduleDto
			{
				SubjectId = s.SubjectId,
				ExamDate = s.ExamDate,
				StartTime = s.StartTime,
				EndTime = s.EndTime,
				TotalMarks = s.TotalMarks,
				PassingMarks = s.PassingMarks,
				Notes = s.Notes
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

	public async Task<PagedResultDto<ExamTableItemDto>> GetListAsync(GetExamsInput input)
	{
		var examQuery = await examRepository.GetQueryableAsync();
		var standardQuery = await standardRepository.GetQueryableAsync();
		var stageQuery = await educationMediumStageRepository.GetQueryableAsync();
		var examTypeQuery = await examTypeRepository.GetQueryableAsync();

		var query = from exam in examQuery
					join standard in standardQuery on exam.StandardId equals standard.Id
					join stage in stageQuery on standard.EducationMediumStageId equals stage.Id
					join examType in examTypeQuery on exam.ExamTypeId equals examType.Id
					where stage.EducationMediumId == input.AcademicMediumId && exam.Status == input.Status
					select new { exam, standard, examType };

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

		return new PagedResultDto<ExamTableItemDto>(totalCount, items);
	}
}