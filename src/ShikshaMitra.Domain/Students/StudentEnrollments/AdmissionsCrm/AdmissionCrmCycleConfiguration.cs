using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmCycleConfiguration : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	private const AdmissionCrmExamReadinessField DefaultExamReadinessFields =
		AdmissionCrmExamReadinessField.Contact |
		AdmissionCrmExamReadinessField.AdmissionIntent |
		AdmissionCrmExamReadinessField.DesiredClass;

	private const AdmissionCrmExamReadinessField AllExamReadinessFields =
		AdmissionCrmExamReadinessField.Contact |
		AdmissionCrmExamReadinessField.AdmissionIntent |
		AdmissionCrmExamReadinessField.DesiredClass |
		AdmissionCrmExamReadinessField.StudentName |
		AdmissionCrmExamReadinessField.ParentGuardianName |
		AdmissionCrmExamReadinessField.InquirySource;

	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public string CycleName { get; private set; } = string.Empty;
	public bool IsActive { get; private set; }
	public bool RegistrationFormRequiredBeforeExam { get; private set; }
	public bool EntranceExamRequired { get; private set; }
	public bool JourneyLocked { get; private set; }
	public DateTime? JourneyLockedAt { get; private set; }
	public Guid? JourneyLockedByUserId { get; private set; }
	public string? JourneyLockReason { get; private set; }
	public int StaleAfterDays { get; private set; }
	public int ArchiveAfterDays { get; private set; }
	public int DefaultLeadSlaHours { get; private set; }
	public int ActivationCancellationWindowDays { get; private set; }
	public decimal DefaultExamPassingPercentage { get; private set; }
	public AdmissionCrmMeritRankingMode MeritRankingMode { get; private set; }
	public AdmissionCrmExamReadinessField ExamReadinessFields { get; private set; }
	public string LeadScoringRulesJson { get; private set; } = "{}";
	public string LeadRoutingRulesJson { get; private set; } = "{}";
	public string JourneyRulesJson { get; private set; } = "[]";
	public string ApplicantChecklistJson { get; private set; } = "[]";
	public string ChannelSettingsJson { get; private set; } = "[]";
	public string SlaPolicyJson { get; private set; } = "{}";
	public string? Notes { get; private set; }

	protected AdmissionCrmCycleConfiguration()
	{
	}

	public AdmissionCrmCycleConfiguration(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		string cycleName)
		: base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		SetDefaults(cycleName);
	}

	public void Update(
		string cycleName,
		bool isActive,
		bool registrationFormRequiredBeforeExam,
		bool entranceExamRequired,
		int staleAfterDays,
		int archiveAfterDays,
		int defaultLeadSlaHours,
		int activationCancellationWindowDays,
		decimal defaultExamPassingPercentage,
		AdmissionCrmMeritRankingMode meritRankingMode,
		AdmissionCrmExamReadinessField examReadinessFields,
		string leadScoringRulesJson,
		string leadRoutingRulesJson,
		string journeyRulesJson,
		string applicantChecklistJson,
		string channelSettingsJson,
		string slaPolicyJson,
		string? notes)
	{
		SetCycleName(cycleName);
		IsActive = isActive;
		RegistrationFormRequiredBeforeExam = registrationFormRequiredBeforeExam;
		EntranceExamRequired = entranceExamRequired;
		SetStaleArchiveDays(staleAfterDays, archiveAfterDays);
		SetDefaultLeadSlaHours(defaultLeadSlaHours);
		SetActivationCancellationWindowDays(activationCancellationWindowDays);
		SetDefaultExamPassingPercentage(defaultExamPassingPercentage);
		MeritRankingMode = meritRankingMode;
		SetExamReadinessFields(examReadinessFields);
		SetLeadScoringRulesJson(leadScoringRulesJson);
		SetLeadRoutingRulesJson(leadRoutingRulesJson);
		SetJourneyRulesJson(journeyRulesJson);
		SetApplicantChecklistJson(applicantChecklistJson);
		SetChannelSettingsJson(channelSettingsJson);
		SlaPolicyJson = Check.NotNullOrWhiteSpace(slaPolicyJson, nameof(slaPolicyJson), AdmissionCrmConsts.MaxRoutingRulesJsonLength);
		Notes = NormalizeOptional(notes, nameof(notes), AdmissionCrmConsts.MaxNotesLength);
	}

	public void CopySettingsFrom(AdmissionCrmCycleConfiguration source, string cycleName)
	{
		Check.NotNull(source, nameof(source));

		Update(
			cycleName,
			source.IsActive,
			source.RegistrationFormRequiredBeforeExam,
			source.EntranceExamRequired,
			source.StaleAfterDays,
			source.ArchiveAfterDays,
			source.DefaultLeadSlaHours,
			source.ActivationCancellationWindowDays,
			source.DefaultExamPassingPercentage,
			source.MeritRankingMode,
			source.ExamReadinessFields,
			source.LeadScoringRulesJson,
			source.LeadRoutingRulesJson,
			source.JourneyRulesJson,
			source.ApplicantChecklistJson,
			source.ChannelSettingsJson,
			source.SlaPolicyJson,
			source.Notes);
	}

	private void SetDefaults(string cycleName)
	{
		SetCycleName(cycleName);
		IsActive = true;
		RegistrationFormRequiredBeforeExam = false;
		EntranceExamRequired = true;
		JourneyLocked = false;
		JourneyLockedAt = null;
		JourneyLockedByUserId = null;
		JourneyLockReason = null;
		StaleAfterDays = 30;
		ArchiveAfterDays = 60;
		DefaultLeadSlaHours = 24;
		ActivationCancellationWindowDays = 7;
		DefaultExamPassingPercentage = 40;
		MeritRankingMode = AdmissionCrmMeritRankingMode.MarksThenInquiryDate;
		ExamReadinessFields = DefaultExamReadinessFields;
		LeadScoringRulesJson = "{}";
		LeadRoutingRulesJson = "{}";
		JourneyRulesJson = "[]";
		ApplicantChecklistJson = "[]";
		ChannelSettingsJson = "[]";
		SlaPolicyJson = "{\"timeZoneId\":\"Asia/Kolkata\",\"workingDays\":[1,2,3,4,5,6],\"workingDayStartHour\":8,\"workingDayEndHour\":17,\"holidays\":[],\"escalationAfterHours\":24}";
	}

	public void LockJourney(Guid? lockedByUserId, DateTime lockedAt, string? reason)
	{
		JourneyLocked = true;
		JourneyLockedAt = lockedAt;
		JourneyLockedByUserId = lockedByUserId;
		JourneyLockReason = NormalizeOptional(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
	}

	public void UnlockJourney()
	{
		JourneyLocked = false;
		JourneyLockedAt = null;
		JourneyLockedByUserId = null;
		JourneyLockReason = null;
	}

	private void SetCycleName(string cycleName)
	{
		CycleName = Check.NotNullOrWhiteSpace(
			cycleName,
			nameof(cycleName),
			maxLength: AdmissionCrmConsts.MaxCycleNameLength);
	}

	private void SetStaleArchiveDays(int staleAfterDays, int archiveAfterDays)
	{
		if (staleAfterDays <= 0)
		{
			throw new BusinessException("AdmissionCrm:InvalidStaleAfterDays")
				.WithData("StaleAfterDays", staleAfterDays);
		}

		if (archiveAfterDays <= staleAfterDays)
		{
			throw new BusinessException("AdmissionCrm:InvalidArchiveAfterDays")
				.WithData("StaleAfterDays", staleAfterDays)
				.WithData("ArchiveAfterDays", archiveAfterDays);
		}

		StaleAfterDays = staleAfterDays;
		ArchiveAfterDays = archiveAfterDays;
	}

	private void SetDefaultLeadSlaHours(int defaultLeadSlaHours)
	{
		if (defaultLeadSlaHours <= 0)
		{
			throw new BusinessException("AdmissionCrm:InvalidLeadSlaHours")
				.WithData("DefaultLeadSlaHours", defaultLeadSlaHours);
		}

		DefaultLeadSlaHours = defaultLeadSlaHours;
	}

	private void SetActivationCancellationWindowDays(int value)
	{
		if (value is < 0 or > 90)
		{
			throw new BusinessException("AdmissionCrm:InvalidActivationCancellationWindowDays")
				.WithData("ActivationCancellationWindowDays", value);
		}

		ActivationCancellationWindowDays = value;
	}

	private void SetDefaultExamPassingPercentage(decimal defaultExamPassingPercentage)
	{
		if (defaultExamPassingPercentage < 0 || defaultExamPassingPercentage > 100)
		{
			throw new BusinessException("AdmissionCrm:InvalidPassingPercentage")
				.WithData("DefaultExamPassingPercentage", defaultExamPassingPercentage);
		}

		DefaultExamPassingPercentage = defaultExamPassingPercentage;
	}

	private void SetExamReadinessFields(AdmissionCrmExamReadinessField examReadinessFields)
	{
		if ((examReadinessFields & ~AllExamReadinessFields) != 0)
		{
			throw new BusinessException("AdmissionCrm:InvalidExamReadinessFields")
				.WithData("ExamReadinessFields", examReadinessFields);
		}

		ExamReadinessFields = examReadinessFields;
	}

	private void SetLeadScoringRulesJson(string leadScoringRulesJson)
	{
		LeadScoringRulesJson = Check.NotNullOrWhiteSpace(
			leadScoringRulesJson,
			nameof(leadScoringRulesJson),
			maxLength: AdmissionCrmConsts.MaxScoringRulesJsonLength);
	}

	private void SetLeadRoutingRulesJson(string leadRoutingRulesJson)
	{
		LeadRoutingRulesJson = Check.NotNullOrWhiteSpace(
			leadRoutingRulesJson,
			nameof(leadRoutingRulesJson),
			maxLength: AdmissionCrmConsts.MaxRoutingRulesJsonLength);
	}

	private void SetJourneyRulesJson(string journeyRulesJson)
	{
		JourneyRulesJson = Check.NotNullOrWhiteSpace(
			journeyRulesJson,
			nameof(journeyRulesJson),
			maxLength: AdmissionCrmConsts.MaxRoutingRulesJsonLength);
	}

	private void SetApplicantChecklistJson(string applicantChecklistJson)
	{
		ApplicantChecklistJson = Check.NotNullOrWhiteSpace(
			applicantChecklistJson,
			nameof(applicantChecklistJson),
			maxLength: AdmissionCrmConsts.MaxRoutingRulesJsonLength);
	}

	private void SetChannelSettingsJson(string channelSettingsJson)
	{
		ChannelSettingsJson = Check.NotNullOrWhiteSpace(
			channelSettingsJson,
			nameof(channelSettingsJson),
			maxLength: AdmissionCrmConsts.MaxRoutingRulesJsonLength);
	}

	private static string? NormalizeOptional(string? value, string name, int maxLength)
	{
		return string.IsNullOrWhiteSpace(value)
			? null
			: Check.Length(value.Trim(), name, maxLength);
	}
}
