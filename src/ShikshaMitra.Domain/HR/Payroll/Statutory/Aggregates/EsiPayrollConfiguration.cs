using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.Statutory;

public class EsiPayrollConfiguration : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public DateTime EffectiveMonth { get; private set; }
	public bool Enabled { get; private set; }
	public string? RegistrationNumber { get; private set; }
	public DateTime? CoveredSince { get; private set; }
	public bool IsCancelled { get; private set; }
	public string? CancellationReason { get; private set; }
	public Guid? ReplacesId { get; private set; }
	public string RequestFingerprint { get; private set; } = string.Empty;
	public string CalculationSettingsJson { get; private set; } = "{}";
	public string ComponentTreatmentsJson { get; private set; } = "{}";

	private EsiPayrollConfiguration() { }

	public EsiPayrollConfiguration(Guid id, Guid? tenantId, DateTime effectiveMonth, bool enabled,
		string? registrationNumber, DateTime? coveredSince, string requestFingerprint, Guid? replacesId = null,
		IReadOnlyDictionary<Guid, StatutoryWageTreatment>? treatments = null, EsiCalculationSettings? calculationSettings = null) : base(id)
	{
		if (effectiveMonth == default || effectiveMonth.Day != 1 || effectiveMonth.TimeOfDay != TimeSpan.Zero)
			throw new BusinessException(HRErrorCodes.EsiConfigurationInvalidMonth);

		if (registrationNumber?.Length > 100 || string.IsNullOrWhiteSpace(requestFingerprint))
			throw new BusinessException(HRErrorCodes.StatutoryActivationBlocked);
		TenantId = tenantId;
		EffectiveMonth = effectiveMonth;
		Enabled = enabled;
		RegistrationNumber = registrationNumber?.Trim();
		CoveredSince = coveredSince?.Date;
		RequestFingerprint = requestFingerprint;
		ReplacesId = replacesId;
		calculationSettings ??= new EsiCalculationSettings();
		calculationSettings.Validate();
		CalculationSettingsJson = JsonSerializer.Serialize(calculationSettings);
		ComponentTreatmentsJson = JsonSerializer.Serialize(treatments ?? new Dictionary<Guid, StatutoryWageTreatment>());
	}

	public Dictionary<Guid, StatutoryWageTreatment> GetTreatments() =>
		JsonSerializer.Deserialize<Dictionary<Guid, StatutoryWageTreatment>>(ComponentTreatmentsJson)!;

	public EsiCalculationSettings GetCalculationSettings()
	{
		var stored = JsonNode.Parse(CalculationSettingsJson)?.AsObject() ?? new JsonObject();
		// Read older immutable versions as their original coupled policy without rewriting their JSON.
		if (stored.TryGetPropertyValue("WageBasis", out var legacyBasis) &&
			!stored.ContainsKey(nameof(EsiCalculationSettings.EligibilityBasis)) &&
			!stored.ContainsKey(nameof(EsiCalculationSettings.CalculationBasis)))
		{
			var basis = legacyBasis?.GetValue<string>();
			stored.Remove("WageBasis");
			stored[nameof(EsiCalculationSettings.EligibilityBasis)] = basis;
			stored[nameof(EsiCalculationSettings.CalculationBasis)] = basis;
		}
		return stored.Deserialize<EsiCalculationSettings>() ?? new();
	}

	public void Cancel(string reason)
	{
		if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
			throw new BusinessException(HRErrorCodes.EsiConfigurationReasonRequired);
		IsCancelled = true;
		CancellationReason = reason.Trim();
	}

	// Adapt immutable payroll configuration to the existing statutory calculation contract.
	public StatutoryEstablishment? ToCalculationEstablishment()
	{
		if (!Enabled || IsCancelled) return null;
		var establishment = new StatutoryEstablishment(Id, TenantId, StatutorySchemeCodes.EmployeeStateInsurance, "IN-GJ");
		establishment.Configure("IN-GJ", RegistrationNumber, 0, CoveredSince, EffectiveMonth, null);
		// Transient calculation state does not assert legal registration.
		establishment.EnablePlatformCalculation();
		return establishment;
	}
}