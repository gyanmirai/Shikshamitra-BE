using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.TenantSettings.EducationBoards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Curriculum;

[RemoteService(IsEnabled = false)]
public class SubjectAppService(
		IRepository<Subject, Guid> subjectRepository,
		SubjectManager subjectManager,
		IRepository<AssignedSubject, Guid> assignedSubjectRepository,
		IRepository<WeeklyLectureSlotSubject, Guid> weeklyLectureSlotSubjectRepository,
		IRepository<TimetableSlotSubject, Guid> timetableSlotSubjectRepository,
		IRepository<ExamSubject, Guid> examSubjectRepository,
		IRepository<ExamMark, Guid> examMarkRepository,
		IRepository<HomeworkAssignmentEntity, Guid> homeworkAssignmentRepository,
	IRepository<AssignmentEntity, Guid> assignmentRepository,
	ILogger<SubjectAppService> logger,
	AcademicOrganizationScopeAccessService scopeAccessService,
	IRepository<ElectiveGroup, Guid> electiveGroupRepository,
	ElectiveGroupManager electiveGroupManager
) : ApplicationService, ISubjectAppService
{
	public async Task<SubjectDto> GetAsync(Guid id)
	{
		try
		{
			var subject = await subjectRepository.GetAsync(id);
			return ObjectMapper.Map<Subject, SubjectDto>(subject);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error retrieving subject with ID {SubjectId}", id);
			throw new UserFriendlyException("Error occurred while retrieving subject");
		}
	}

	public async Task<List<SubjectDto>> GetListAsync(
		Guid educationMediumId,
		string? searchTerm = null
	)
	{
		try
		{
			if (!await scopeAccessService.CanAccessMediumAsync(educationMediumId))
			{
				return [];
			}

			var queryable = await subjectRepository.GetQueryableAsync();

			// Filter by education medium
			queryable = queryable.Where(x => x.EducationMediumId == educationMediumId);

			// Apply search filter if provided
			if (!string.IsNullOrWhiteSpace(searchTerm))
			{
				queryable = queryable.Where(x => x.Name.Contains(searchTerm));
			}

			// Use ABP's AsyncExecuter for asynchronous query execution
			var subjects = await AsyncExecuter.ToListAsync(queryable);

			return ObjectMapper.Map<List<Subject>, List<SubjectDto>>(subjects);
		}
		catch (UserFriendlyException ex)
		{
			logger.LogError(ex.Message);
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(
				ex,
				"Error retrieving subjects for medium {EducationMediumId}",
				educationMediumId
			);
			throw new UserFriendlyException("Error occurred while retrieving subjects");
		}
	}

	public async Task<SubjectDto> CreateAsync(CreateUpdateSubjectDto createSubjectDto)
	{
		try
		{
			Subject subject = await subjectManager.CreateSubject(
				createSubjectDto.Name,
				createSubjectDto.Color,
				createSubjectDto.EducationMediumId
			);

			await subjectRepository.InsertAsync(subject);

			return ObjectMapper.Map<Subject, SubjectDto>(subject);
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(
				ex,
				"Error creating subject with name {SubjectName}",
				createSubjectDto.Name
			);
			throw new UserFriendlyException("Error occurred while creating subject");
		}
	}

	public async Task<SubjectDto> UpdateAsync(Guid id, CreateUpdateSubjectDto updateSubjectDto)
	{
		try
		{
			var subject = await subjectManager.UpdateAsync(
				id,
				updateSubjectDto.Name,
				updateSubjectDto.Color,
				updateSubjectDto.EducationMediumId
			);

			await subjectRepository.UpdateAsync(subject);

			return ObjectMapper.Map<Subject, SubjectDto>(subject);
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error updating subject with ID {SubjectId}", id);
			throw new UserFriendlyException("Error occurred while updating subject");
		}
	}

	public async Task DeleteAsync(Guid id)
	{
		try
		{
			var subject = await subjectRepository.GetAsync(id);

			// Issue 1: auto-delete any elective group that would have 0 subjects after this deletion
			var allGroups = await electiveGroupRepository.WithDetailsAsync(eg => eg.Subjects);
			var orphanedGroupIds = allGroups
				.Where(eg => eg.Subjects.Count == 1 && eg.Subjects.Any(s => s.SubjectId == id))
				.Select(eg => eg.Id)
				.ToList();
			foreach (var groupId in orphanedGroupIds)
				await electiveGroupManager.DeleteWithCascadeAsync(groupId);

			// Cascade: subject allocations (AppStandardSubjectAssignments)
			var allocations = await assignedSubjectRepository.GetListAsync(a => a.SubjectId == id);
			foreach (var allocation in allocations)
				await assignedSubjectRepository.DeleteAsync(allocation);

			// Cascade: weekly lecture-slot subjects (AppWeeklyLectureSlotSubjects)
			var weeklySlots = await weeklyLectureSlotSubjectRepository.GetListAsync(w => w.SubjectId == id);
			foreach (var slot in weeklySlots)
				await weeklyLectureSlotSubjectRepository.DeleteAsync(slot);

			// Cascade: timetable-slot subjects (AppTimetableSlotSubjects — no soft delete)
			var timetableSlots = await timetableSlotSubjectRepository.GetListAsync(t => t.SubjectId == id);
			foreach (var slot in timetableSlots)
				await timetableSlotSubjectRepository.DeleteAsync(slot, autoSave: true);

			// Cascade: exam results/marks (AppExamMarks) — must come before exam subjects
			var examMarks = await examMarkRepository.GetListAsync(m => m.SubjectId == id);
			foreach (var mark in examMarks)
				await examMarkRepository.DeleteAsync(mark);

			// Cascade: exam subjects (AppExamSubjects — no soft delete; ExamSubjectDivisions cascade at DB level)
			var examSubjects = await examSubjectRepository.GetListAsync(e => e.SubjectId == id);
			foreach (var es in examSubjects)
				await examSubjectRepository.DeleteAsync(es, autoSave: true);

			// Cascade: homeworks (AppHomeworkAssignments)
			var homeworks = await homeworkAssignmentRepository.GetListAsync(h => h.SubjectId == id);
			foreach (var hw in homeworks)
				await homeworkAssignmentRepository.DeleteAsync(hw);

			// Cascade: assignments (AppAssignments)
			var assignments = await assignmentRepository.GetListAsync(a => a.SubjectId == id);
			foreach (var asgn in assignments)
				await assignmentRepository.DeleteAsync(asgn);

			await subjectRepository.DeleteAsync(subject);
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error deleting subject with ID {SubjectId}", id);
			throw new UserFriendlyException("Error occurred while deleting subject");
		}
	}

	public async Task<SubjectUsageCheckDto> CheckUsagesAsync(Guid id)
	{
		try
		{
			var subject = await subjectRepository.GetAsync(id);

			var allocations = await assignedSubjectRepository.CountAsync(a => a.SubjectId == id);
			var weeklySlots = await weeklyLectureSlotSubjectRepository.CountAsync(w => w.SubjectId == id);
			var timetableSlots = await timetableSlotSubjectRepository.CountAsync(t => t.SubjectId == id);
			var examSubjects = await examSubjectRepository.CountAsync(e => e.SubjectId == id);
			var examResults = await examMarkRepository.CountAsync(m => m.SubjectId == id);
			var homeworks = await homeworkAssignmentRepository.CountAsync(h => h.SubjectId == id);
			var assignments = await assignmentRepository.CountAsync(a => a.SubjectId == id);

			var total = allocations + weeklySlots + timetableSlots + examSubjects + examResults + homeworks + assignments;

			return new SubjectUsageCheckDto
			{
				SubjectId = id,
				SubjectName = subject.Name,
				HasUsages = total > 0,
				Allocations = allocations,
				WeeklyLectureSlots = weeklySlots,
				TimetableSlots = timetableSlots,
				ExamSubjects = examSubjects,
				ExamResults = examResults,
				Homeworks = homeworks,
				Assignments = assignments
			};
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error checking usages for subject {SubjectId}", id);
			throw new UserFriendlyException("Error occurred while checking subject usages");
		}
	}
}
