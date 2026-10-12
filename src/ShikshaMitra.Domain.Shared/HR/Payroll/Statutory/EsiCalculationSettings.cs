using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;

namespace ShikshaMitra.HR.Payroll.Statutory;

public sealed record EsiCalculationSettings
{
	public string WageBasis { get; init; } = "CLASSIFIED_EARNINGS";
	public Guid? BasicComponentId { get; init; }
	public decimal? EmployeePercentage { get; init; }
	public decimal? EmployerPercentage { get; init; }
	public string? OverrideReason { get; init; }
	public string? EvidenceReference { get; init; }

	public void Validate()
	{
		if (WageBasis is not ("CLASSIFIED_EARNINGS" or "GROSS" or "BASIC_COMPONENT") ||
			(WageBasis == "BASIC_COMPONENT" && (BasicComponentId == null || BasicComponentId == Guid.Empty)) ||
			(WageBasis != "BASIC_COMPONENT" && BasicComponentId.HasValue) ||
			EmployeePercentage is < 0m or > 100m || EmployerPercentage is < 0m or > 100m ||
			OverrideReason?.Length > 500 || EvidenceReference?.Length > 1000)
			throw new BusinessException("HR:Payroll:EsiCalculationSettingsInvalid");
		if ((EmployeePercentage.HasValue || EmployerPercentage.HasValue) &&
			(string.IsNullOrWhiteSpace(OverrideReason) || string.IsNullOrWhiteSpace(EvidenceReference)))
			throw new BusinessException("HR:Payroll:EsiRateOverrideEvidenceRequired");
	}

	[System.Text.Json.Serialization.JsonIgnore]
	public bool BasicComponentIsActive { get; init; } = true;

	public bool HasRateOverride => EmployeePercentage.HasValue || EmployerPercentage.HasValue;

}
