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
	IRepository<ElectiveGroup, Guid> electiveGroupRepository
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

			// Block deletion if this subject is the last one in an elective group
			var allGroups = await electiveGroupRepository.WithDetailsAsync(eg => eg.Subjects);
			var blockingGroup = allGroups
				.FirstOrDefault(eg => eg.Subjects.Count == 1 && eg.Subjects.Any(s => s.SubjectId == id));
			if (blockingGroup != null)
			{
				throw new BusinessException(CurriculumErrorCodes.SubjectIsLastInElectiveGroup)
					.WithData("message", $"Cannot delete '{subject.Name}' because it is the only subject in elective group '{blockingGroup.Name}'. Please delete the elective group first.");
			}

			// Block deletion if the subject is in use anywhere
			var usageParts = new System.Collections.Generic.List<string>();

			var allocationCount = await assignedSubjectRepository.CountAsync(a => a.SubjectId == id);
			if (allocationCount > 0)
				usageParts.Add($"{allocationCount} class allocation(s)");

			var weeklySlotCount = await weeklyLectureSlotSubjectRepository.CountAsync(w => w.SubjectId == id);
			if (weeklySlotCount > 0)
				usageParts.Add($"{weeklySlotCount} weekly lecture slot(s)");

			var timetableSlotCount = await timetableSlotSubjectRepository.CountAsync(t => t.SubjectId == id);
			if (timetableSlotCount > 0)
				usageParts.Add($"{timetableSlotCount} timetable slot(s)");

			var examSubjectCount = await examSubjectRepository.CountAsync(e => e.SubjectId == id);
			if (examSubjectCount > 0)
				usageParts.Add($"{examSubjectCount} exam(s)");

			var examMarkCount = await examMarkRepository.CountAsync(m => m.SubjectId == id);
			if (examMarkCount > 0)
				usageParts.Add($"{examMarkCount} exam result(s)");

			var homeworkCount = await homeworkAssignmentRepository.CountAsync(h => h.SubjectId == id);
			if (homeworkCount > 0)
				usageParts.Add($"{homeworkCount} homework(s)");

			var assignmentCount = await assignmentRepository.CountAsync(a => a.SubjectId == id);
			if (assignmentCount > 0)
				usageParts.Add($"{assignmentCount} assignment(s)");

			if (usageParts.Count > 0)
			{
				throw new BusinessException(CurriculumErrorCodes.SubjectIsInUse)
					.WithData("message", $"Cannot delete '{subject.Name}' because it is in use: {string.Join(", ", usageParts)}. Please remove these usages first.");
			}

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
