using System;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

public class PayslipSettingsDto
{
	// Read-only Institute Setup details. Incoming values are never used as document identity.
	public string SchoolName { get; set; } = string.Empty;
	public string SchoolAddress { get; set; } = string.Empty;
	public SalaryDocumentPresentation Presentation { get; set; } = new();
	public Guid PresentationVersionId { get; set; }
	public bool AutomaticEmail { get; set; }
	public string? ConcurrencyStamp { get; set; }
	public Guid IdempotencyKey { get; set; }
}
