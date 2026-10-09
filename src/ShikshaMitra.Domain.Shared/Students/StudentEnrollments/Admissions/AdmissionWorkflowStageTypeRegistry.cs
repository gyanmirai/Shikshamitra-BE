using System.Collections.Generic;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed record AdmissionWorkflowStageTypeMetadata(
	AdmissionWorkflowStageType Type,
	string DisplayName,
	string Description,
	string Icon,
	string DefaultColor,
	IReadOnlyList<string> SupportedConfiguration,
	string RuntimeHandler,
	string NextRequiredAction,
	bool CanBeEntryStage,
	bool CanBeTerminalStage,
	bool SupportsFormAttachment,
	bool SupportsConditions,
	bool IsRuntimeReady,
	bool IsSelectable,
	bool IsDeprecated,
	string? ReplacementGuidance);

public static class AdmissionWorkflowStageTypeRegistry
{
	public static IReadOnlyList<AdmissionWorkflowStageTypeMetadata> All { get; } =
	[
		Metadata(AdmissionWorkflowStageType.Inquiry, "Inquiry (legacy)", "Historical workflow stage for inquiry qualification.", "mdi:account-question-outline", "#0EA5E9", true, true, false, true, true, "InquiryStageHandler", ["inquiry"], false, true, "Manage inquiry capture and qualification in Admissions > Inquiries. Start new admission processes with Application Form or another application-stage step."),
		Metadata(AdmissionWorkflowStageType.Form, "Form", "Collect, submit, verify, and correct a published dynamic form.", "mdi:form-select", "#6366F1", true, true, true, true, true, "FormStageHandler", ["formDefinitionId", "form"]),
		Metadata(AdmissionWorkflowStageType.Assessment, "Assessment", "Record an entrance exam, interaction, or other assessment.", "mdi:clipboard-text-outline", "#8B5CF6", true, true, false, true, true, "AssessmentStageHandler", ["assessment"]),
		Metadata(AdmissionWorkflowStageType.Eligibility, "Eligibility (retired)", "Retired admission stage.", "mdi:shield-check-outline", "#14B8A6", true, true, false, true, false, "EligibilityStageHandler", ["eligibility"], false, true, "Use the five supported admission stage types."),
		Metadata(AdmissionWorkflowStageType.Payment, "Payment", "Confirm the published Finance fee structure; collection happens through Finance after admission.", "mdi:cash-check", "#F59E0B", true, true, false, true, true, "PaymentStageHandler"),
		Metadata(AdmissionWorkflowStageType.DocumentVerification, "Document verification", "Review required applicant documents.", "mdi:file-check-outline", "#06B6D4", true, true, false, true, true, "DocumentVerificationStageHandler", ["documentVerification"]),
		Metadata(AdmissionWorkflowStageType.Review, "Review (retired)", "Retired admission stage.", "mdi:account-search-outline", "#64748B", true, true, false, true, false, "ReviewStageHandler", ["review"], false, true, "Use Admission decision."),
		Metadata(AdmissionWorkflowStageType.Approval, "Admission decision", "Record an authorized approve, waitlist, or reject decision.", "mdi:check-decagram-outline", "#22C55E", true, true, false, true, true, "ApprovalStageHandler"),
		Metadata(AdmissionWorkflowStageType.Offer, "Offer (retired)", "Retired admission stage.", "mdi:email-seal-outline", "#10B981", true, true, false, true, false, "OfferStageHandler", ["offer"], false, true, "Approve the admission decision and finalize the completed journey."),
		Metadata(AdmissionWorkflowStageType.Seat, "Seat (legacy)", "Legacy workflow stage for admission capacity.", "mdi:seat-outline", "#F97316", true, true, false, true, true, "SeatStageHandler", ["seat"], false, true, "This stage is unavailable for new workflows. Existing published workflows continue to run while capacity setup moves to Admission Offering."),
		Metadata(AdmissionWorkflowStageType.Enrollment, "Enrollment (legacy)", "Legacy workflow stage for completing enrollment.", "mdi:account-school-outline", "#16A34A", true, true, false, false, true, "EnrollmentStageHandler", null, false, true, "Use Admission decision and Finalize admission. Student Onboarding owns the post-admission handoff."),
		Metadata(AdmissionWorkflowStageType.Custom, "Custom (legacy)", "Legacy school-specific manual stage.", "mdi:puzzle-outline", "#475569", true, true, false, true, true, "CustomStageHandler", null, false, true, "Replace it with Form, Review, Assessment, Appointment, or Admission decision so requirements and outcomes remain structured."),
		Metadata(AdmissionWorkflowStageType.Appointment, "Appointment", "Schedule and complete an applicant appointment.", "mdi:calendar-clock-outline", "#EC4899", true, true, false, true, false, "AppointmentStageHandler", null, false, false, "Appointment will become selectable after its runtime scheduling and completion flow is available.")
	];

	public static AdmissionWorkflowStageTypeMetadata? Find(AdmissionWorkflowStageType type)
	{
		foreach (var metadata in All)
		{
			if (metadata.Type == type)
			{
				return metadata;
			}
		}

		return null;
	}

	private static AdmissionWorkflowStageTypeMetadata Metadata(
		AdmissionWorkflowStageType type,
		string displayName,
		string description,
		string icon,
		string color,
		bool canBeEntryStage,
		bool canBeTerminalStage,
		bool supportsFormAttachment,
		bool supportsConditions,
		bool isRuntimeReady,
		string runtimeHandler,
		IReadOnlyList<string>? supportedConfiguration = null,
		bool isSelectable = true,
		bool isDeprecated = false,
		string? replacementGuidance = null) =>
		new(
			type,
			displayName,
			description,
			icon,
			color,
			supportedConfiguration ?? [],
			runtimeHandler,
			GetNextRequiredAction(type),
			canBeEntryStage,
			canBeTerminalStage,
			supportsFormAttachment,
			supportsConditions,
			isRuntimeReady,
			isSelectable,
			isDeprecated,
			replacementGuidance);

	private static string GetNextRequiredAction(AdmissionWorkflowStageType type) => type switch
	{
		AdmissionWorkflowStageType.Inquiry => "Review and qualify the inquiry",
		AdmissionWorkflowStageType.Form => "Complete and submit the required form",
		AdmissionWorkflowStageType.Assessment => "Schedule or record the assessment",
		AdmissionWorkflowStageType.Eligibility => "Complete the eligibility review",
		AdmissionWorkflowStageType.Payment => "Confirm the Finance fee structure",
		AdmissionWorkflowStageType.DocumentVerification => "Verify the required documents",
		AdmissionWorkflowStageType.Review => "Complete the assigned review",
		AdmissionWorkflowStageType.Approval => "Record the admission decision",
		AdmissionWorkflowStageType.Offer => "Issue or respond to the admission offer",
		AdmissionWorkflowStageType.Seat => "Reserve or confirm the seat",
		AdmissionWorkflowStageType.Enrollment => "Complete student enrollment",
		AdmissionWorkflowStageType.Appointment => "Schedule or complete the appointment",
		_ => "Complete the current workflow stage"
	};
}
