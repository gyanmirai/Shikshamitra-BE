using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Academic.Examination.Exams;

public class ExamSubject : Entity<Guid>
{
    public Guid ExamId { get; private set; }
    public Guid SubjectId { get; private set; }
    public DateTime? ExamDate { get; private set; }
    public DateTime? StartTime { get; private set; }
    public DateTime? EndTime { get; private set; }
    public double? TotalMarks { get; private set; }
    public double? PassingMarks { get; private set; }
    public string? Notes { get; private set; }

    public ICollection<ExamSubjectDivision> RoomAssignments { get; private set; }

    private ExamSubject()
    {
        RoomAssignments = new Collection<ExamSubjectDivision>();
    }

    public ExamSubject(
        Guid id,
        Guid examId,
        Guid subjectId,
        DateTime? examDate,
        DateTime? startTime,
        DateTime? endTime,
        double? totalMarks,
        double? passingMarks,
        string? notes = null) : base(id)
    {
        ExamId = examId;
        SubjectId = subjectId;
        ExamDate = examDate;
        StartTime = startTime;
        EndTime = endTime;
        TotalMarks = totalMarks;
        PassingMarks = passingMarks;
        Notes = notes;

        RoomAssignments = new Collection<ExamSubjectDivision>();
    }

    public ExamSubject(
        Guid id,
        Guid examId,
        Guid subjectId) : base(id)
    {
        ExamId = examId;
        SubjectId = subjectId;

        RoomAssignments = new Collection<ExamSubjectDivision>();
    }

    internal void SetExamId(Guid examId)
    {
        ExamId = examId;
    }

    public void UpdateSchedule(DateTime? examDate, DateTime? startTime, DateTime? endTime, double? totalMarks, double? passingMarks, string? notes)
    {
        ExamDate = examDate;
        StartTime = startTime;
        EndTime = endTime;
        TotalMarks = totalMarks;
        PassingMarks = passingMarks;
        Notes = notes;
    }

    public void AddRoomAssignment(ExamSubjectDivision assignment)
    {
        assignment.SetExamSubjectId(Id);
        RoomAssignments.Add(assignment);
    }
    
    public void RemoveRoomAssignment(Guid assignmentId)
    {
        var assignment = RoomAssignments.FirstOrDefault(x => x.Id == assignmentId);
        if (assignment != null)
        {
            RoomAssignments.Remove(assignment);
        }
    }
}
