using System;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public static class StudentOnboardingErrorCodes
{
	public const string FinalizationNotReady = "Students.Onboarding.FinalizationNotReady";
	public const string WorkflowIncomplete = "Students.Onboarding.WorkflowIncomplete";
	public const string CoreIdentityIncomplete = "Students.Onboarding.CoreIdentityIncomplete";
	public const string GuardianContactUnverified = "Students.Onboarding.GuardianContactUnverified";
	public const string DuplicateResolutionRequired = "Students.Onboarding.DuplicateResolutionRequired";
	public const string ExistingStudentInvalid = "Students.Onboarding.ExistingStudentInvalid";
	public const string LegacyEnrollDisabled = "Students.Onboarding.LegacyEnrollDisabled";
	public const string CaseNotEditable = "Students.Onboarding.CaseNotEditable";
	public const string InvalidStep = "Students.Onboarding.InvalidStep";
	public const string WaiverReasonRequired = "Students.Onboarding.WaiverReasonRequired";
	public const string PlacementInvalid = "Students.Onboarding.PlacementInvalid";
	public const string DivisionFull = "Students.Onboarding.DivisionFull";
	public const string SeatReservationExpired = "Students.Onboarding.SeatReservationExpired";
	public const string IdentifierSeriesUnavailable = "Students.Onboarding.IdentifierSeriesUnavailable";
	public const string IdentifierAlreadyAllocated = "Students.Onboarding.IdentifierAlreadyAllocated";
	public const string PolicyScopeInvalid = "Students.Onboarding.PolicyScopeInvalid";
	public const string IdentifierSeriesAlreadyExists = "Students.Onboarding.IdentifierSeriesAlreadyExists";
	public const string RequiredStepsIncomplete = "Students.Onboarding.RequiredStepsIncomplete";
	public const string EnrollmentAlreadyExists = "Students.Onboarding.EnrollmentAlreadyExists";
	public const string OnboardingAlreadyExists = "Students.Onboarding.OnboardingAlreadyExists";
	public const string RollNumberInvalid = "Students.Onboarding.RollNumberInvalid";
	public const string RollNumberDuplicate = "Students.Onboarding.RollNumberDuplicate";
	public const string ConcurrencyConflict = "Students.Onboarding.ConcurrencyConflict";
	public const string OwnerInvalid = "Students.Onboarding.OwnerInvalid";
}

public static class StudentOnboardingSettingsConsts
{
	public const int CurrentSchemaVersion = 1;
	public const int MaxConfigurationLength = 200000;
}

public static class StudentOnboardingStepKeys
{
	public const string Identity = "identity";
	public const string Family = "family";
	public const string Communication = "communication";
	public const string Documents = "documents";
	public const string MedicalAndPreviousSchool = "medical_previous_school";
	public const string PlacementAndSeat = "placement_seat";
	public const string IdentifiersAndRoll = "identifiers_roll";
	public const string FinanceAccountsServices = "finance_accounts_services";
	public const string FinalReadiness = "final_readiness";

	public static readonly (string Key, string Label)[] Defaults =
	[
		(Identity, "Identity and general profile"),
		(Family, "Family and emergency contacts"),
		(Communication, "Address and communication"),
		(Documents, "Documents and compliance"),
		(MedicalAndPreviousSchool, "Medical and previous-school information"),
		(PlacementAndSeat, "Academic placement and seat"),
		(IdentifiersAndRoll, "Identifiers and roll number"),
		(FinanceAccountsServices, "Finance, accounts, and optional services"),
		(FinalReadiness, "Final readiness and activation")
	];
}

public sealed record StudentOnboardingCompletedEto(
	Guid OnboardingCaseId,
	Guid StudentId,
	Guid StudentEnrollmentId,
	Guid AcademicYearId,
	Guid StandardId,
	Guid DivisionId,
	Guid? TenantId,
	DateTime CompletedAt);
