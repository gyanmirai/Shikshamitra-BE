using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Timetable.Settings.LectureSlots;

public class GetLectureSlotsByMediumInput : PagedAndSortedResultRequestDto
{
    public Guid AcademicMediumId { get; set; }
    public string? Search { get; set; }
}
