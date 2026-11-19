using System;
using System.Collections.Generic;

namespace ShikshaMitra.HR.Payroll.Statutory;

public sealed record PayrollStatutoryProfile
{
	public Guid? VersionId { get; init; }
	public DateTime EffectiveMonth { get; init; }
	[System.Text.Json.Serialization.JsonRequired]
	public bool ApplyPf { get; init; } = true;
	public string? PfNumber { get; init; }
	public string? UanNumber { get; init; }
	public string? PfDisabledReason { get; init; }
	[System.Text.Json.Serialization.JsonRequired]
	public bool ApplyEsic { get; init; } = true;
	public string? EsicInsuranceNumber { get; init; }
	public string? EsicDisabledReason { get; init; }
}

public sealed record PayrollSchemeApplication(
	string Scheme, bool Apply, bool NumberPresent, bool Eligible,
	decimal CalculatedEmployeeAmount, decimal CalculatedEmployerAmount,
	decimal AppliedEmployeeAmount, decimal AppliedEmployerAmount,
	string? WarningCode, string? Reason)
{
	public decimal? ContributionWages { get; init; }
	public static bool HasNumber(string? number) => !string.IsNullOrWhiteSpace(number) && number.Trim() != "0";
	public static PayrollSchemeApplication Decide(string scheme, bool apply, string? number, bool eligible,
		decimal employeeAmount, decimal employerAmount, string? reason = null)
	{
		var present = HasNumber(number);
		var warning = !apply ? scheme + "_APPLY_DISABLED" : eligible && !present ? scheme + "_NUMBER_MISSING" : null;
		// Registration identifiers are follow-up facts, not contribution eligibility.
		var applied = apply && eligible;
		return new(scheme, apply, present, eligible, employeeAmount, employerAmount,
			applied ? employeeAmount : 0m, applied ? employerAmount : 0m, warning, reason);
	}
}

public sealed record PayrollStatutoryApplication(PayrollStatutoryProfile Profile,
	PayrollSchemeApplication ProvidentFund, PayrollSchemeApplication EmployeeStateInsurance)
{
	public IEnumerable<string> Warnings
	{
		get
		{
			if (ProvidentFund.WarningCode != null) yield return ProvidentFund.WarningCode;
			if (EmployeeStateInsurance.WarningCode != null) yield return EmployeeStateInsurance.WarningCode;
		}
	}
}
