using System;
using System.Text.RegularExpressions;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.Statutory;

public class EmployeeStatutoryProfileVersion : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid EmployeeId { get; private set; }
	public DateTime EffectiveMonth { get; private set; }
	public bool ApplyPf { get; private set; }
	public string? PfNumber { get; private set; }
	public string? UanNumber { get; private set; }
	public string? PfDisabledReason { get; private set; }
	public bool ApplyEsic { get; private set; }
	public string? EsicInsuranceNumber { get; private set; }
	public string? EsicDisabledReason { get; private set; }
	public Guid RequestId { get; private set; }
	public string RequestFingerprint { get; private set; } = string.Empty;
	public bool IsSuperseded { get; private set; }
	protected EmployeeStatutoryProfileVersion() { }
	public EmployeeStatutoryProfileVersion(Guid id, Guid? tenantId, Guid employeeId, DateTime effectiveMonth,
		PayrollStatutoryProfile profile, Guid requestId, string fingerprint) : base(id)
	{
		if (employeeId == Guid.Empty || requestId == Guid.Empty || string.IsNullOrWhiteSpace(fingerprint))
			throw new BusinessException("HR:Payroll:StatutoryProfileInvalid");
		Validate(profile);
		TenantId = tenantId;
		EmployeeId = employeeId;
		EffectiveMonth = new DateTime(effectiveMonth.Year, effectiveMonth.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		ApplyPf = profile.ApplyPf;
		PfNumber = NormalizeNumber(profile.PfNumber);
		UanNumber = NormalizeNumber(profile.UanNumber);
		PfDisabledReason = profile.ApplyPf ? null : profile.PfDisabledReason?.Trim();
		ApplyEsic = profile.ApplyEsic;
		EsicInsuranceNumber = NormalizeNumber(profile.EsicInsuranceNumber);
		EsicDisabledReason = profile.ApplyEsic ? null : profile.EsicDisabledReason?.Trim();
		RequestId = requestId;
		RequestFingerprint = fingerprint;
	}
	public PayrollStatutoryProfile ToProfile() => new()
	{
		VersionId = Id, EffectiveMonth = EffectiveMonth, ApplyPf = ApplyPf, PfNumber = PfNumber,
		UanNumber = UanNumber, PfDisabledReason = PfDisabledReason, ApplyEsic = ApplyEsic,
		EsicInsuranceNumber = EsicInsuranceNumber, EsicDisabledReason = EsicDisabledReason
	};
	public void Supersede() => IsSuperseded = true;
	public static string? NormalizeNumber(string? number) => PayrollSchemeApplication.HasNumber(number) ? number!.Trim() : null;
	public static void Validate(PayrollStatutoryProfile profile)
	{
		if ((!profile.ApplyPf && string.IsNullOrWhiteSpace(profile.PfDisabledReason)) ||
			(!profile.ApplyEsic && string.IsNullOrWhiteSpace(profile.EsicDisabledReason)) ||
			profile.PfDisabledReason?.Length > 500 || profile.EsicDisabledReason?.Length > 500 ||
			NormalizeNumber(profile.PfNumber)?.Length > 50 || NormalizeNumber(profile.EsicInsuranceNumber)?.Length > 100 ||
			(NormalizeNumber(profile.PfNumber) is { } pf && !Regex.IsMatch(pf, @"^[a-zA-Z0-9/\- ]+$")) ||
			(NormalizeNumber(profile.UanNumber) is { } uan && !Regex.IsMatch(uan, @"^\d{12}$")) ||
			(NormalizeNumber(profile.EsicInsuranceNumber) is { } esi && !Regex.IsMatch(esi, @"^[a-zA-Z0-9/\-]+$")))
			throw new BusinessException("HR:Payroll:StatutoryProfileInvalid");
	}
}
