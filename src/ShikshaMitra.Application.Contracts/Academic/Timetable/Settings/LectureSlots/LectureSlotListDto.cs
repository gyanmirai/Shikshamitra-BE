using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Timetable.Settings.LectureSlots;

public class LectureSlotListDto : EntityDto<Guid>
{
    public Guid StandardId { get; set; }
    public string StandardName { get; set; } = string.Empty;
    public List<SlotConfigurationSummaryDto> SlotConfigurations { get; set; } = [];

    public int TotalSlotsPerDay { get; set; }
    public int TeachingSlotsPerDay { get; set; }
    public int NonTeachingSlotsPerDay { get; set; }
    public int ActiveDaysCount { get; set; }
    public int WeeklyTeachingCapacity { get; set; }
    public int RequiredWeeklyLectures { get; set; }
    public int Difference { get; set; }
    public TimetableSettingStatus Status { get; set; }
}

public class SlotConfigurationSummaryDto
{
    public int FromDay { get; set; }
    public int ToDay { get; set; }
    public int TotalSlots { get; set; }
    public List<TimeSlotDetailDto> TimeSlots { get; set; } = [];
}
