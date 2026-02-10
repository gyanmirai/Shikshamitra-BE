using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Application.Contracts.Finance.Setting.FeeCategory.Dtos;

public class FeeCategoryResponse : EntityDto<Guid>
{
    public Guid AcademicYearId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
