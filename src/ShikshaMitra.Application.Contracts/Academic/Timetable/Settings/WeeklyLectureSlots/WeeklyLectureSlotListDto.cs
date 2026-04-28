using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;

public class WeeklyLectureSlotListDto : EntityDto<Guid>
{
    public Guid StandardId { get; set; }
    public string StandardName { get; set; } = string.Empty;
    public int TotalLectures { get; set; }
    public List<WeeklyLectureSubjectSummaryDto> Subjects { get; set; } = [];
}

public class WeeklyLectureSubjectSummaryDto
{
    public Guid? SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public Guid? ElectiveGroupId { get; set; }
    public string? ElectiveGroupName { get; set; }
    public int WeeklyLectures { get; set; }
}
