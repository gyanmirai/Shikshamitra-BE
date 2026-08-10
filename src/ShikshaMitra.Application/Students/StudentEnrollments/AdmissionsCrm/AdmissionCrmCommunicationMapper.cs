using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmCommunicationMapper
{
	public static AdmissionCrmCommunicationProviderDto Provider(AdmissionCrmCommunicationProviderConfiguration value) => new()
	{
		Id = value.Id, AcademicYearId = value.AcademicYearId, Channel = value.Channel, Status = value.Status,
		IsEnabled = value.IsEnabled, ProviderName = value.ProviderName, SenderId = value.SenderId,
		Settings = AdmissionCrmCommunicationProviderSettingsMapper.Sanitize(
			AdmissionCrmCommunicationProviderSettingsMapper.Deserialize(value.SettingsJson)),
		WebhookUrl = value.WebhookUrl, LastValidatedAt = value.LastValidatedAt
	};

	public static AdmissionCrmCommunicationOptOutDto OptOut(AdmissionCrmCommunicationOptOut value) => new()
	{
		Id = value.Id, AcademicYearId = value.AcademicYearId, LeadId = value.LeadId, Channel = value.Channel,
		ContactValue = value.ContactValue, NormalizedContactValue = value.NormalizedContactValue, IsActive = value.IsActive,
		Reason = value.Reason, OptedOutAt = value.OptedOutAt, OptedInAt = value.OptedInAt, OptInReason = value.OptInReason
	};

	public static AdmissionCrmMessageDeliveryLogDto Delivery(AdmissionCrmMessageDeliveryLog value) => new()
	{
		Id = value.Id, AcademicYearId = value.AcademicYearId, LeadId = value.LeadId, TemplateId = value.TemplateId,
		Channel = value.Channel, Recipient = value.Recipient, NormalizedRecipient = value.NormalizedRecipient,
		ProviderName = value.ProviderName, ProviderMessageId = value.ProviderMessageId, SubjectSnapshot = value.SubjectSnapshot,
		BodySnapshot = value.BodySnapshot, Status = value.Status, StatusDetails = value.StatusDetails,
		AttemptCount = value.AttemptCount, CreatedAt = value.CreatedAt, LastAttemptAt = value.LastAttemptAt,
		SentAt = value.SentAt, DeliveredAt = value.DeliveredAt
	};
}
