using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmCycleConfigurationDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public string CycleName { get; set; } = string.Empty;
	public bool IsActive { get; set; }
	public bool RegistrationFormRequiredBeforeExam { get; set; }
	public bool EntranceExamRequired { get; set; } = true;
	public bool JourneyLocked { get; set; }
	public DateTime? JourneyLockedAt { get; set; }
	public Guid? JourneyLockedByUserId { get; set; }
	public string? JourneyLockReason { get; set; }
	public int StaleAfterDays { get; set; }
	public int ArchiveAfterDays { get; set; }
	public int DefaultLeadSlaHours { get; set; }
	public AdmissionCrmSlaPolicyDto SlaPolicy { get; set; } = new();
	public int ActivationCancellationWindowDays { get; set; }
	public decimal DefaultExamPassingPercentage { get; set; }
	public AdmissionCrmMeritRankingMode MeritRankingMode { get; set; }
	public AdmissionCrmExamReadinessField ExamReadinessFields { get; set; }
	public AdmissionCrmLeadScoringRulesDto LeadScoringRules { get; set; } =
		AdmissionCrmLeadScoringRulesDto.CreateDefault();
	public AdmissionCrmLeadRoutingSettingsDto LeadRoutingSettings { get; set; } =
		AdmissionCrmLeadRoutingSettingsDto.CreateDefault();
	public List<AdmissionCrmJourneyRuleDto> JourneyRules { get; set; } = new();
	public List<AdmissionCrmApplicantChecklistItemDto> ApplicantChecklist { get; set; } = new();
	public List<AdmissionCrmChannelSettingDto> ChannelSettings { get; set; } = new();
	public string? Notes { get; set; }
}
