using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Finance.Fees.Settings.Dtos;

public class FeeCategoryResponse : EntityDto<Guid>
{
    public Guid AcademicYearId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public bool IsActive { get; set; }
    public string DeactivationReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
	public Guid? CreatorId { get; set; }
	public Guid? LastModifierId { get; set; }
	public bool IsUsedInDownstream { get; set; } = false;
    public string ConcurrencyStamp { get; set; }
    public List<string> UsageBlockers { get; set; } = new();
    public List<FeeHeadResponse> FeeHeads { get; set; } = new();
    public int TotalFeeHeads => FeeHeads?.Count ?? 0;
    public int ActiveFeeHeads => FeeHeads?.Count(x => x.IsEffectivelyActive) ?? 0;
}
