using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

public class PayslipSettingsDto
{
	[Required, StringLength(250)] public string SchoolName { get; set; } = string.Empty;
	[Required, StringLength(1000)] public string SchoolAddress { get; set; } = string.Empty;
	public bool AutomaticEmail { get; set; }
	public string? ConcurrencyStamp { get; set; }
	public Guid IdempotencyKey { get; set; }
}
