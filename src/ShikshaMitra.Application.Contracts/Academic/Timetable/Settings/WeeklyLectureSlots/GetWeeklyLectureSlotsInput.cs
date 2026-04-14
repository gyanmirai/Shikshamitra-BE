using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;

public class GetWeeklyLectureSlotsInput : PagedAndSortedResultRequestDto
{
    public Guid AcademicMediumId { get; set; }
    public string? Search { get; set; }
}
