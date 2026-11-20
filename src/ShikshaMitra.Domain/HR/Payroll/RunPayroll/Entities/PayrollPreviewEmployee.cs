using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollPreviewEmployee : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid RunId { get; private set; }
	public Guid EmployeeId { get; private set; }
	public string EmployeeName { get; private set; } = "";
	public string EmployeeCode { get; private set; } = "";
	public string ItemJson { get; private set; } = "";
	public decimal? PayableDays { get; private set; }
	public decimal? NetPay { get; private set; }
	public decimal? NetPayDelta { get; private set; }
	public string[] RegisterIssueCodes { get; private set; } = [];
	private PayrollPreviewEmployee() { }
	public PayrollPreviewEmployee(Guid id, Guid? tenantId, Guid runId, Guid employeeId, string name, string code, string json,
		decimal? payableDays = null, decimal? netPay = null, decimal? netPayDelta = null, string[]? issueCodes = null) : base(id)
	{
		TenantId = tenantId; RunId = runId; EmployeeId = employeeId; EmployeeName = name; EmployeeCode = code; ItemJson = json;
		PayableDays = payableDays;
		NetPay = netPay;
		NetPayDelta = netPayDelta;
		RegisterIssueCodes = issueCodes ?? [];
	}
}
