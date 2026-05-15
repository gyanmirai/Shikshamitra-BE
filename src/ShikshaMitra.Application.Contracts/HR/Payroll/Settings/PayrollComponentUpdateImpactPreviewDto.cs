using System;
using System.Collections.Generic;

namespace ShikshaMitra.HR.Payroll.Settings;

public class PayrollComponentImpactEmployeeDto
{
	public Guid EmployeeId { get; set; }
	public string EmployeeName { get; set; } = string.Empty;
	public decimal CurrentAmount { get; set; }
	public bool UsesMasterValue { get; set; }
}

public class PayrollComponentUpdateImpactPreviewDto
{
	public int LinkedEmployeeCount { get; set; }
	public int SyncedEmployeeCount { get; set; }
	public int ManualOverrideEmployeeCount { get; set; }
	public int UnaffectedEmployeeCount { get; set; }
	public int FinalizedSlipCount { get; set; }
	public int DraftSlipCount { get; set; }
	public string EffectiveMonth { get; set; } = string.Empty;
	public string PreviewHash { get; set; } = string.Empty;
	public IReadOnlyList<PayrollComponentImpactEmployeeDto> SyncedEmployees { get; set; } = Array.Empty<PayrollComponentImpactEmployeeDto>();
	public IReadOnlyList<PayrollComponentImpactEmployeeDto> ManualOverrideEmployees { get; set; } = Array.Empty<PayrollComponentImpactEmployeeDto>();
}
