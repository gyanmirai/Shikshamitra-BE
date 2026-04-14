using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Timetable.Settings.LectureSlots;

public class CreateLectureSlotDto
{
    public Guid StandardId { get; set; }
    public string SchoolHours { get; set; } = string.Empty;
    public Guid? PreviousStandardId { get; set; }
    public List<SlotConfigurationDto> SlotConfigurations { get; set; } = [];
}
