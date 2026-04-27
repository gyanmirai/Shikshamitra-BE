using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Timetable.Settings.LectureSlots;

public class LectureSlotListDto : EntityDto<Guid>
{
    public Guid StandardId { get; set; }
    public string StandardName { get; set; } = string.Empty;
    public List<SlotConfigurationSummaryDto> SlotConfigurations { get; set; } = [];
}

public class SlotConfigurationSummaryDto
{
    public int FromDay { get; set; }
    public int ToDay { get; set; }
    public int TotalSlots { get; set; }
    public List<TimeSlotDetailDto> TimeSlots { get; set; } = [];
}
