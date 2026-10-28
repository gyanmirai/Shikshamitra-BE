using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Records;

public sealed class StudentRecordTransitionInput
{
	public Guid StudentId { get; set; }
	[Required] public string Outcome { get; set; } = "Promote";
	public Guid? TargetAcademicYearId { get; set; }
	public Guid? TargetStandardId { get; set; }
	public Guid? TargetDivisionId { get; set; }
	[Range(1, int.MaxValue)] public int? RollNumber { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? RollAssignmentMode { get; set; }
	public DateTime EffectiveDate { get; set; }
	[Required, StringLength(1000)] public string Reason { get; set; } = "";
	public string? StudentConcurrencyStamp { get; set; }
	public string? EnrollmentConcurrencyStamp { get; set; }
}
public sealed class StudentRecordTransitionBatchInput
{
	public Guid AcademicYearId { get; set; }
	[StringLength(128)] public string IdempotencyKey { get; set; } = "";
	public List<StudentRecordTransitionInput> Items { get; set; } = [];
}
public sealed class StudentRecordTransitionIssueDto
{
	public string Code { get; set; } = "";
	public string Message { get; set; } = "";
}
public sealed class StudentRecordTransitionPreviewRowDto
{
	public bool ApprovalRequired { get; set; }
	public List<StudentRecordTransitionIssueDto> OutstandingItems { get; set; } = [];
	[JsonIgnore] public string OutstandingEvidence { get; set; } = "[]";
	public ShikshaMitra.Finance.Fees.Collections.Dtos.FeeProrationResultDto? Proration { get; set; }
	public string RollNumberScope { get; set; } = "class-division";
	public string RollAssignmentMode { get; set; } = "manual";
	public int? ProposedRollNumber { get; set; }
	public bool OnboardingRollApprovalRequired { get; set; }
	public Guid? TransitionId { get; set; }
	public Guid StudentId { get; set; }
	public string? StudentName { get; set; }
	public string Outcome { get; set; } = "";
	public bool Ready { get; set; }
	public List<StudentRecordTransitionIssueDto> Issues { get; set; } = [];
	public string StudentConcurrencyStamp { get; set; } = "";
	public string EnrollmentConcurrencyStamp { get; set; } = "";
}
public sealed class StudentRecordTransitionPreviewDto
{
	public List<StudentRecordTransitionPreviewRowDto> Items { get; set; } = [];
	public int ReadyCount { get; set; }
	public int ExceptionCount { get; set; }
	public bool CanSubmit { get; set; }
	public string BatchExceptionMode { get; set; } = "";
	public Guid? PolicyVersionId { get; set; }
}
public sealed class StudentRecordTransitionBatchResultDto
{
	public Guid BatchId { get; set; }
	public List<StudentRecordTransitionDto> Items { get; set; } = [];
	public List<StudentRecordTransitionPreviewRowDto> Exceptions { get; set; } = [];
}
public sealed class StudentRecordTransitionQueryDto : PagedResultRequestDto
{
	public Guid? AcademicYearId { get; set; }
	public Guid? StudentId { get; set; }
	public string? Status { get; set; }
}
public sealed class StudentRecordTransitionDto
{
	public bool OutstandingOverrideRequired { get; set; }
	public List<StudentRecordTransitionIssueDto> OutstandingItems { get; set; } = [];
	public Guid? AdmissionApplicationId { get; set; }
	public Guid? AdmissionInquiryId { get; set; }
	public bool CanApprove { get; set; }
	public bool CanCancel { get; set; }
	public bool CanApply { get; set; }
	public bool CanCorrect { get; set; }
	public Guid Id { get; set; }
	public Guid BatchId { get; set; }
	public Guid StudentId { get; set; }
	public string? StudentName { get; set; }
	public Guid AcademicYearId { get; set; }
	public string Outcome { get; set; } = "";
	public string Status { get; set; } = "";
	public string Reason { get; set; } = "";
	public DateTime EffectiveDate { get; set; }
	public Guid? TargetAcademicYearId { get; set; }
	public Guid? TargetStandardId { get; set; }
	public Guid? TargetDivisionId { get; set; }
	public int? RollNumber { get; set; }
	public int CurrentApprovalStage { get; set; }
	public int ApprovalStageCount { get; set; }
	public string ConcurrencyStamp { get; set; } = "";
	public Guid? RenewalCaseId { get; set; }
	public Guid? CorrectionOfId { get; set; }
	public string? LastError { get; set; }
	public List<StudentRecordTransitionEventDto> History { get; set; } = [];
}
public sealed class StudentRecordTransitionEventDto
{
	public string Action { get; set; } = "";
	public Guid ActorId { get; set; }
	public int Stage { get; set; }
	public string Remarks { get; set; } = "";
	public DateTime OccurredAt { get; set; }
}
public sealed class StudentRecordTransitionDecisionDto
{
	[StringLength(1000)] public string OutstandingReason { get; set; } = "";
	[Required] public string ConcurrencyStamp { get; set; } = "";
	[StringLength(1000)] public string Remarks { get; set; } = "";
}
public sealed class StudentRecordTransitionCorrectionDto
{
	[Required] public string ConcurrencyStamp { get; set; } = "";
	[Required, StringLength(128)] public string IdempotencyKey { get; set; } = "";
	[Required, StringLength(1000)] public string Reason { get; set; } = "";
	public bool PreviewOnly { get; set; } = true;
}
public sealed class StudentRecordReturnInput
{
	public StudentRecordReturnInquiryDto? Inquiry { get; set; }
	public AdmissionEntryMode? EntryPath { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? RollAssignmentMode { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? StudentConcurrencyStamp { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? EnrollmentConcurrencyStamp { get; set; }
	public Guid StudentId { get; set; }
	public Guid TargetAcademicYearId { get; set; }
	public Guid? TargetStandardId { get; set; }
	public Guid? TargetDivisionId { get; set; }
	public int? RollNumber { get; set; }
	public DateTime? EffectiveDate { get; set; }
	public string? Route { get; set; }
	[Required, StringLength(128)] public string IdempotencyKey { get; set; } = "";
	[Required, StringLength(1000)] public string Reason { get; set; } = "";
	public Guid? AdmissionOfferingId { get; set; }
	public Guid? ResumeApplicationId { get; set; }
	public StudentRecordReturnRegistrationDto? Registration { get; set; }
}
public sealed class StudentRecordReturnRegistrationDto
{
	[Required, StringLength(128)] public string GuardianFirstName { get; set; } = "";
	[StringLength(128)] public string? GuardianLastName { get; set; }
	[Required, StringLength(32, MinimumLength = 7)] public string GuardianMobile { get; set; } = "";
	[EmailAddress, StringLength(256)] public string? GuardianEmail { get; set; }
	public AdmissionGuardianRelationship GuardianRelationship { get; set; } = AdmissionGuardianRelationship.Parent;
}
public sealed class StudentRecordReturnInquiryDto
{
	[Required] public AdmissionInquirySource? Source { get; set; }
	public string? SourceDetail { get; set; }
	public string PreferredLanguageCode { get; set; } = "en";
	public List<AdmissionFormValueDto> FormValues { get; set; } = [];
}
public sealed class StudentRecordReturnInquiryIntakeDto
{
	public List<string> ServerBoundFieldCodes { get; set; } = [];
	public AdmissionFormDto? Form { get; set; }
	public List<AdmissionInquirySource> AllowedSources { get; set; } = [];
	public List<AdmissionInquirySource> RequireSourceDetailFor { get; set; } = [];
	public bool RequireGuardianEmail { get; set; }
	public bool RequireApplicantDateOfBirth { get; set; }
	public List<AdmissionFormValueDto> BoundValues { get; set; } = [];
}
public sealed class StudentRecordReturnDto
{
	public Guid StudentId { get; set; }
	public string Route { get; set; } = "";
	public Guid? RenewalCaseId { get; set; }
	public Guid? TransitionId { get; set; }
	public Guid? AdmissionApplicationId { get; set; }
	public Guid? AdmissionInquiryId { get; set; }
	public string Status { get; set; } = "";
}
public sealed class StudentRecordRenewalDto
{
	public Guid Id { get; set; }
	public Guid StudentId { get; set; }
	public string? StudentName { get; set; }
	public Guid AcademicYearId { get; set; }
	public Guid StandardId { get; set; }
	public Guid DivisionId { get; set; }
	public int? RollNumber { get; set; }
	public string Status { get; set; } = "";
	public string ConcurrencyStamp { get; set; } = "";
	public bool GuardianReviewRequired { get; set; }
	public bool GuardianReviewConfirmed { get; set; }
	public bool FinanceRequired { get; set; }
	public bool PortalReviewRequired { get; set; }
	public List<StudentRecordTransitionIssueDto> Issues { get; set; } = [];
}
public sealed class StudentRecordRenewalDecisionDto
{
	[Required] public string ConcurrencyStamp { get; set; } = "";
	[Required, StringLength(1000)] public string Remarks { get; set; } = "";
}
public sealed class StudentRecordTransitionOptionDto
{
	public StudentRecordReturnInquiryIntakeDto? InquiryIntake { get; set; }
	public AdmissionEntryMode? EntryMode { get; set; }
	public Guid Id { get; set; }
	public string Name { get; set; } = "";
	public Guid? ParentId { get; set; }
}
public sealed class StudentRecordTransitionOptionsDto
{
	public string RollAssignmentMode { get; set; } = "manual";
	public bool FreshApplicationRequiresRegistration { get; set; }
	public List<StudentRecordTransitionOptionDto> AcademicYears { get; set; } = [];
	public List<StudentRecordTransitionOptionDto> Standards { get; set; } = [];
	public List<StudentRecordTransitionOptionDto> Divisions { get; set; } = [];
	public List<StudentRecordTransitionOptionDto> Offerings { get; set; } = [];
	public List<StudentRecordTransitionOptionDto> ResumeApplications { get; set; } = [];
	public StudentRecordReturnPolicyDto ReturnPolicy { get; set; } = new();
}
public sealed class StudentRecordReturnPolicyDto
{
	public List<string> AllowedRoutes { get; set; } = [];
	public string DefaultRoute { get; set; } = "ReuseProfile";
}
public sealed class StudentRecordRenewalPortalOptionsDto
{
	public bool StudentRequired { get; set; }
	public bool GuardianRequired { get; set; }
	public bool CanProvision { get; set; }
	public string? StudentEmail { get; set; }
	public List<StudentRecordRenewalGuardianOptionDto> Guardians { get; set; } = [];
	public List<StudentPortalAccountDto> Accounts { get; set; } = [];
}
public sealed class StudentRecordRenewalGuardianOptionDto
{
	public string Relationship { get; set; } = "";
	public string Name { get; set; } = "";
	public string Email { get; set; } = "";
}
public interface IStudentRecordTransitionAppService : IApplicationService
{
	Task<PagedResultDto<StudentRecordTransitionDto>> GetListAsync(StudentRecordTransitionQueryDto input);
	Task<StudentRecordTransitionDto> GetAsync(Guid id);
	Task<StudentSchoolExitSummaryDto> GetSchoolExitSummaryAsync(Guid studentId, Guid academicYearId, string outcome = "Withdraw", DateTime? effectiveDate = null);
	Task<StudentRecordTransitionPreviewDto> PreviewAsync(StudentRecordTransitionBatchInput input);
	Task<StudentRecordTransitionOptionsDto> GetOptionsAsync(Guid? academicYearId, Guid? studentId, Guid? admissionOfferingId = null);
	Task<StudentRecordTransitionBatchResultDto> SubmitBatchAsync(StudentRecordTransitionBatchInput input);
	Task<StudentRecordTransitionDto> ApproveAsync(Guid id, StudentRecordTransitionDecisionDto input);
	Task<StudentRecordTransitionDto> RejectAsync(Guid id, StudentRecordTransitionDecisionDto input);
	Task<StudentRecordTransitionDto> CancelAsync(Guid id, StudentRecordTransitionDecisionDto input);
	Task<StudentRecordTransitionDto> ApplyAsync(Guid id, StudentRecordTransitionDecisionDto input);
	Task<StudentRecordTransitionPreviewRowDto> CorrectAsync(Guid id, StudentRecordTransitionCorrectionDto input);
	Task<StudentRecordReturnDto> ReturnAsync(StudentRecordReturnInput input);
	Task<StudentRecordTransitionPreviewRowDto> PreviewReturnAsync(StudentRecordReturnInput input);
	Task<PagedResultDto<StudentRecordRenewalDto>> GetRenewalsAsync(StudentRecordTransitionQueryDto input);
	Task<StudentRecordRenewalDto> GetRenewalAsync(Guid id);
	Task<StudentRecordRenewalDto> ConfirmRenewalAsync(Guid id, StudentRecordRenewalDecisionDto input);
	Task<StudentRecordRenewalDto> ActivateRenewalAsync(Guid id, StudentRecordRenewalDecisionDto input);
	Task<StudentRecordRenewalPortalOptionsDto> GetRenewalPortalOptionsAsync(Guid id);
	Task<StudentPortalAccountDto> ProvisionRenewalPortalAsync(Guid id, ProvisionStudentPortalAccountDto input);
}
