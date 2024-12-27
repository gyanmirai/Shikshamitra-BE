using ShikshaMitra.Common;
using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.Payroll.Settings;

public class ProfessionalTaxDto : AuditedEntityDto<Guid>
{
	public Guid? TenantId { get; set; }
	public GenderType GenderType { get;  set; }
	public double FromSalary { get;  set; }
	public double ToSalary { get;  set; }
	public double TaxAmount { get;  set; }
}