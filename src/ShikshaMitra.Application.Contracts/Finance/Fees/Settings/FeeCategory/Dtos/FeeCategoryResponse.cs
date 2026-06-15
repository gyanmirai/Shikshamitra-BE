using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Finance.Fees.Settings.Dtos;

public class FeeCategoryResponse : EntityDto<Guid>
{
    public Guid AcademicYearId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
	public bool IsUsedInDownstream { get; set; } = false;
}
