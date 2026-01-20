using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Exams.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using System;
using Volo.Abp;
using System.Linq;

namespace ShikshaMitra.Academic.Examination.Exams;

[RemoteService(false)]
public class ExamAppService(
	IRepository<Exam, Guid> examRepository,
	ExamManager examManager) : ApplicationService, IExamAppService
{
    public async Task CreateBasicDetailsAsync(ExamBasicDetailsRequestDto input)
    {
        var exam = examManager.Create(
            input.ExamTypeId,
            input.ExamName,
            input.StandardId,
            input.DivisionIds,
            input.SubjectIds
        );

        await examRepository.InsertAsync(exam);
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
}
