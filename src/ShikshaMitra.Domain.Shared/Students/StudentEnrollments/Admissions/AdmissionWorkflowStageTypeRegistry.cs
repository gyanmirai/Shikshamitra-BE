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
		Metadata(AdmissionWorkflowStageType.Inquiry, "Inquiry (legacy)", "Historical workflow stage for inquiry qualification.", "mdi:account-question-outline", "#0EA5E9", true, false, false, true, true, "InquiryStageHandler", ["inquiry"], false, true, "Manage inquiry capture and qualification in Admissions > Inquiries. Start new admission processes with Application Form or another application-stage step."),
		Metadata(AdmissionWorkflowStageType.Form, "Form", "Collect, submit, verify, and correct a published dynamic form.", "mdi:form-select", "#6366F1", true, false, true, true, true, "FormStageHandler", ["formDefinitionId", "form"]),
		Metadata(AdmissionWorkflowStageType.Assessment, "Assessment", "Record an entrance exam, interaction, or other assessment.", "mdi:clipboard-text-outline", "#8B5CF6", false, false, false, true, true, "AssessmentStageHandler", ["assessment"]),
		Metadata(AdmissionWorkflowStageType.Eligibility, "Eligibility", "Evaluate configured admission eligibility criteria.", "mdi:shield-check-outline", "#14B8A6", false, true, false, true, true, "EligibilityStageHandler", ["eligibility"]),
		Metadata(AdmissionWorkflowStageType.Payment, "Payment", "Collect, verify, waive, or reverse an admission payment.", "mdi:cash-check", "#F59E0B", false, false, false, true, true, "PaymentStageHandler", ["payment"]),
		Metadata(AdmissionWorkflowStageType.DocumentVerification, "Document verification", "Review required applicant documents.", "mdi:file-check-outline", "#06B6D4", false, false, false, true, true, "DocumentVerificationStageHandler", ["documentVerification"]),
		Metadata(AdmissionWorkflowStageType.Review, "Review", "Record an assigned review and an explicit admission decision.", "mdi:account-search-outline", "#64748B", false, true, false, true, true, "ReviewStageHandler", ["review"]),
		Metadata(AdmissionWorkflowStageType.Approval, "Admission decision", "Record an authorized approve, waitlist, or reject decision.", "mdi:check-decagram-outline", "#22C55E", false, true, false, true, true, "ApprovalStageHandler"),
		Metadata(AdmissionWorkflowStageType.Offer, "Offer", "Issue, respond to, extend, withdraw, or reissue an admission offer.", "mdi:email-seal-outline", "#10B981", false, true, false, true, true, "OfferStageHandler", ["offer"]),
		Metadata(AdmissionWorkflowStageType.Seat, "Seat (legacy)", "Legacy workflow stage for admission capacity.", "mdi:seat-outline", "#F97316", false, true, false, true, true, "SeatStageHandler", ["seat"], false, true, "This stage is unavailable for new workflows. Existing published workflows continue to run while capacity setup moves to Admission Offering."),
		Metadata(AdmissionWorkflowStageType.Enrollment, "Enrollment (legacy)", "Legacy workflow stage for completing enrollment.", "mdi:account-school-outline", "#16A34A", false, true, false, false, true, "EnrollmentStageHandler", null, false, true, "Use Admission decision and Finalize admission. Student Onboarding owns the post-admission handoff."),
		Metadata(AdmissionWorkflowStageType.Custom, "Custom (legacy)", "Legacy school-specific manual stage.", "mdi:puzzle-outline", "#475569", true, true, false, true, true, "CustomStageHandler", null, false, true, "Replace it with Form, Review, Assessment, Appointment, or Admission decision so requirements and outcomes remain structured."),
		Metadata(AdmissionWorkflowStageType.Appointment, "Appointment", "Schedule and complete an applicant appointment.", "mdi:calendar-clock-outline", "#EC4899", false, false, false, true, false, "AppointmentStageHandler", null, false, false, "Appointment will become selectable after its runtime scheduling and completion flow is available.")
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
		AdmissionWorkflowStageType.Payment => "Collect or verify the required payment",
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
