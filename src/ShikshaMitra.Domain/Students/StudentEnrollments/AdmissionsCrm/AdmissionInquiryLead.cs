using System;
using System.Linq;
using System.Net.Mail;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionInquiryLead : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public int ApplicantNumber { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid? AdmissionYearSetupId { get; private set; }
	public int? AdmissionYearSetupVersion { get; private set; }
	public string? AdmissionYearSetupSnapshotJson { get; private set; }
	public Guid? StudentId { get; private set; }
	public Guid? InquirySourceId { get; private set; }
	public string? InquirySourceSnapshot { get; private set; }
	public Guid? InquiryTypeId { get; private set; }
	public string? InquiryTypeSnapshot { get; private set; }
	public AdmissionInquiryChannel Channel { get; private set; }
	public string? CampaignName { get; private set; }
	public string? CampusBranchName { get; private set; }
	public string? PreferredCommunicationLanguage { get; private set; }
	public string? FirstName { get; private set; }
	public string? MiddleName { get; private set; }
	public string? LastName { get; private set; }
	public DateTimeOffset? DateOfBirth { get; private set; }
	public string? ParentGuardianName { get; private set; }
	public string? PrimaryContactNo { get; private set; }
	public string? NormalizedPrimaryContactNo { get; private set; }
	public string? Email { get; private set; }
	public string? NormalizedEmail { get; private set; }
	public string AdmissionIntent { get; private set; } = string.Empty;
	public AdmissionCrmRouteType? IntendedRouteType { get; private set; }
	public Guid? IntendedRouteDefinitionId { get; private set; }
	public int? IntendedRouteDefinitionVersion { get; private set; }
	public string? IntendedRouteNameSnapshot { get; private set; }
	public Guid? TargetStandardId { get; private set; }
	public string? DesiredClassName { get; private set; }
	public AdmissionInquiryStage Stage { get; private set; }
	public Guid? OwnerUserId { get; private set; }
	public int LeadScore { get; private set; }
	public AdmissionCrmLeadPriority Priority { get; private set; }
	public string? RegistrationFormNumber { get; private set; }
	public double? RegistrationFormAmount { get; private set; }
	public DateTime? RegistrationFormIssuedAt { get; private set; }
	public DateTimeOffset LastActivityTime { get; private set; }
	public string? Notes { get; private set; }
	public DateTime? ArchivedAt { get; private set; }
	public string? ArchiveReason { get; private set; }
	public DateTime? ReopenedAt { get; private set; }
	public string? ReopenReason { get; private set; }
	public Guid? MergedIntoLeadId { get; private set; }
	public DateTime? MergedAt { get; private set; }
	public string? MergeReason { get; private set; }
	public string? DuplicateContinueReason { get; private set; }

	public AdmissionCrmInquirySource? InquirySource { get; private set; }
	public AdmissionCrmInquiryType? InquiryType { get; private set; }

	protected AdmissionInquiryLead()
	{
	}

	public AdmissionInquiryLead(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		Guid? inquirySourceId,
		string? inquirySourceSnapshot,
		AdmissionInquiryChannel channel,
		string? campaignName,
		string? campusBranchName,
		string? preferredCommunicationLanguage,
		string? firstName,
		string? middleName,
		string? lastName,
		DateTimeOffset? dateOfBirth,
		string? parentGuardianName,
		string? primaryContactNo,
		string? email,
		string admissionIntent,
		Guid? targetStandardId,
		string? desiredClassName,
		string? notes,
		DateTimeOffset createdAt,
		AdmissionCrmRouteType? intendedRouteType = null,
		Guid? intendedRouteDefinitionId = null,
		int? intendedRouteDefinitionVersion = null,
		string? intendedRouteNameSnapshot = null)
		: base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		SetInquirySource(inquirySourceId, inquirySourceSnapshot);
		SetChannel(channel, campaignName);
		SetRoutingContext(campusBranchName, preferredCommunicationLanguage);
		SetApplicantName(firstName, middleName, lastName, dateOfBirth, parentGuardianName);
		SetContact(primaryContactNo, email);
		SetIntent(admissionIntent, targetStandardId, desiredClassName);
		SetIntendedRoute(
			intendedRouteType,
			intendedRouteDefinitionId,
			intendedRouteDefinitionVersion,
			intendedRouteNameSnapshot);
		SetNotes(notes);
		Stage = AdmissionInquiryStage.NewInquiry;
		Priority = AdmissionCrmLeadPriority.Warm;
		LastActivityTime = createdAt;
	}

	public void LinkStudent(Guid studentId)
	{
		StudentId = studentId;
		Touch();
	}

	public void ApplyAdmissionYearSetup(
		Guid? setupId,
		int version,
		string snapshotJson)
	{
		if (version < 0)
		{
			throw new BusinessException("AdmissionCrm:InvalidAdmissionYearSetupVersion")
				.WithData("Version", version);
		}

		AdmissionYearSetupId = setupId;
		AdmissionYearSetupVersion = version;
		AdmissionYearSetupSnapshotJson = Check.NotNullOrWhiteSpace(
			snapshotJson,
			nameof(snapshotJson),
			AdmissionCrmConsts.MaxAdmissionYearSetupSnapshotJsonLength);
	}

	public void ApplyInquiryType(Guid? inquiryTypeId, string? inquiryTypeSnapshot)
	{
		InquiryTypeId = inquiryTypeId;
		InquiryTypeSnapshot = NormalizeOptional(
			inquiryTypeSnapshot,
			nameof(inquiryTypeSnapshot),
			AdmissionCrmConsts.MaxSourceSnapshotLength);
	}

	public void ChangeStage(AdmissionInquiryStage stage)
	{
		if (Stage == AdmissionInquiryStage.Archived && stage != AdmissionInquiryStage.Archived)
		{
			throw new BusinessException("AdmissionCrm:ArchivedLeadRequiresReopen")
				.WithData("LeadId", Id)
				.WithData("RequestedStage", stage);
		}

		if (Stage == AdmissionInquiryStage.Stale && stage != AdmissionInquiryStage.Stale)
		{
			throw new BusinessException("AdmissionCrm:StaleLeadRequiresReopen")
				.WithData("LeadId", Id)
				.WithData("RequestedStage", stage);
		}

		Stage = stage;
		Touch();
	}

	public void AssignOwner(Guid? ownerUserId)
	{
		OwnerUserId = ownerUserId;
		Touch();
	}

	public void ChangePriority(AdmissionCrmLeadPriority priority)
	{
		Priority = priority;
		Touch();
	}

	public void UpdateScore(int score)
	{
		if (score < 0 || score > 100)
		{
			throw new BusinessException("AdmissionCrm:InvalidLeadScore")
				.WithData("Score", score);
		}

		LeadScore = score;
		Touch();
	}

	public void IssueRegistrationForm(string formNumber, double amount, DateTime issuedAt)
	{
		if (Stage == AdmissionInquiryStage.Archived || MergedIntoLeadId.HasValue)
		{
			throw new BusinessException("AdmissionCrm:RegistrationFormCannotBeIssuedForClosedLead")
				.WithData("LeadId", Id)
				.WithData("Stage", Stage);
		}

		if (RegistrationFormIssuedAt.HasValue)
		{
			throw new BusinessException("AdmissionCrm:RegistrationFormAlreadyIssued")
				.WithData("LeadId", Id)
				.WithData("FormNumber", RegistrationFormNumber);
		}

		var normalizedFormNumber = Check.NotNullOrWhiteSpace(
			formNumber,
			nameof(formNumber),
			maxLength: AdmissionCrmConsts.MaxRegistrationFormNumberLength);
		if (!IsValidRegistrationFormNumber(normalizedFormNumber))
		{
			throw new BusinessException("AdmissionCrm:InvalidRegistrationFormNumber")
				.WithData("FormNumber", normalizedFormNumber);
		}

		if (amount is < 0 or > 999999.99)
		{
			throw new BusinessException("AdmissionCrm:InvalidRegistrationFormAmount")
				.WithData("Amount", amount);
		}

		RegistrationFormNumber = normalizedFormNumber;
		RegistrationFormAmount = amount;
		RegistrationFormIssuedAt = issuedAt;
		Touch();
	}

	public void MarkStale()
	{
		if (Stage is AdmissionInquiryStage.Archived or AdmissionInquiryStage.Eligible or AdmissionInquiryStage.Rejected)
		{
			return;
		}

		Stage = AdmissionInquiryStage.Stale;
	}

	public void Archive(string reason, DateTime archivedAt)
	{
		if (Stage == AdmissionInquiryStage.Archived)
		{
			return;
		}

		Stage = AdmissionInquiryStage.Archived;
		ArchivedAt = archivedAt;
		ArchiveReason = Check.NotNullOrWhiteSpace(
			reason,
			nameof(reason),
			maxLength: AdmissionCrmConsts.MaxNotesLength);
	}

	public void MergeInto(Guid targetLeadId, string reason, DateTime mergedAt)
	{
		if (targetLeadId == Id)
		{
			throw new BusinessException("AdmissionCrm:CannotMergeLeadIntoItself")
				.WithData("LeadId", Id);
		}

		var normalizedReason = Check.NotNullOrWhiteSpace(
			reason,
			nameof(reason),
			maxLength: AdmissionCrmConsts.MaxNotesLength);
		MergedIntoLeadId = targetLeadId;
		MergedAt = mergedAt;
		MergeReason = normalizedReason;
		Stage = AdmissionInquiryStage.Archived;
		ArchivedAt = mergedAt;
		ArchiveReason = normalizedReason;
		Touch();
	}

	public void RecordDuplicateContinue(string reason)
	{
		DuplicateContinueReason = Check.NotNullOrWhiteSpace(
			reason,
			nameof(reason),
			maxLength: AdmissionCrmConsts.MaxNotesLength);
		Touch();
	}

	public void RecordStaffNoteActivity()
	{
		Touch();
	}

	public void Reopen(string reason, DateTime reopenedAt)
	{
		if (Stage != AdmissionInquiryStage.Archived && Stage != AdmissionInquiryStage.Stale)
		{
			throw new BusinessException("AdmissionCrm:LeadNotReopenable")
				.WithData("LeadId", Id)
				.WithData("Stage", Stage);
		}

		Stage = AdmissionInquiryStage.FollowUp;
		ReopenedAt = reopenedAt;
		ReopenReason = Check.NotNullOrWhiteSpace(
			reason,
			nameof(reason),
			maxLength: AdmissionCrmConsts.MaxNotesLength);
		Touch();
	}

	private void SetInquirySource(Guid? inquirySourceId, string? inquirySourceSnapshot)
	{
		InquirySourceId = inquirySourceId;
		InquirySourceSnapshot = NormalizeOptional(
			inquirySourceSnapshot,
			nameof(inquirySourceSnapshot),
			AdmissionCrmConsts.MaxSourceSnapshotLength);
	}

	private void SetChannel(AdmissionInquiryChannel channel, string? campaignName)
	{
		Channel = channel;
		CampaignName = NormalizeOptional(
			campaignName,
			nameof(campaignName),
			AdmissionCrmConsts.MaxCampaignLength);
	}

	private void SetRoutingContext(string? campusBranchName, string? preferredCommunicationLanguage)
	{
		CampusBranchName = NormalizeOptional(
			campusBranchName,
			nameof(campusBranchName),
			AdmissionCrmConsts.MaxNameLength);
		PreferredCommunicationLanguage = NormalizeOptional(
			preferredCommunicationLanguage,
			nameof(preferredCommunicationLanguage),
			AdmissionCrmConsts.MaxNameLength);
	}

	private void SetApplicantName(
		string? firstName,
		string? middleName,
		string? lastName,
		DateTimeOffset? dateOfBirth,
		string? parentGuardianName)
	{
		FirstName = NormalizeOptional(firstName, nameof(firstName), AdmissionCrmConsts.MaxNameLength);
		MiddleName = NormalizeOptional(middleName, nameof(middleName), AdmissionCrmConsts.MaxNameLength);
		LastName = NormalizeOptional(lastName, nameof(lastName), AdmissionCrmConsts.MaxNameLength);
		DateOfBirth = dateOfBirth;
		ParentGuardianName = NormalizeOptional(parentGuardianName, nameof(parentGuardianName), AdmissionCrmConsts.MaxNameLength);
	}

	private void SetContact(string? primaryContactNo, string? email)
	{
		if (!AdmissionCrmContactRules.IsValidPhone(primaryContactNo))
		{
			throw new BusinessException("AdmissionCrm:InvalidContactNumber");
		}

		PrimaryContactNo = NormalizeOptional(
			primaryContactNo,
			nameof(primaryContactNo),
			AdmissionCrmConsts.MaxContactLength);
		Email = NormalizeEmail(email);
		NormalizedPrimaryContactNo = NormalizePhone(PrimaryContactNo);
		NormalizedEmail = Email?.ToLowerInvariant();

		if (string.IsNullOrWhiteSpace(NormalizedPrimaryContactNo) &&
		    string.IsNullOrWhiteSpace(NormalizedEmail))
		{
			throw new BusinessException("AdmissionCrm:ContactRequired");
		}
	}

	private void SetIntendedRoute(
		AdmissionCrmRouteType? intendedRouteType,
		Guid? intendedRouteDefinitionId,
		int? intendedRouteDefinitionVersion,
		string? intendedRouteNameSnapshot)
	{
		if (intendedRouteType.HasValue && !Enum.IsDefined(intendedRouteType.Value))
		{
			throw new BusinessException("AdmissionCrm:InvalidIntendedRouteType")
				.WithData("IntendedRouteType", intendedRouteType.Value);
		}

		if (intendedRouteDefinitionId.HasValue &&
		    (!intendedRouteType.HasValue ||
		     !intendedRouteDefinitionVersion.HasValue ||
		     intendedRouteDefinitionVersion.Value <= 0 ||
		     string.IsNullOrWhiteSpace(intendedRouteNameSnapshot)))
		{
			throw new BusinessException("AdmissionCrm:InvalidIntendedRouteDefinition")
				.WithData("IntendedRouteDefinitionId", intendedRouteDefinitionId.Value);
		}

		if (!intendedRouteDefinitionId.HasValue &&
		    (intendedRouteDefinitionVersion.HasValue || !string.IsNullOrWhiteSpace(intendedRouteNameSnapshot)))
		{
			throw new BusinessException("AdmissionCrm:InvalidIntendedRouteDefinition");
		}

		IntendedRouteType = intendedRouteType;
		IntendedRouteDefinitionId = intendedRouteDefinitionId;
		IntendedRouteDefinitionVersion = intendedRouteDefinitionVersion;
		IntendedRouteNameSnapshot = NormalizeOptional(
			intendedRouteNameSnapshot,
			nameof(intendedRouteNameSnapshot),
			AdmissionCrmConsts.MaxNameLength);
	}

	private void SetIntent(string admissionIntent, Guid? targetStandardId, string? desiredClassName)
	{
		AdmissionIntent = Check.NotNullOrWhiteSpace(
			admissionIntent,
			nameof(admissionIntent),
			maxLength: AdmissionCrmConsts.MaxIntentLength);
		TargetStandardId = targetStandardId;
		DesiredClassName = NormalizeOptional(
			desiredClassName,
			nameof(desiredClassName),
			AdmissionCrmConsts.MaxNameLength);
	}

	private void SetNotes(string? notes)
	{
		Notes = NormalizeOptional(notes, nameof(notes), AdmissionCrmConsts.MaxNotesLength);
	}

	private void Touch()
	{
		LastActivityTime = DateTimeOffset.UtcNow;
	}

	private static string? NormalizeOptional(string? value, string name, int maxLength)
	{
		return string.IsNullOrWhiteSpace(value)
			? null
			: Check.Length(value.Trim(), name, maxLength);
	}

	private static string? NormalizePhone(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}

		var digits = new string(value.Where(char.IsDigit).ToArray());
		return string.IsNullOrWhiteSpace(digits) ? null : digits;
	}

	private static string? NormalizeEmail(string? value)
	{
		var normalized = NormalizeOptional(value, nameof(value), AdmissionCrmConsts.MaxEmailLength);
		if (normalized == null)
		{
			return null;
		}

		if (!MailAddress.TryCreate(normalized, out var parsed) ||
		    !string.Equals(parsed.Address, normalized, StringComparison.OrdinalIgnoreCase))
		{
			throw new BusinessException("AdmissionCrm:InvalidEmail")
				.WithData("Email", normalized);
		}

		return normalized;
	}

	private static bool IsValidRegistrationFormNumber(string formNumber)
	{
		return formNumber.All(character =>
			char.IsAsciiLetterUpper(character) ||
			char.IsDigit(character) ||
			character == '-');
	}
}
