using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ShikshaMitra.Domain.Finance.Policies;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

public class StudentFeeStructure : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }

	public Guid StudentId { get; private set; }

	public Guid FeeStructureStandardId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid ConcessionPolicyId { get; private set; }
	public Guid ConcessionPolicyVersionId { get; private set; }
	public ConcessionFeeHeadScope ConcessionFeeHeadScope { get; private set; }
	public string ConcessionApplicableFeeHeadIdsJson { get; private set; } = "[]";
	// Nullable for legacy assignments created before policy version pinning.
	// All new assignments are validated and populated by the application service.
	public Guid? CollectionPolicyId { get; private set; }
	public Guid? CollectionPolicyVersionId { get; private set; }
	public Guid? RefundPolicyId { get; private set; }
	public Guid? RefundPolicyVersionId { get; private set; }
	public Guid? ProrationPolicyId { get; private set; }
	public Guid? ProrationPolicyVersionId { get; private set; }
	public DateTime ChargeStartDate { get; private set; }
	public DateTime ChargeEndDate { get; private set; }
	public ChargePeriodSource ChargePeriodSource { get; private set; } = ChargePeriodSource.AcademicYear;
	public Guid? ChargePeriodSourceReferenceId { get; private set; }
	public string? ChargePeriodOverrideReason { get; private set; }
	/// <summary>Canonical SHA-256 of the assignment command for safe retry detection.</summary>
	public string? AssignmentRequestFingerprint { get; private set; }

	protected StudentFeeStructure() { }

	public StudentFeeStructure(
		Guid id,
		Guid? tenantId,
		Guid studentId,
		Guid feeStructureStandardId,
		Guid academicYearId = default) : base(id)
	{
		TenantId = tenantId;
		StudentId = studentId;
		FeeStructureStandardId = feeStructureStandardId;
		AcademicYearId = academicYearId;
		ConcessionFeeHeadScope = ConcessionFeeHeadScope.EntireFeeStructure;
	}

	public StudentFeeStructure(
		Guid id,
		Guid? tenantId,
		Guid studentId,
		Guid feeStructureStandardId,
		Guid academicYearId,
		Guid concessionPolicyId,
		Guid concessionPolicyVersionId,
		ConcessionFeeHeadScope concessionFeeHeadScope,
		IReadOnlyCollection<Guid> concessionApplicableFeeHeadIds,
		Guid? collectionPolicyId = null,
		Guid? collectionPolicyVersionId = null,
		Guid? refundPolicyId = null,
		Guid? refundPolicyVersionId = null,
		Guid? prorationPolicyId = null,
		Guid? prorationPolicyVersionId = null,
		DateTime? chargeStartDate = null,
		DateTime? chargeEndDate = null,
		ChargePeriodSource chargePeriodSource = ChargePeriodSource.AcademicYear,
		Guid? chargePeriodSourceReferenceId = null,
		string? chargePeriodOverrideReason = null,
		string? assignmentRequestFingerprint = null) : base(id)
	{
		TenantId = tenantId;
		StudentId = studentId;
		FeeStructureStandardId = feeStructureStandardId;
		AcademicYearId = academicYearId;
		ConcessionPolicyId = concessionPolicyId;
		ConcessionPolicyVersionId = concessionPolicyVersionId;
		SetConcessionApplicability(concessionFeeHeadScope, concessionApplicableFeeHeadIds);
		CollectionPolicyId = collectionPolicyId;
		CollectionPolicyVersionId = collectionPolicyVersionId;
		if (refundPolicyId.HasValue != refundPolicyVersionId.HasValue)
			throw new ArgumentException("Refund policy and version must both be present or absent.");
		RefundPolicyId = refundPolicyId;
		RefundPolicyVersionId = refundPolicyVersionId;
		if (prorationPolicyId.HasValue != prorationPolicyVersionId.HasValue)
			throw new ArgumentException("Proration policy and version must both be present or absent.");
		ProrationPolicyId = prorationPolicyId;
		ProrationPolicyVersionId = prorationPolicyVersionId;
		ChargeStartDate = chargeStartDate?.Date ?? DateTime.MinValue;
		ChargeEndDate = chargeEndDate?.Date ?? DateTime.MinValue;
		if (ChargeStartDate != DateTime.MinValue && ChargeEndDate != DateTime.MinValue && ChargeStartDate > ChargeEndDate)
			throw new ArgumentException("Charge start date cannot be after charge end date.");
		ChargePeriodSource = chargePeriodSource;
		ChargePeriodSourceReferenceId = chargePeriodSourceReferenceId;
		ChargePeriodOverrideReason = string.IsNullOrWhiteSpace(chargePeriodOverrideReason)
			? null : chargePeriodOverrideReason.Trim();
		AssignmentRequestFingerprint = string.IsNullOrWhiteSpace(assignmentRequestFingerprint)
			? null : assignmentRequestFingerprint.Trim();
	}

	public void RefreshConcessionPolicySnapshot(
		Guid academicYearId,
		Guid concessionPolicyId,
		Guid concessionPolicyVersionId,
		ConcessionFeeHeadScope concessionFeeHeadScope,
		IReadOnlyCollection<Guid> concessionApplicableFeeHeadIds)
	{
		if (academicYearId == Guid.Empty || concessionPolicyId == Guid.Empty || concessionPolicyVersionId == Guid.Empty)
			throw new ArgumentException("Academic year, concession policy, and policy version are required.");

		AcademicYearId = academicYearId;
		ConcessionPolicyId = concessionPolicyId;
		ConcessionPolicyVersionId = concessionPolicyVersionId;
		SetConcessionApplicability(concessionFeeHeadScope, concessionApplicableFeeHeadIds);
	}

	public IReadOnlyList<Guid> GetConcessionApplicableFeeHeadIds() =>
		JsonSerializer.Deserialize<List<Guid>>(ConcessionApplicableFeeHeadIdsJson) ?? [];

	private void SetConcessionApplicability(
		ConcessionFeeHeadScope scope,
		IReadOnlyCollection<Guid> applicableFeeHeadIds)
	{
		var ids = applicableFeeHeadIds.Distinct().Order().ToList();
		if (scope == ConcessionFeeHeadScope.SelectedFeeHeads && ids.Count == 0)
			throw new ArgumentException("At least one applicable concession fee head is required.");
		ConcessionFeeHeadScope = scope;
		ConcessionApplicableFeeHeadIdsJson = JsonSerializer.Serialize(ids);
	}
}
