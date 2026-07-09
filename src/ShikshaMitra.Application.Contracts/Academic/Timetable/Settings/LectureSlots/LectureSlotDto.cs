using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Timetable.Settings.LectureSlots;

public class LectureSlotDto : EntityDto<Guid>
{
    public Guid StandardId { get; set; }
    public string StandardName { get; set; } = string.Empty;
    public string SchoolHours { get; set; } = string.Empty;
    public Guid? PreviousStandardId { get; set; }
    public List<SlotConfigurationDetailDto> SlotConfigurations { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public int TotalSlotsPerDay { get; set; }
    public int TeachingSlotsPerDay { get; set; }
    public int NonTeachingSlotsPerDay { get; set; }
    public int ActiveDaysCount { get; set; }
    public int WeeklyTeachingCapacity { get; set; }
    public int RequiredWeeklyLectures { get; set; }
    public int Difference { get; set; }
    public TimetableSettingStatus Status { get; set; }
}

public class SlotConfigurationDetailDto
{
    public int TotalSlots { get; set; }
    public List<int> WeekRange { get; set; } = [];
    public List<TimeSlotDetailDto> TimeSlots { get; set; } = [];
}

public class TimeSlotDetailDto
{
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public SlotType SlotType { get; set; }
    public Guid? RoomId { get; set; }
    public string? RoomName { get; set; }
    public BreakType? BreakType { get; set; }
}
