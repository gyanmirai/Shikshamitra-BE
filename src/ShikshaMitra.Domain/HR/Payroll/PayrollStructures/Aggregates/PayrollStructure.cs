using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.TenantSettings.EducationBoards;
using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.PayrollStructures;

public class PayrollStructure : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }

	public Guid? EmployeeId { get; set; }

	//public Guid EducationMediumStageId { get; init; }

	public Guid PayElementId { get; init; }

	public PayElementType PayElementType { get; init; }

	public double Amount { get; init; }

	// navigation property
	public virtual PayElement? PayElement { get; init; }
	public virtual EducationMediumStage? EducationMediumStage { get; init; }

	// constructor for orm
	private PayrollStructure()
	{
	}

	public PayrollStructure(Guid id,
		Guid? tenantId,
		//Guid educationMediumStageId,
		Guid payElementId,
		PayElementType payElementType,
		double amount) : base(id)
	{
		TenantId = tenantId;
		//EducationMediumStageId = educationMediumStageId;
		PayElementId = payElementId;
		PayElementType = payElementType;
		Amount = amount;
	}
}