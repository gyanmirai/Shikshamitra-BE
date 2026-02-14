using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.StudentFeeAssignment;

public class StudentFeeHead : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }

	public Guid StudentFeeStructureId { get; private set; }

	public Guid FeeHeadId { get; private set; }

	public string FeeHeadName { get; private set; }

	public string CategoryName { get; private set; }

	public decimal Amount { get; private set; }

	public bool IsOptional { get; private set; }

	public bool IsOneTime { get; private set; }

	public bool IsSelected { get; private set; }

	protected StudentFeeHead() { }

	public StudentFeeHead(
		Guid id,
		Guid? tenantId,
		Guid studentFeeStructureId,
		Guid feeHeadId,
		string feeHeadName,
		string categoryName,
		decimal amount,
		bool isOptional,
		bool isOneTime,
		bool isSelected) : base(id)
	{
		TenantId = tenantId;
		StudentFeeStructureId = studentFeeStructureId;
		FeeHeadId = feeHeadId;
		FeeHeadName = feeHeadName;
		CategoryName = categoryName;
		Amount = amount;
		IsOptional = isOptional;
		IsOneTime = isOneTime;
		IsSelected = isSelected;
	}
}
