using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

public class StudentFeeStructure : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }

	public Guid StudentId { get; private set; }

	public Guid StandardId { get; private set; }

	public Guid FeeStructureStandardId { get; private set; }

	public DateTime AssignedOn { get; private set; }

	protected StudentFeeStructure()
	{
	}

	public StudentFeeStructure(
		Guid id,
		Guid? tenantId,
		Guid studentId,
		Guid standardId,
		Guid feeStructureStandardId) : base(id)
	{
		TenantId = tenantId;
		StudentId = Check.NotNull(studentId, nameof(studentId));
		StandardId = Check.NotNull(standardId, nameof(standardId));
		FeeStructureStandardId = Check.NotNull(feeStructureStandardId, nameof(feeStructureStandardId));
		AssignedOn = DateTime.UtcNow;
	}

	public void UpdateFeeStructure(Guid feeStructureStandardId)
	{
		FeeStructureStandardId = Check.NotNull(feeStructureStandardId, nameof(feeStructureStandardId));
	}
}
