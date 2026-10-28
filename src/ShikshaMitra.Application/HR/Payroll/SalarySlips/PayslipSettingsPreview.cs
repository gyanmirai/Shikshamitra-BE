using System;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

// Deliberately synthetic, deterministic and independent of employee/payroll repositories.
internal static class PayslipSettingsPreview
{
	public static PayslipPdfModel Create(string schoolName, string schoolAddress, SalaryDocumentPresentation presentation) => new(
		string.IsNullOrWhiteSpace(schoolName) ? "School name not configured" : schoolName,
		schoolAddress, "Sample Employee (Synthetic)", "SAMPLE-001", "Teaching (sample)", "Sample Teacher",
		"01 Jun 2025", "October 2026", "SAMPLE-2026-10", "Preview", true, "SYNTHETIC SAMPLE",
		new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc),
		30m, 30m, 29m, 0m, 0m, 1m, "Calendar days", "Sample Bank", "XXXX XXXX 0000", "SAMPLE00000",
		"SAMPLE-UAN", "SAMPLE-PF",
		[new("Basic salary", 24000m, 24000m), new("House rent allowance", 9600m, 9600m), new("Special allowance", 6400m, 6400m)],
		[new("Employee PF", 1800m), new("Professional tax", 200m), new("Other deductions", 0m)],
		40000m, 2000m, 38000m, "Thirty Eight Thousand Rupees only",
		"Synthetic sample only. No real employee or salary data. These amounts illustrate document appearance.")
	{
		Presentation = presentation, FullGrossSalary = 40000m, GrossSalary = 40000m,
		CasualLeaveDays = 1m, UnpaidLeaveDays = 0m, InsuranceNumber = "SAMPLE-ESIC"
	};
}
