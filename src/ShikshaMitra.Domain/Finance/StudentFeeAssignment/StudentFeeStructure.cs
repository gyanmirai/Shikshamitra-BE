using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.StudentFeeAssignment;

public class StudentFeeStructure : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }

	public Guid StudentId { get; private set; }

	public Guid StandardId { get; private set; }

	public Guid FeeStructureStandardId { get; private set; }

	public string StructureName { get; private set; }

	public DateTime AssignedAt { get; private set; }

	public ICollection<StudentFeeHead> StudentFeeHeads { get; private set; }

	protected StudentFeeStructure()
	{
		StudentFeeHeads = new List<StudentFeeHead>();
	}

	public StudentFeeStructure(
		Guid id,
		Guid? tenantId,
		Guid studentId,
		Guid standardId,
		Guid feeStructureStandardId,
		string structureName) : base(id)
	{
		TenantId = tenantId;
		StudentId = Check.NotNull(studentId, nameof(studentId));
		StandardId = Check.NotNull(standardId, nameof(standardId));
		FeeStructureStandardId = Check.NotNull(feeStructureStandardId, nameof(feeStructureStandardId));
		StructureName = Check.NotNullOrWhiteSpace(structureName, nameof(structureName));
		AssignedAt = DateTime.UtcNow;
		StudentFeeHeads = new List<StudentFeeHead>();
	}

	public void AddFeeHead(StudentFeeHead feeHead)
	{
		Check.NotNull(feeHead, nameof(feeHead));
		StudentFeeHeads.Add(feeHead);
	}
}
