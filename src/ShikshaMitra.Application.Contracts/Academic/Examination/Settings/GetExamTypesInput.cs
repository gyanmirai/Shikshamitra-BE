using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Examination.Settings;

public class GetExamTypesInput : PagedAndSortedResultRequestDto
{
    public Guid AcademicMediumId { get; set; }

    public bool IncludeArchived { get; set; }
}
