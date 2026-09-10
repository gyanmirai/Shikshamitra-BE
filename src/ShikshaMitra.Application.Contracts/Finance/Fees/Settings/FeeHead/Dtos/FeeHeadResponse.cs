using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Finance.Fees.Settings.Dtos;

public class FeeHeadResponse : EntityDto<Guid>
{
    public Guid AcademicYearId { get; set; }
    public Guid FeeCategoryId { get; set; }
    public string FeeCategoryName { get; set; }
    public Guid IncomeCategoryId { get; set; }
    public string IncomeCategoryName { get; set; }
    public Guid IncomeAccountHeadId { get; set; }
    public string IncomeAccountHeadName { get; set; }
    public string Name { get; set; }
    public FeeHeadModule? Module { get; set; }
    public bool IsConcessionApplied { get; set; }
    public string Description { get; set; }
    public bool IsActive { get; set; }
    public string DeactivationReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
	public bool IsUsedInDownstream { get; set; } = false;
}

/// <summary>
/// Lightweight reusable lookup item for active fee-head selectors. Only fee heads whose
/// parent category is also active are returned by the active lookup endpoint.
/// </summary>
public class ActiveFeeHeadLookupDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public Guid FeeCategoryId { get; set; }
	public string FeeCategoryName { get; set; } = string.Empty;
	public Guid IncomeCategoryId { get; set; }
	public string IncomeCategoryName { get; set; } = string.Empty;
	public Guid IncomeAccountHeadId { get; set; }
	public string IncomeAccountHeadName { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public FeeHeadModule? Module { get; set; }
	public bool IsConcessionApplied { get; set; }
}
