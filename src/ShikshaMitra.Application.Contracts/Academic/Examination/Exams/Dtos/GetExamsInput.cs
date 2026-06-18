using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Examination.Exams.Dtos;

public class GetExamsInput : PagedAndSortedResultRequestDto
{
    public Guid AcademicMediumId { get; set; }
    public ExamStatus Status { get; set; }
    public Guid? SubjectId { get; set; }
    public Guid? StandardId { get; set; }
    public Guid? DivisionId { get; set; }
    public bool? GradeEntryPublishedOnly { get; set; }
}
