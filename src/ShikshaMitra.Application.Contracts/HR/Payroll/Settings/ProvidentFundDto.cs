using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.Payroll.Settings;

public class ProvidentFundDto : AuditedEntityDto<Guid>
{
	public Guid? TenantId { get; set; }
	public string Code { get; set; }
	public string Name { get; set; }
	public float EmployeePercentage { get; set; }
	public float EmployerPercentage { get; set; }
}