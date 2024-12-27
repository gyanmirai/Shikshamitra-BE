using ShikshaMitra.Common;
using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.Settings;

public class ProfessionalTax : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get;  private set; }
	public GenderType GenderType { get; private set; }
	public double FromSalary { get; private set; }
	public double ToSalary { get; private set; }
	public double TaxAmount { get; private set; }


	
	protected ProfessionalTax() { } // For ORM
	
	internal ProfessionalTax(Guid id, Guid? tenantId,GenderType genderType, double fromSalary, double toSalary, double taxAmount) : base(id)
	{
		TenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
		GenderType = genderType;
		FromSalary = fromSalary;
		ToSalary = toSalary;
		TaxAmount = taxAmount;
		

	}
}