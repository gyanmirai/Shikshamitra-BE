using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

public class StudentFeeStructure : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }

	public Guid StudentId { get; private set; }

	public Guid FeeStructureStandardId { get; private set; }

	protected StudentFeeStructure() { }

	public StudentFeeStructure(
		Guid id,
		Guid? tenantId,
		Guid studentId,
		Guid feeStructureStandardId) : base(id)
	{
		TenantId = tenantId;
		StudentId = studentId;
		FeeStructureStandardId = feeStructureStandardId;
	}
}
