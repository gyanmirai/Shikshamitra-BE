using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;

public class WeeklyLectureSlotDto : EntityDto<Guid>
{
    public Guid StandardId { get; set; }
    public string StandardName { get; set; } = string.Empty;
    public int TotalLectures { get; set; }
    public List<WeeklyLectureSubjectDetailDto> Subjects { get; set; } = [];

    public int AvailableTeachingCapacity { get; set; }
    public int Difference { get; set; }
    public TimetableSettingStatus Status { get; set; }
}

public class WeeklyLectureSubjectDetailDto
{
    public Guid? SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string? SubjectCode { get; set; }
    public string? SubjectColor { get; set; }
    public Guid? ElectiveGroupId { get; set; }
    public string? ElectiveGroupName { get; set; }
    public string? ElectiveGroupColor { get; set; }
    public List<ElectiveGroupSubjectDto> ElectiveGroupSubjects { get; set; } = [];
    public int WeeklyLectures { get; set; }
}

public class ElectiveGroupSubjectDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
}
