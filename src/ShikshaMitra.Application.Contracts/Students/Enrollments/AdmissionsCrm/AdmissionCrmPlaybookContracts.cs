using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPlaybookApplicabilityDto
{
	public string? Campus { get; set; }
	public string? DesiredClass { get; set; }
	public string? Board { get; set; }
	public string? Medium { get; set; }
	public string? ApplicantCategory { get; set; }
	public string? AdmissionRound { get; set; }
	public string? AdmissionRoute { get; set; }
}

public class AdmissionCrmPlaybookStageDto
{
	public required string Code { get; set; }
	public required string DisplayName { get; set; }
	public AdmissionInquiryStage ReportingMilestone { get; set; }
	public int Order { get; set; }
	public bool IsRequired { get; set; }
	public bool IsParentVisible { get; set; }
	public int? SlaHours { get; set; }
	public IReadOnlyList<string> AllowedNextStageCodes { get; set; } = [];
}

public class AdmissionCrmAdmissionRoundDto
{
	public required string Code { get; set; }
	public required string Name { get; set; }
	public DateTime OpeningDate { get; set; }
	public DateTime ClosingDate { get; set; }
	public DateTime? ExamWindowStart { get; set; }
	public DateTime? ExamWindowEnd { get; set; }
	public DateTime? RegistrationDeadline { get; set; }
	public DateTime? DecisionDeadline { get; set; }
	public bool IsActive { get; set; } = true;
}

public class AdmissionCrmSeatPlanDto
{
	public required string Code { get; set; }
	public string? RoundCode { get; set; }
	public string? Campus { get; set; }
	public string? DesiredClass { get; set; }
	public string? Board { get; set; }
	public string? Medium { get; set; }
	public string? ApplicantCategory { get; set; }
	public int PlannedSeats { get; set; }
	public int ApprovedCapacity { get; set; }
	public int OfferedSeats { get; set; }
	public int AcceptedSeats { get; set; }
	public int WaitlistedApplicants { get; set; }
	public int WithdrawnApplicants { get; set; }
	public int RemainingCapacity { get; set; }
	public bool IsOverAllocated { get; set; }
}

public class AdmissionCrmExamComponentDto
{
	public required string Code { get; set; }
	public required string Name { get; set; }
	public AdmissionCrmAssessmentComponentType ComponentType { get; set; }
	public decimal MaximumMarks { get; set; }
	public decimal? MinimumMarks { get; set; }
	public decimal WeightPercentage { get; set; }
	public bool IsMandatory { get; set; }
	public int ScorePrecision { get; set; }
}

public class AdmissionCrmExamResultRuleDto
{
	public decimal AggregatePassingPercentage { get; set; }
	public bool RequireMandatoryComponentPass { get; set; } = true;
	public int? MeritCapacity { get; set; }
	public int? WaitlistCapacity { get; set; }
	public IReadOnlyList<string> TieBreakerCodes { get; set; } = [];
}

public class AdmissionCrmExamDefinitionDto
{
	public required string Code { get; set; }
	public required string Name { get; set; }
	public string? RoundCode { get; set; }
	public string? Campus { get; set; }
	public string? DesiredClass { get; set; }
	public string? Board { get; set; }
	public string? Medium { get; set; }
	public int DurationMinutes { get; set; }
	public string? Instructions { get; set; }
	public IReadOnlyList<AdmissionCrmExamComponentDto> Components { get; set; } = [];
	public AdmissionCrmExamResultRuleDto ResultRule { get; set; } = new();
}

public class AdmissionCrmSlotTemplateDto
{
	public required string Code { get; set; }
	public required string Name { get; set; }
	public required string ExamDefinitionCode { get; set; }
	public string? Campus { get; set; }
	public string? RoomOrHall { get; set; }
	public int RoomNo { get; set; }
	public int Capacity { get; set; }
	public TimeSpan ReportingTime { get; set; }
	public TimeSpan StartTime { get; set; }
	public TimeSpan EndTime { get; set; }
	public DateTime? ValidFrom { get; set; }
	public DateTime? ValidTo { get; set; }
	public IReadOnlyList<DayOfWeek> DaysOfWeek { get; set; } = [];
	public IReadOnlyList<DateTime> BlackoutDates { get; set; } = [];
	public bool IsActive { get; set; } = true;
}

public class AdmissionCrmSlotAssignmentRuleDto
{
	public required string Code { get; set; }
	public required string Name { get; set; }
	public required string Trigger { get; set; }
	public AdmissionCrmSlotAssignmentStrategy Strategy { get; set; }
	public int Priority { get; set; }
	public string? SlotTemplateCode { get; set; }
	public bool IsActive { get; set; } = true;
}

public abstract class AdmissionCrmPlaybookUpsertDto
{
	public required string Name { get; set; }
	public bool IsFallback { get; set; }
	public AdmissionCrmRegistrationPath RegistrationPath { get; set; }
	public AdmissionCrmPlaybookApplicabilityDto Applicability { get; set; } = new();
	public IReadOnlyList<AdmissionCrmPlaybookStageDto> Stages { get; set; } = [];
	public IReadOnlyList<AdmissionCrmAdmissionRoundDto> Rounds { get; set; } = [];
	public IReadOnlyList<AdmissionCrmSeatPlanDto> SeatPlans { get; set; } = [];
	public IReadOnlyList<AdmissionCrmExamDefinitionDto> ExamDefinitions { get; set; } = [];
	public IReadOnlyList<AdmissionCrmSlotTemplateDto> SlotTemplates { get; set; } = [];
	public IReadOnlyList<AdmissionCrmSlotAssignmentRuleDto> AssignmentRules { get; set; } = [];
	public string? Notes { get; set; }
}

public class CreateAdmissionCrmPlaybookDto : AdmissionCrmPlaybookUpsertDto
{
}

public class UpdateAdmissionCrmPlaybookDto : AdmissionCrmPlaybookUpsertDto
{
}

public class AdmissionCrmPlaybookDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public string Name { get; set; } = string.Empty;
	public int Version { get; set; }
	public AdmissionCrmPlaybookStatus Status { get; set; }
	public bool IsFallback { get; set; }
	public AdmissionCrmRegistrationPath RegistrationPath { get; set; }
	public AdmissionCrmPlaybookApplicabilityDto Applicability { get; set; } = new();
	public IReadOnlyList<AdmissionCrmPlaybookStageDto> Stages { get; set; } = [];
	public IReadOnlyList<AdmissionCrmAdmissionRoundDto> Rounds { get; set; } = [];
	public IReadOnlyList<AdmissionCrmSeatPlanDto> SeatPlans { get; set; } = [];
	public IReadOnlyList<AdmissionCrmExamDefinitionDto> ExamDefinitions { get; set; } = [];
	public IReadOnlyList<AdmissionCrmSlotTemplateDto> SlotTemplates { get; set; } = [];
	public IReadOnlyList<AdmissionCrmSlotAssignmentRuleDto> AssignmentRules { get; set; } = [];
	public string? Notes { get; set; }
	public DateTime? PublishedAt { get; set; }
	public DateTime? RetiredAt { get; set; }
}

public class GetAdmissionCrmPlaybookListInput : PagedAndSortedResultRequestDto
{
	public string? Filter { get; set; }
	public AdmissionCrmPlaybookStatus? Status { get; set; }
}

public class ResolveAdmissionCrmPlaybookDto : AdmissionCrmPlaybookApplicabilityDto
{
}

public class AdmissionCrmPlaybookResolutionDto
{
	public AdmissionCrmPlaybookDto Playbook { get; set; } = new();
	public int SpecificityScore { get; set; }
	public string Explanation { get; set; } = string.Empty;
}

public class GenerateAdmissionCrmPlaybookSlotsDto
{
	public required string SlotTemplateCode { get; set; }
	public DateTime FromDate { get; set; }
	public DateTime ToDate { get; set; }
	public int UtcOffsetMinutes { get; set; }
}

public class AdmissionCrmGeneratedSlotDto
{
	public Guid SlotId { get; set; }
	public string SlotTemplateCode { get; set; } = string.Empty;
	public string ExamDefinitionCode { get; set; } = string.Empty;
	public string SlotName { get; set; } = string.Empty;
	public DateTimeOffset StartTime { get; set; }
	public DateTimeOffset EndTime { get; set; }
	public int Capacity { get; set; }
	public int RoomNo { get; set; }
	public bool WasCreated { get; set; }
}

public class AdmissionCrmSlotGenerationResultDto
{
	public int CreatedCount { get; set; }
	public int ExistingCount { get; set; }
	public IReadOnlyList<AdmissionCrmGeneratedSlotDto> Slots { get; set; } = [];
}

public class RecommendAdmissionCrmPlaybookSlotDto
{
	public required string AssignmentRuleCode { get; set; }
	public DateTime? PreferredDate { get; set; }
}

public class AdmissionCrmSlotRecommendationDto
{
	public Guid EntranceExamSlotId { get; set; }
	public string SlotName { get; set; } = string.Empty;
	public DateTimeOffset StartTime { get; set; }
	public DateTimeOffset EndTime { get; set; }
	public int RoomNo { get; set; }
	public int Capacity { get; set; }
	public int ScheduledCount { get; set; }
	public int AvailableSeats { get; set; }
	public AdmissionCrmSlotAssignmentStrategy Strategy { get; set; }
	public string Explanation { get; set; } = string.Empty;
}

public class PreviewAdmissionCrmExamResultDto
{
	public required string ExamDefinitionCode { get; set; }
	public IReadOnlyDictionary<string, decimal> ComponentScores { get; set; } =
		new Dictionary<string, decimal>();
}

public class AdmissionCrmExamComponentResultPreviewDto
{
	public string ComponentCode { get; set; } = string.Empty;
	public decimal Score { get; set; }
	public decimal MaximumMarks { get; set; }
	public decimal Percentage { get; set; }
	public decimal WeightedContribution { get; set; }
	public bool PassedMinimum { get; set; }
}

public class AdmissionCrmExamResultPreviewDto
{
	public string ExamDefinitionCode { get; set; } = string.Empty;
	public decimal WeightedPercentage { get; set; }
	public bool MandatoryComponentsPassed { get; set; }
	public bool Passed { get; set; }
	public string Outcome { get; set; } = string.Empty;
	public IReadOnlyList<string> DecisionReasons { get; set; } = [];
	public IReadOnlyList<string> TieBreakerCodes { get; set; } = [];
	public IReadOnlyList<AdmissionCrmExamComponentResultPreviewDto> Components { get; set; } = [];
}
