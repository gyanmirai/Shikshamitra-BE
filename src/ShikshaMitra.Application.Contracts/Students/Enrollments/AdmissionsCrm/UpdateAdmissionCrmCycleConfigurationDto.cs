using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class UpdateAdmissionCrmCycleConfigurationDto
{
	[MaxLength(64)]
	public string? ImpactPreviewFingerprint { get; set; }

	[Required]
	[MaxLength(AdmissionCrmConsts.MaxCycleNameLength)]
	public required string CycleName { get; set; }

	public bool IsActive { get; set; } = true;
	public bool RegistrationFormRequiredBeforeExam { get; set; }
	public bool EntranceExamRequired { get; set; } = true;

	[Range(1, 365)]
	public int StaleAfterDays { get; set; } = 30;

	[Range(2, 730)]
	public int ArchiveAfterDays { get; set; } = 60;

	[Range(1, 720)]
	public int DefaultLeadSlaHours { get; set; } = 24;
	public AdmissionCrmSlaPolicyDto SlaPolicy { get; set; } = new();

	[Range(0, 90)]
	public int ActivationCancellationWindowDays { get; set; } = 7;

	[Range(0, 100)]
	public decimal DefaultExamPassingPercentage { get; set; } = 40;

	public AdmissionCrmMeritRankingMode MeritRankingMode { get; set; } =
		AdmissionCrmMeritRankingMode.MarksThenInquiryDate;

	public AdmissionCrmExamReadinessField ExamReadinessFields { get; set; } =
		AdmissionCrmExamReadinessField.Contact |
		AdmissionCrmExamReadinessField.AdmissionIntent |
		AdmissionCrmExamReadinessField.DesiredClass;

	public AdmissionCrmLeadScoringRulesDto LeadScoringRules { get; set; } =
		AdmissionCrmLeadScoringRulesDto.CreateDefault();

	public AdmissionCrmLeadRoutingSettingsDto LeadRoutingSettings { get; set; } =
		AdmissionCrmLeadRoutingSettingsDto.CreateDefault();

	public List<AdmissionCrmJourneyRuleDto> JourneyRules { get; set; } = new();
	public List<AdmissionCrmApplicantChecklistItemDto> ApplicantChecklist { get; set; } = new();
	public List<AdmissionCrmChannelSettingDto> ChannelSettings { get; set; } = new();

	[MaxLength(AdmissionCrmConsts.MaxNotesLength)]
	public string? Notes { get; set; }
}
