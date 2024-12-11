using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.TenantSettings.EducationBoards;
using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.PayrollStructures;

public class PayrollStructure : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; }

	public Guid EducationMediumStageId { get; init; }

	public Guid PayElementId { get; init; }

	public PayrollStructureType PayrollStructureType { get; init; }

	public double Amount { get; init; }

	// navigation property
	public virtual PayElement? PayElement { get; init; }
	public virtual EducationMediumStage? EducationMediumStage { get; init; }

	// constructor for orm
	private PayrollStructure()
	{
	}

	public PayrollStructure(Guid id, Guid? tenantId, Guid educationMediumStageId, Guid payElementId,
		PayrollStructureType payrollStructureType, double amount) : base(id)
	{
		TenantId = tenantId;
		EducationMediumStageId = educationMediumStageId;
		PayElementId = payElementId;
		PayrollStructureType = payrollStructureType;
		Amount = amount;
	}
}