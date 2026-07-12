using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmMessageDeliveryLog : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public const string ProviderNotConfiguredStatus = "ProviderNotConfigured";
	public const string BlockedByOptOutStatus = "BlockedByOptOut";
	public const string QueuedStatus = "Queued";
	public const string SentStatus = "Sent";
	public const string DeliveredStatus = "Delivered";
	public const string FailedStatus = "Failed";

	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid? LeadId { get; private set; }
	public Guid? TemplateId { get; private set; }
	public AdmissionCrmMessageChannel Channel { get; private set; }
	public string Recipient { get; private set; } = string.Empty;
	public string NormalizedRecipient { get; private set; } = string.Empty;
	public string? ProviderName { get; private set; }
	public string? ProviderMessageId { get; private set; }
	public string? SubjectSnapshot { get; private set; }
	public string BodySnapshot { get; private set; } = string.Empty;
	public string Status { get; private set; } = ProviderNotConfiguredStatus;
	public string StatusDetails { get; private set; } = string.Empty;
	public int AttemptCount { get; private set; }
	public DateTime CreatedAt { get; private set; }
	public DateTime? LastAttemptAt { get; private set; }
	public DateTime? SentAt { get; private set; }
	public DateTime? DeliveredAt { get; private set; }

	protected AdmissionCrmMessageDeliveryLog()
	{
	}

	public AdmissionCrmMessageDeliveryLog(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		Guid? leadId,
		Guid? templateId,
		AdmissionCrmMessageChannel channel,
		string recipient,
		string? providerName,
		string? subjectSnapshot,
		string bodySnapshot,
		string status,
		string statusDetails,
		DateTime createdAt)
		: base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		LeadId = leadId;
		TemplateId = templateId;
		Channel = channel;
		Recipient = Check.NotNullOrWhiteSpace(
			recipient,
			nameof(recipient),
			AdmissionCrmConsts.MaxEmailLength);
		NormalizedRecipient = AdmissionCrmCommunicationOptOut.NormalizeContact(channel, recipient);
		ProviderName = NormalizeOptional(
			providerName,
			nameof(providerName),
			AdmissionCrmConsts.MaxNameLength);
		SubjectSnapshot = NormalizeOptional(
			subjectSnapshot,
			nameof(subjectSnapshot),
			AdmissionCrmConsts.MaxMessageSubjectLength);
		BodySnapshot = Check.NotNullOrWhiteSpace(
			bodySnapshot,
			nameof(bodySnapshot),
			AdmissionCrmConsts.MaxMessageBodyLength);
		CreatedAt = createdAt;
		SetStatus(status, statusDetails, createdAt);
	}

	public void QueueRetry(string statusDetails, DateTime queuedAt)
	{
		SetStatus(QueuedStatus, statusDetails, queuedAt);
	}

	public void ApplyDispatchResult(string status, string statusDetails, DateTime changedAt)
	{
		ApplyDispatchResult(status, statusDetails, changedAt, providerMessageId: null);
	}

	public void ApplyDispatchResult(
		string status,
		string statusDetails,
		DateTime changedAt,
		string? providerMessageId)
	{
		RecordProviderMessageId(providerMessageId);
		SetStatus(status, statusDetails, changedAt, countAttempt: true);
	}

	public void RecordProviderMessageId(string? providerMessageId)
	{
		var normalizedProviderMessageId = NormalizeOptional(
			providerMessageId,
			nameof(providerMessageId),
			AdmissionCrmConsts.MaxProviderMessageIdLength);
		if (!string.IsNullOrWhiteSpace(normalizedProviderMessageId) && ProviderMessageId == null)
		{
			ProviderMessageId = normalizedProviderMessageId;
		}
	}

	public bool ApplyProviderCallback(
		string status,
		string statusDetails,
		string? providerMessageId,
		DateTime changedAt)
	{
		RecordProviderMessageId(providerMessageId);

		if (GetDeliveryStatusRank(status) < GetDeliveryStatusRank(Status))
		{
			return false;
		}

		SetStatus(status, statusDetails, changedAt, countAttempt: false);
		return true;
	}

	private void SetStatus(
		string status,
		string statusDetails,
		DateTime changedAt,
		bool countAttempt = true)
	{
		Status = status;
		StatusDetails = Check.NotNullOrWhiteSpace(
			statusDetails,
			nameof(statusDetails),
			AdmissionCrmConsts.MaxNotesLength);

		if (status == QueuedStatus ||
		    status == SentStatus ||
		    status == FailedStatus)
		{
			if (countAttempt)
			{
				AttemptCount += 1;
			}

			LastAttemptAt = changedAt;
		}

		if (status == SentStatus)
		{
			SentAt ??= changedAt;
		}

		if (status == DeliveredStatus)
		{
			DeliveredAt ??= changedAt;
		}
	}

	private static int GetDeliveryStatusRank(string status)
	{
		return status switch
		{
			QueuedStatus => 10,
			SentStatus => 20,
			FailedStatus => 30,
			DeliveredStatus => 40,
			_ => 0
		};
	}

	private static string? NormalizeOptional(string? value, string name, int maxLength)
	{
		return string.IsNullOrWhiteSpace(value)
			? null
			: Check.Length(value.Trim(), name, maxLength);
	}
}
