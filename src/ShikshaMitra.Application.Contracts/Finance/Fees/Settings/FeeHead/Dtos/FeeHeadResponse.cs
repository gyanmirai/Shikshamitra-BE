using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Finance.Fees.Settings.Dtos;

public class FeeHeadResponse : EntityDto<Guid>
{
    public Guid AcademicYearId { get; set; }
    public Guid FeeCategoryId { get; set; }
    public string FeeCategoryName { get; set; }
    public string Name { get; set; }
    public FeeHeadModule Module { get; set; }
    public bool IsConcessionApplied { get; set; }
    public string Description { get; set; }
    public bool IsActive { get; set; }
    public string DeactivationReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
	public Guid? CreatorId { get; set; }
	public Guid? LastModifierId { get; set; }
	public bool IsUsedInDownstream { get; set; } = false;
    public bool IsEffectivelyActive { get; set; }
    public Guid RevisionGroupId { get; set; }
    public int RevisionNumber { get; set; }
    public Guid? PreviousRevisionId { get; set; }
    public Guid? SuccessorRevisionId { get; set; }
    public bool IsCurrentRevision { get; set; }
    public string RevisionReason { get; set; }
    public string ConcurrencyStamp { get; set; }
    public System.Collections.Generic.List<string> UsageBlockers { get; set; } = new();
}
