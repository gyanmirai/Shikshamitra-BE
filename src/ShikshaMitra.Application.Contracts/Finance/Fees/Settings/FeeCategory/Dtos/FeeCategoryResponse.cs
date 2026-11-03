using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Finance.Fees.Settings.Dtos;

public class FeeCategoryResponse : EntityDto<Guid>
{
    public Guid AcademicYearId { get; set; }
    public string Name { get; set; } = null!; // The response mapper populates these members after construction.
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public string? DeactivationReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
	public bool IsUsedInDownstream { get; set; } = false;
    public List<FeeHeadResponse> FeeHeads { get; set; } = new();
    public int TotalFeeHeads => FeeHeads?.Count ?? 0;
}
