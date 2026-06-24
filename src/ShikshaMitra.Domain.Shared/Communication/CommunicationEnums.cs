namespace ShikshaMitra.Communication;

public enum CommunicationChannelType
{
	WhatsApp = 0,
	Sms = 1,
	Email = 2,
	InApp = 3
}

public enum CommunicationCampaignStatus
{
	Draft = 0,
	PendingApproval = 1,
	Approved = 2,
	Scheduled = 3,
	Queued = 4,
	Sending = 5,
	Sent = 6,
	PartiallyFailed = 7,
	Failed = 8,
	Cancelled = 9
}

public enum CommunicationImportanceLevel
{
	Normal = 0,
	Urgent = 1,
	Critical = 2
}

public enum CommunicationSourceModule
{
	Manual = 0,
	Student = 1,
	HR = 2,
	Finance = 3,
	Transport = 4,
	Calendar = 5,
	Academic = 6,
	Library = 7,
	Hostel = 8,
	System = 9
}

public enum CommunicationTemplateApprovalStatus
{
	Draft = 0,
	PendingApproval = 1,
	Approved = 2,
	Rejected = 3,
	Retired = 4
}

public enum CommunicationProviderStatus
{
	Disabled = 0,
	Enabled = 1,
	Suspended = 2
}

public enum CommunicationConsentStatus
{
	Unknown = 0,
	OptedIn = 1,
	OptedOut = 2,
	Revoked = 3
}

public enum CommunicationDeliveryStatus
{
	Queued = 0,
	Sent = 1,
	Delivered = 2,
	Read = 3,
	Failed = 4,
	Bounced = 5,
	Replied = 6,
	Acknowledged = 7,
	Skipped = 8
}

public enum CommunicationReplyStatus
{
	Open = 0,
	Assigned = 1,
	Resolved = 2,
	Archived = 3
}
