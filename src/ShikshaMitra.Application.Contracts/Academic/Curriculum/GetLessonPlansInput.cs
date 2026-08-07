using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Curriculum;

public class GetLessonPlansInput : PagedAndSortedResultRequestDto
{
    public Guid? AcademicYearId { get; set; }
    public Guid? AcademicMediumId { get; set; }
    public Guid? TeacherId { get; set; }
    public Guid? StandardId { get; set; }
    public Guid? DivisionId { get; set; }
    public Guid? SubjectId { get; set; }
    public Guid? ChapterId { get; set; }
    public LessonPlanStatus? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
