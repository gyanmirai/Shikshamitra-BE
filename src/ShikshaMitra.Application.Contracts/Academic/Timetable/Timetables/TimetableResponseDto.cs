using System;
using System.Collections.Generic;
using ShikshaMitra.Academic.Curriculum;

namespace ShikshaMitra.Academic.Timetable.Timetables;

public class TimetableResponseDto
{
    public Dictionary<string, List<TimetableSlotResponseDto>> Days { get; set; } = [];
    public List<TimetableSubjectSummaryDto> SubjectSummary { get; set; } = [];
    public int TotalSlots { get; set; }
    public int OccupiedSlots { get; set; }
    public int UnoccupiedSlots { get; set; }
}

public class EmployeeTimetableResponseDto
{
    public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public Guid? AcademicYearId { get; set; }
    public Dictionary<string, List<EmployeeTimetableSlotResponseDto>> Days { get; set; } = [];
    public int TotalSlots { get; set; }
}

public class EmployeeTimetableSlotResponseDto
{
    public Guid TimetableId { get; set; }
    public Guid TimetableSlotId { get; set; }
    public Guid StandardId { get; set; }
    public string StandardName { get; set; } = string.Empty;
    public Guid DivisionId { get; set; }
    public string DivisionName { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public SlotType Type { get; set; }
    public BreakType? BreakType { get; set; }
    public Curriculum.SubjectType? SubjectType { get; set; }
    public Guid? ElectiveGroupId { get; set; }
    public string? ElectiveGroupName { get; set; }
    public string? ElectiveGroupColor { get; set; }
    public Guid SubjectAssignmentId { get; set; }
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string? SubjectCode { get; set; }
    public string? SubjectColor { get; set; }
    public Guid? RoomId { get; set; }
    public string? RoomName { get; set; }
}

public class TimetableSubjectSummaryDto
{
    public Guid Id { get; set; }
    public Guid SubjectAssignmentId { get; set; }
    public SubjectType SubjectType { get; set; }
    public Guid? ElectiveGroupId { get; set; }
    public string? ElectiveGroupName { get; set; }
    public string? Color { get; set; }
    public int TotalSlots { get; set; }
    public int OccupiedSlots { get; set; }
    public int UnoccupiedSlots { get; set; }
    public List<TimetableSummarySubjectDto> Subjects { get; set; } = [];
}

public class TimetableSummarySubjectDto
{
    public Guid SubjectAssignmentId { get; set; }
    public SubjectType SubjectType { get; set; }
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string? SubjectCode { get; set; }
    public string? Color { get; set; }
}

public class TimetableSlotResponseDto
{
    public int Sequence { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public SlotType Type { get; set; }
    public BreakType? BreakType { get; set; }
    public Curriculum.SubjectType? SubjectType { get; set; }
    public Guid? ElectiveGroupId { get; set; }
    public string? ElectiveGroupName { get; set; }
    public string? ElectiveGroupColor { get; set; }
    public List<TimetableSubjectResponseDto> Subjects { get; set; } = [];
}

public class TimetableSubjectResponseDto
{
    public Guid SubjectAssignmentId { get; set; }
    public Guid SubjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public Guid? TeacherId { get; set; }
    public string? TeacherName { get; set; }
    public Guid? RoomId { get; set; }
    public string? RoomName { get; set; }
    public int TotalSlots { get; set; }
    public int OccupiedSlots { get; set; }
    public int UnoccupiedSlots { get; set; }
}
