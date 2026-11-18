namespace ShikshaMitra.Communication.Notifications;

public enum NotificationIntentState
{
	Scheduled, Ready, Processing, Completed, PartiallyFailed, Cancelled, Expired, NeedsReview, AwaitingDelivery
}

public enum NotificationIntentOrigin { Automatic, ReviewedManual }

public enum NotificationDispatchState
{
	Pending, Processing, ProviderAccepted, Delivered, Read, Failed, Skipped, UnknownAcceptance
}

public enum NotificationProviderTemplateState
{
	Draft, PendingApproval, Approved, Rejected, Paused, Disabled
}
