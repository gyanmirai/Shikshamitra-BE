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

	protected StudentFeeStructure() { }

	public StudentFeeStructure(Guid id, Guid? tenantId, Guid studentId, Guid feeStructureStandardId) : base(id)
	{
		TenantId = tenantId;
		StudentId = studentId;
		FeeStructureStandardId = feeStructureStandardId;
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
		IReadOnlyCollection<Guid> concessionApplicableFeeHeadIds) : base(id)
	{
		TenantId = tenantId;
		StudentId = studentId;
		FeeStructureStandardId = feeStructureStandardId;
		AcademicYearId = academicYearId;
		ConcessionPolicyId = concessionPolicyId;
		ConcessionPolicyVersionId = concessionPolicyVersionId;
		SetConcessionApplicability(concessionFeeHeadScope, concessionApplicableFeeHeadIds);
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
