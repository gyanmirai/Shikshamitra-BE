using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.Settings.FeeStructures;

public class FeeStructureDetail : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid FeeStructureId { get; private set; }
    public Guid FeeHeadId { get; private set; }

    public double Amount { get; private set; }
    public bool IsOneTime { get; private set; }
    public bool IsOptional { get; private set; }

	public MonthEnum? PayableMonth { get; private set; }
	public int? PayableYear { get; private set; }

    // ── Payment Term attachment ───────────────────────────────────────────────

    /// <summary>The Payment Term attached to this fee head. Null for legacy structures.</summary>
    public Guid? PaymentTermId { get; private set; }

    /// <summary>The specific Payment Term version snapshot used for demand generation.</summary>
    public Guid? PaymentTermVersionId { get; private set; }

    /// <summary>How this fee head uses the attached Payment Term. Defaults to Inherited.</summary>
    public ScheduleMode ScheduleMode { get; private set; }

    /// <summary>Structure-specific schedule overrides (populated only when ScheduleMode is Customized).</summary>
    public ICollection<FeeStructureDetailScheduleOverride> ScheduleOverrides { get; private set; } = new List<FeeStructureDetailScheduleOverride>();

	public FeeHead FeeHead { get; private set; }

	protected FeeStructureDetail() { }

    public FeeStructureDetail(
        Guid id,
        Guid? tenantId,
        Guid feeStructureId,
        Guid feeHeadId,
        double amount,
        bool isOneTime,
        bool isOptional,
		MonthEnum? payableMonth,
		int? payableYear) : base(id)
    {
        TenantId = tenantId;
        FeeStructureId = feeStructureId;
        FeeHeadId = feeHeadId;
        Amount = amount;
        IsOneTime = isOneTime;
        IsOptional = isOptional;
		PayableMonth = isOneTime ? payableMonth : null;
		PayableYear = isOneTime ? payableYear : null;
        ScheduleMode = ScheduleMode.Inherited;
    }

    // ── Payment Term attachment mutations ─────────────────────────────────────

    /// <summary>Attaches a Payment Term version to this fee head in inherited mode.</summary>
    public void AttachPaymentTerm(Guid paymentTermId, Guid paymentTermVersionId)
    {
        PaymentTermId = paymentTermId;
        PaymentTermVersionId = paymentTermVersionId;
        ScheduleMode = ScheduleMode.Inherited;
        ScheduleOverrides.Clear();
    }

    /// <summary>Attaches a Payment Term version with structure-specific overrides.</summary>
    public void AttachPaymentTermWithOverrides(
        Guid paymentTermId,
        Guid paymentTermVersionId,
        IReadOnlyList<FeeStructureDetailScheduleOverride> overrides)
    {
        PaymentTermId = paymentTermId;
        PaymentTermVersionId = paymentTermVersionId;
        ScheduleMode = ScheduleMode.Customized;
        ScheduleOverrides.Clear();
        foreach (var overrideEntry in overrides)
        {
            ScheduleOverrides.Add(overrideEntry);
        }
    }

    /// <summary>Removes the Payment Term attachment from this fee head.</summary>
    public void DetachPaymentTerm()
    {
        PaymentTermId = null;
        PaymentTermVersionId = null;
        ScheduleMode = ScheduleMode.Inherited;
        ScheduleOverrides.Clear();
    }

    /// <summary>Updates the amount for this fee head.</summary>
    public void UpdateAmount(double amount)
    {
        Amount = amount;
    }
}