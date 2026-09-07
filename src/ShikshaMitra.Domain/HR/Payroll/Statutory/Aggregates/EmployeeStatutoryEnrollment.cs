using System;
using ShikshaMitra.HR;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.Statutory;

public class EmployeeStatutoryEnrollment : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid EmployeeId { get; private set; }
	public string SchemeCode { get; private set; } = string.Empty;
	public EmployeeStatutoryEnrollmentStatus Status { get; private set; }
	public StatutoryWageCeilingCategory WageCeilingCategory { get; private set; }
	public string? InsuranceNumber { get; private set; }
	public DateTime? CoveredFrom { get; private set; }
	public DateTime? CoveredTo { get; private set; }
	public DateTime? ContributionPeriodEnd { get; private set; }
	public string? StatusReason { get; private set; }
	public bool EmployerShareReliefEligible { get; private set; }
	public DateTime? EmployerShareReliefFrom { get; private set; }
	public DateTime? EmployerShareReliefTo { get; private set; }
	public string? DisabilityEvidenceReference { get; private set; }

	protected EmployeeStatutoryEnrollment() { }

	public EmployeeStatutoryEnrollment(Guid id, Guid? tenantId, Guid employeeId, string schemeCode) : base(id)
	{
		TenantId = tenantId;
		EmployeeId = employeeId;
		SchemeCode = schemeCode.Trim().ToUpperInvariant();
		Status = EmployeeStatutoryEnrollmentStatus.PendingReview;
		WageCeilingCategory = StatutoryWageCeilingCategory.Standard;
	}

	public void Update(
		EmployeeStatutoryEnrollmentStatus status,
		StatutoryWageCeilingCategory wageCeilingCategory,
		string? insuranceNumber,
		DateTime? coveredFrom,
		DateTime? coveredTo,
		DateTime? contributionPeriodEnd,
		string? statusReason,
		bool employerShareReliefEligible = false,
		DateTime? employerShareReliefFrom = null,
		DateTime? employerShareReliefTo = null,
		string? disabilityEvidenceReference = null)
	{
		if (employerShareReliefEligible && wageCeilingCategory != StatutoryWageCeilingCategory.PersonWithDisability)
		{
			throw new BusinessException(HRErrorCodes.StatutoryActivationBlocked)
				.WithData("reason", "PWD_RELIEF_REQUIRES_PWD_WAGE_CEILING");
		}
		if (employerShareReliefEligible && !employerShareReliefFrom.HasValue)
		{
			throw new BusinessException(HRErrorCodes.StatutoryActivationBlocked)
				.WithData("reason", "PWD_RELIEF_START_REQUIRED");
		}
		if (employerShareReliefEligible && string.IsNullOrWhiteSpace(disabilityEvidenceReference))
		{
			throw new BusinessException(HRErrorCodes.StatutoryActivationBlocked)
				.WithData("reason", "PWD_RELIEF_EVIDENCE_REQUIRED");
		}
		if (employerShareReliefFrom.HasValue && employerShareReliefTo.HasValue &&
			employerShareReliefTo.Value.Date < employerShareReliefFrom.Value.Date)
		{
			throw new BusinessException(HRErrorCodes.StatutoryActivationBlocked)
				.WithData("reason", "PWD_RELIEF_DATE_RANGE_INVALID");
		}

		Status = status;
		WageCeilingCategory = wageCeilingCategory;
		InsuranceNumber = string.IsNullOrWhiteSpace(insuranceNumber) ? null : insuranceNumber.Trim();
		CoveredFrom = coveredFrom?.Date;
		CoveredTo = coveredTo?.Date;
		ContributionPeriodEnd = contributionPeriodEnd?.Date;
		StatusReason = string.IsNullOrWhiteSpace(statusReason) ? null : statusReason.Trim();
		EmployerShareReliefEligible = employerShareReliefEligible;
		EmployerShareReliefFrom = employerShareReliefFrom?.Date;
		EmployerShareReliefTo = employerShareReliefTo?.Date;
		DisabilityEvidenceReference = string.IsNullOrWhiteSpace(disabilityEvidenceReference)
			? null
			: disabilityEvidenceReference.Trim();
	}
}
