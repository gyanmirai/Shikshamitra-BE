using System;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectUsageCheckDto
{
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;

    /// <summary>True if the subject is referenced by at least one other record.</summary>
    public bool HasUsages { get; set; }

    /// <summary>Subject-allocation rows in academic_StandardSubjectAssignments.</summary>
    public int Allocations { get; set; }

    /// <summary>Weekly lecture-slot configuration rows referencing this subject.</summary>
    public int WeeklyLectureSlots { get; set; }

    /// <summary>Timetable-slot rows referencing this subject.</summary>
    public int TimetableSlots { get; set; }

    /// <summary>Exam-subject rows referencing this subject.</summary>
    public int ExamSubjects { get; set; }

    /// <summary>Student exam result (mark) rows referencing this subject.</summary>
    public int ExamResults { get; set; }

    /// <summary>Homework assignments referencing this subject.</summary>
    public int Homeworks { get; set; }

    /// <summary>Assignments referencing this subject.</summary>
    public int Assignments { get; set; }
}
