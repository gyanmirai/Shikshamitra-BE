using System;
using System.ComponentModel.DataAnnotations;
using ShikshaMitra.Finance.Fees.Settings;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Domain.Finance.Fees.Settings;

public class FeeHead : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    public Guid AcademicYearId { get; private set; }

    public Guid FeeCategoryId { get; private set; }

    [Required]
    public string Name { get; private set; }

    [Required]
    public string NormalizedName { get; private set; }

    public FeeHeadModule Module { get; private set; }

    public bool IsConcessionApplied { get; private set; }

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public string? DeactivationReason { get; private set; }

    public Guid RevisionGroupId { get; private set; }

    public int RevisionNumber { get; private set; }

    public Guid? PreviousRevisionId { get; private set; }

    public Guid? SuccessorRevisionId { get; private set; }

    public bool IsCurrentRevision { get; private set; }

    public string? RevisionReason { get; private set; }

	public FeeCategory FeeCategory { get; private set; }

    protected FeeHead() { }

    public FeeHead(
        Guid id,
        Guid? tenantId,
        Guid academicYearId,
        Guid feeCategoryId,
        string name,
        FeeHeadModule module,
        bool isConcessionApplied,
        string? description,
        bool isActive = true,
        Guid? revisionGroupId = null,
        int revisionNumber = 1,
        Guid? previousRevisionId = null) : base(id)
    {
        TenantId = tenantId;
        AcademicYearId = academicYearId;
        FeeCategoryId = feeCategoryId;
        SetName(name);
        Module = module;
        IsConcessionApplied = isConcessionApplied;
        Description = description;
        IsActive = isActive;
        RevisionGroupId = revisionGroupId ?? id;
        RevisionNumber = revisionNumber;
        PreviousRevisionId = previousRevisionId;
        IsCurrentRevision = true;
    }

    public void Update(Guid feeCategoryId, string name, FeeHeadModule module, bool isConcessionApplied, string? description)
    {
        FeeCategoryId = feeCategoryId;
        SetName(name);
        Module = module;
        IsConcessionApplied = isConcessionApplied;
        Description = description;
    }

    public void SetActiveStatus(bool isActive, string? deactivationReason)
    {
		if (!isActive && string.IsNullOrWhiteSpace(deactivationReason))
			throw new Volo.Abp.BusinessException(FinanceFeeHeadErrorCodes.DeactivationReasonRequired)
				.WithData("Field", "DeactivationReason");
        IsActive = isActive;
		DeactivationReason = isActive ? null : deactivationReason!.Trim();
    }

    public void Supersede(Guid successorRevisionId)
    {
        SuccessorRevisionId = successorRevisionId;
        IsCurrentRevision = false;
        IsActive = false;
        DeactivationReason = "Superseded by a newer revision.";
    }

    public void SetRevisionReason(string? reason) => RevisionReason = reason;

    private void SetName(string name)
    {
        Name = FeeSetupNameNormalizer.ForStorage(name);
        NormalizedName = FeeSetupNameNormalizer.ForComparison(Name);
    }
}
