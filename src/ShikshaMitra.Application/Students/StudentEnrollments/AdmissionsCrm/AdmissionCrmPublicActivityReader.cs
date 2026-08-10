using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPublicActivityReader(
	IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository,
	IRepository<AdmissionCrmMessageDeliveryLog, Guid> deliveryLogRepository) : ApplicationService, ITransientDependency
{
	public async Task<List<AdmissionApplicantStatusActivityDto>> GetAsync(AdmissionInquiryLead lead)
	{
		var timelineEvents = await AsyncExecuter.ToListAsync((await timelineRepository.GetQueryableAsync()).Where(item =>
			item.TenantId == CurrentTenant.Id && item.AcademicYearId == lead.AcademicYearId && item.LeadId == lead.Id));
		var deliveryLogs = await AsyncExecuter.ToListAsync((await deliveryLogRepository.GetQueryableAsync()).Where(item =>
			item.TenantId == CurrentTenant.Id && item.AcademicYearId == lead.AcademicYearId && item.LeadId == lead.Id));
		return timelineEvents.Where(IsVisible).Select(ToTimelineActivity)
			.Concat(deliveryLogs.Select(ToMessageActivity))
			.OrderByDescending(item => item.OccurredAt)
			.Take(10)
			.ToList();
	}

	private static bool IsVisible(AdmissionCrmTimelineEvent item) => item.EventType is
		AdmissionCrmTimelineEventType.LeadCreated or
		AdmissionCrmTimelineEventType.PublicFormSubmitted or
		AdmissionCrmTimelineEventType.ExamScheduled or
		AdmissionCrmTimelineEventType.ExamAttendanceMarked or
		AdmissionCrmTimelineEventType.ExamResultRecorded or
		AdmissionCrmTimelineEventType.EligibilityDecided or
		AdmissionCrmTimelineEventType.ApplicantChecklistUpdated or
		AdmissionCrmTimelineEventType.OfferIssued or
		AdmissionCrmTimelineEventType.OfferAccepted or
		AdmissionCrmTimelineEventType.OfferDeclined or
		AdmissionCrmTimelineEventType.AdmissionRouteEvaluated or
		AdmissionCrmTimelineEventType.ActivationRequested or
		AdmissionCrmTimelineEventType.StudentActivated;

	private static AdmissionApplicantStatusActivityDto ToTimelineActivity(AdmissionCrmTimelineEvent item) => item.EventType switch
	{
		AdmissionCrmTimelineEventType.PublicFormSubmitted => Activity("Inquiry", "Inquiry submitted", "Your admission inquiry was submitted successfully.", item.OccurredAt),
		AdmissionCrmTimelineEventType.LeadCreated => Activity("Inquiry", "Inquiry received", "The admissions team has recorded your inquiry.", item.OccurredAt),
		AdmissionCrmTimelineEventType.ExamScheduled => Activity("EntranceExam", "Entrance exam scheduled", "Entrance exam schedule details are available on this page.", item.OccurredAt),
		AdmissionCrmTimelineEventType.ExamAttendanceMarked => Activity("EntranceExam", "Entrance exam attendance updated", "The school has recorded entrance exam attendance.", item.OccurredAt),
		AdmissionCrmTimelineEventType.ExamResultRecorded => Activity("EntranceExam", "Entrance exam result updated", "Entrance exam result details are available on this page.", item.OccurredAt),
		AdmissionCrmTimelineEventType.ApplicantChecklistUpdated => Activity("Checklist", "Applicant checklist updated", "The school has updated an applicant checklist item.", item.OccurredAt),
		AdmissionCrmTimelineEventType.OfferIssued => Activity("Offer", "Admission offer issued", "An admission offer is available for review.", item.OccurredAt),
		AdmissionCrmTimelineEventType.OfferAccepted => Activity("Offer", "Admission offer accepted", "Your offer acceptance has been recorded.", item.OccurredAt),
		AdmissionCrmTimelineEventType.OfferDeclined => Activity("Offer", "Admission offer declined", "Your offer response has been recorded.", item.OccurredAt),
		AdmissionCrmTimelineEventType.AdmissionRouteEvaluated => Activity("AdmissionRoute", "Admission route reviewed", "The school has reviewed the applicable admission route.", item.OccurredAt),
		AdmissionCrmTimelineEventType.ActivationRequested => Activity("Activation", "Student activation in progress", "Final academic placement and access setup are in progress.", item.OccurredAt),
		AdmissionCrmTimelineEventType.StudentActivated => Activity("Activation", "Student ready to join", "Academic placement and required access setup are complete.", item.OccurredAt),
		_ => Activity("Eligibility", "Eligibility decision updated", "The admissions team has updated the eligibility decision.", item.OccurredAt)
	};

	private static AdmissionApplicantStatusActivityDto Activity(
		string type, string title, string details, DateTimeOffset occurredAt) => new()
	{
		Type = type,
		Title = title,
		Details = details,
		OccurredAt = occurredAt
	};

	private static AdmissionApplicantStatusActivityDto ToMessageActivity(AdmissionCrmMessageDeliveryLog log) => new()
	{
		Type = "Message",
		Title = $"{log.Channel} message {GetMessageStatusLabel(log.Status)}",
		Details = GetMessageStatusDetails(log.Status),
		OccurredAt = ToDateTimeOffset(log.DeliveredAt ?? log.SentAt ?? log.LastAttemptAt ?? log.CreatedAt)
	};

	private static string GetMessageStatusLabel(string status) => status switch
	{
		AdmissionCrmMessageDeliveryLog.DeliveredStatus => "delivered",
		AdmissionCrmMessageDeliveryLog.SentStatus => "sent",
		AdmissionCrmMessageDeliveryLog.QueuedStatus => "queued",
		AdmissionCrmMessageDeliveryLog.FailedStatus => "failed",
		AdmissionCrmMessageDeliveryLog.BlockedByOptOutStatus => "not sent",
		AdmissionCrmMessageDeliveryLog.ProviderNotConfiguredStatus => "recorded",
		_ => "updated"
	};

	private static string GetMessageStatusDetails(string status) => status switch
	{
		AdmissionCrmMessageDeliveryLog.DeliveredStatus => "The school message was delivered.",
		AdmissionCrmMessageDeliveryLog.SentStatus => "The school message was sent.",
		AdmissionCrmMessageDeliveryLog.QueuedStatus => "The school message is queued for sending.",
		AdmissionCrmMessageDeliveryLog.FailedStatus => "The school message could not be delivered.",
		AdmissionCrmMessageDeliveryLog.BlockedByOptOutStatus => "The message was not sent because this contact is opted out.",
		AdmissionCrmMessageDeliveryLog.ProviderNotConfiguredStatus => "The school recorded a message update.",
		_ => "The school message status changed."
	};

	private static DateTimeOffset ToDateTimeOffset(DateTime value) => value.Kind switch
	{
		DateTimeKind.Utc => new DateTimeOffset(value),
		DateTimeKind.Local => new DateTimeOffset(value).ToUniversalTime(),
		_ => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc))
	};
}
