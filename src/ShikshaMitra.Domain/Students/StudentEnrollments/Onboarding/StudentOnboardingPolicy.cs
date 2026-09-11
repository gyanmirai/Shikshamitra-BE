using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public class StudentOnboardingPolicy : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid? OfferingId { get; private set; }
	public Guid? StandardId { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public bool RequireDocuments { get; private set; }
	public bool RequireFamilyDetails { get; private set; }
	public bool RequireCommunicationDetails { get; private set; }
	public bool RequireMedicalDetails { get; private set; }
	public bool RequireFeeClearance { get; private set; }
	public bool RequireStudentAccount { get; private set; }
	public bool RequireGuardianAccount { get; private set; }
	public bool RequireServiceConsent { get; private set; }
	public int SeatReservationMinutes { get; private set; }
	public int SlaHours { get; private set; }
	public bool IsActive { get; private set; }

	protected StudentOnboardingPolicy() { }

	public StudentOnboardingPolicy(Guid id, Guid? tenantId, Guid academicYearId, Guid? offeringId,
		Guid? standardId, string name, bool requireDocuments, bool requireFamilyDetails,
		bool requireCommunicationDetails, bool requireMedicalDetails, bool requireFeeClearance,
		bool requireStudentAccount, bool requireGuardianAccount, bool requireServiceConsent,
		int seatReservationMinutes, int slaHours) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = Check.NotDefaultOrNull<Guid>(academicYearId, nameof(academicYearId));
		OfferingId = offeringId;
		StandardId = standardId;
		Update(name, requireDocuments, requireFamilyDetails, requireCommunicationDetails,
			requireMedicalDetails, requireFeeClearance, requireStudentAccount, requireGuardianAccount,
			requireServiceConsent, seatReservationMinutes, slaHours, true);
	}

	public void Update(string name, bool requireDocuments, bool requireFamilyDetails,
		bool requireCommunicationDetails, bool requireMedicalDetails, bool requireFeeClearance,
		bool requireStudentAccount, bool requireGuardianAccount, bool requireServiceConsent,
		int seatReservationMinutes, int slaHours, bool isActive)
	{
		if (seatReservationMinutes is < 5 or > 10080) throw new ArgumentOutOfRangeException(nameof(seatReservationMinutes));
		if (slaHours is < 1 or > 8760) throw new ArgumentOutOfRangeException(nameof(slaHours));
		Name = Check.NotNullOrWhiteSpace(name, nameof(name), 120).Trim();
		RequireDocuments = requireDocuments;
		RequireFamilyDetails = requireFamilyDetails;
		RequireCommunicationDetails = requireCommunicationDetails;
		RequireMedicalDetails = requireMedicalDetails;
		RequireFeeClearance = requireFeeClearance;
		RequireStudentAccount = requireStudentAccount;
		RequireGuardianAccount = requireGuardianAccount;
		RequireServiceConsent = requireServiceConsent;
		SeatReservationMinutes = seatReservationMinutes;
		SlaHours = slaHours;
		IsActive = isActive;
	}
}
