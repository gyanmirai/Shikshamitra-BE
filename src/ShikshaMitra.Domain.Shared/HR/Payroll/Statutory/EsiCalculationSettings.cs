using System;
using System.Text.Json.Serialization;
using Volo.Abp;

namespace ShikshaMitra.HR.Payroll.Statutory;

[JsonConverter(typeof(EsiCalculationSettingsJsonConverter))]
public sealed record EsiCalculationSettings
{
	public string EligibilityBasis { get; init; } = "CLASSIFIED_EARNINGS";
	public string CalculationBasis { get; init; } = "CLASSIFIED_EARNINGS";
	public Guid? EligibilityComponentId { get; init; }
	public Guid? CalculationComponentId { get; init; }
	public decimal? EmployeePercentage { get; init; }
	public decimal? EmployerPercentage { get; init; }
	public string? OverrideReason { get; init; }
	public string? EvidenceReference { get; init; }

	public void Validate()
	{
		if (!IsValidBasis(EligibilityBasis) || !IsValidBasis(CalculationBasis) ||
			(EligibilityBasis == "COMPONENT" && (EligibilityComponentId == null || EligibilityComponentId == Guid.Empty)) ||
			(CalculationBasis == "COMPONENT" && (CalculationComponentId == null || CalculationComponentId == Guid.Empty)) ||
			(EligibilityBasis != "COMPONENT" && EligibilityComponentId.HasValue) ||
			(CalculationBasis != "COMPONENT" && CalculationComponentId.HasValue) ||
			EmployeePercentage is < 0m or > 100m || EmployerPercentage is < 0m or > 100m ||
			OverrideReason?.Length > 500 || EvidenceReference?.Length > 1000)
			throw new BusinessException("HR:Payroll:EsiCalculationSettingsInvalid");
		if ((EmployeePercentage.HasValue || EmployerPercentage.HasValue) &&
			(string.IsNullOrWhiteSpace(OverrideReason) || string.IsNullOrWhiteSpace(EvidenceReference)))
			throw new BusinessException("HR:Payroll:EsiRateOverrideEvidenceRequired");
	}

	[JsonIgnore]
	public bool EligibilityComponentIsActive { get; init; } = true;
	[JsonIgnore]
	public bool CalculationComponentIsActive { get; init; } = true;
	[JsonIgnore]
	public bool IsLegacyRead { get; init; }

	[JsonIgnore]
	public bool UsesComponent => EligibilityBasis == "COMPONENT" || CalculationBasis == "COMPONENT";

	public bool HasRateOverride => EmployeePercentage.HasValue || EmployerPercentage.HasValue;

	private static bool IsValidBasis(string basis) => basis is "CLASSIFIED_EARNINGS" or "GROSS" or "COMPONENT";
}