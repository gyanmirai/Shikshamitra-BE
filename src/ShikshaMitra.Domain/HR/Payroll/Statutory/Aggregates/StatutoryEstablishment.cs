using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.Statutory;

public class StatutoryEstablishment : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string SchemeCode { get; private set; } = string.Empty;
	public string JurisdictionCode { get; private set; } = string.Empty;
	public string? RegistrationNumber { get; private set; }
	public int EmployeeCount { get; private set; }
	public DateTime? CoveredSince { get; private set; }
	public DateTime? BenefitsActiveFrom { get; private set; }
	public StatutoryEstablishmentStatus Status { get; private set; }
	public string? EvidenceReference { get; private set; }
	public Guid? ConfirmedByUserId { get; private set; }
	public DateTime? ConfirmedAt { get; private set; }

	protected StatutoryEstablishment() { }

	public StatutoryEstablishment(Guid id, Guid? tenantId, string schemeCode, string jurisdictionCode) : base(id)
	{
		TenantId = tenantId;
		SchemeCode = schemeCode.Trim().ToUpperInvariant();
		JurisdictionCode = jurisdictionCode.Trim().ToUpperInvariant();
		Status = StatutoryEstablishmentStatus.Draft;
	}

	public void Configure(
		string jurisdictionCode,
		string? registrationNumber,
		int employeeCount,
		DateTime? coveredSince,
		DateTime? benefitsActiveFrom,
		string? evidenceReference)
	{
		JurisdictionCode = jurisdictionCode.Trim().ToUpperInvariant();
		RegistrationNumber = string.IsNullOrWhiteSpace(registrationNumber) ? null : registrationNumber.Trim();
		EmployeeCount = employeeCount;
		CoveredSince = coveredSince?.Date;
		BenefitsActiveFrom = benefitsActiveFrom?.Date;
		EvidenceReference = string.IsNullOrWhiteSpace(evidenceReference) ? null : evidenceReference.Trim();
		Status = CanActivate() ? StatutoryEstablishmentStatus.Ready : StatutoryEstablishmentStatus.Draft;
		ConfirmedByUserId = null;
		ConfirmedAt = null;
	}

	public bool CanActivate() =>
		!string.IsNullOrWhiteSpace(RegistrationNumber) &&
		BenefitsActiveFrom.HasValue &&
		CoveredSince.HasValue;

	public void Activate(Guid? confirmedByUserId, DateTime confirmedAt)
	{
		if (!CanActivate())
		{
			throw new InvalidOperationException("The statutory establishment is not ready for activation.");
		}

		Status = StatutoryEstablishmentStatus.Active;
		ConfirmedByUserId = confirmedByUserId;
		ConfirmedAt = confirmedAt;
	}

	public void Suspend()
	{
		Status = StatutoryEstablishmentStatus.Suspended;
	}
}
