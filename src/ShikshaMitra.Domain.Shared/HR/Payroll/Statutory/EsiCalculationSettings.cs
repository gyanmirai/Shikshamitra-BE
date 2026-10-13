using System;
using System.Text.Json.Serialization;
using Volo.Abp;

namespace ShikshaMitra.HR.Payroll.Statutory;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EsiCalculationSettings
{
	public string EligibilityBasis { get; init; } = "CLASSIFIED_EARNINGS";
	public string CalculationBasis { get; init; } = "CLASSIFIED_EARNINGS";
	public Guid? BasicComponentId { get; init; }
	public decimal? EmployeePercentage { get; init; }
	public decimal? EmployerPercentage { get; init; }
	public string? OverrideReason { get; init; }
	public string? EvidenceReference { get; init; }

	public void Validate()
	{
		if (!IsValidBasis(EligibilityBasis) || !IsValidBasis(CalculationBasis) ||
			(UsesBasicComponent && (BasicComponentId == null || BasicComponentId == Guid.Empty)) ||
			(!UsesBasicComponent && BasicComponentId.HasValue) ||
			EmployeePercentage is < 0m or > 100m || EmployerPercentage is < 0m or > 100m ||
			OverrideReason?.Length > 500 || EvidenceReference?.Length > 1000)
			throw new BusinessException("HR:Payroll:EsiCalculationSettingsInvalid");
		if ((EmployeePercentage.HasValue || EmployerPercentage.HasValue) &&
			(string.IsNullOrWhiteSpace(OverrideReason) || string.IsNullOrWhiteSpace(EvidenceReference)))
			throw new BusinessException("HR:Payroll:EsiRateOverrideEvidenceRequired");
	}

	[JsonIgnore]
	public bool BasicComponentIsActive { get; init; } = true;

	[JsonIgnore]
	public bool UsesBasicComponent => EligibilityBasis == "BASIC_COMPONENT" || CalculationBasis == "BASIC_COMPONENT";

	public bool HasRateOverride => EmployeePercentage.HasValue || EmployerPercentage.HasValue;

	private static bool IsValidBasis(string basis) => basis is "CLASSIFIED_EARNINGS" or "GROSS" or "BASIC_COMPONENT";
}