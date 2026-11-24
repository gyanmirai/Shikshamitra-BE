namespace ShikshaMitra.Communication.Notifications;

public static class NotificationErrorCodes
{
	public const string EventUnknown = "Communication:Notification:EventUnknown";
	public const string SourceUnavailable = "Communication:Notification:SourceUnavailable";
	public const string PrimaryGuardianNotConfigured = "Communication:Notification:PrimaryGuardianNotConfigured";
	public const string ProviderUnavailable = "Communication:Notification:ProviderUnavailable";
	public const string ProviderQuotaExhausted = "Communication:Notification:ProviderQuotaExhausted";
	public const string TemplateInvalid = "Communication:Notification:TemplateInvalid";
	public const string RequiredVariableMissing = "Communication:Notification:RequiredVariableMissing";
	public const string RecipientIneligible = "Communication:Notification:RecipientIneligible";
	public const string ReviewChanged = "Communication:Notification:ReviewChanged";
	public const string ReviewExpired = "Communication:Notification:ReviewExpired";
	public const string UnknownAcceptance = "Communication:Notification:UnknownAcceptance";
	public const string TemplateImmutable = "Communication:Notification:TemplateImmutable";
	public const string TemplateTestRateLimited = "Communication:Notification:TemplateTestRateLimited";
	public const string TemplateTestAccountUnavailable = "Communication:Notification:TemplateTestAccountUnavailable";
	public const string WebPushTestDeviceSelectionRequired = "WebPushTestDeviceSelectionRequired";
	public const string WebPushSubscriptionUnavailable = "WebPushSubscriptionUnavailable";
	public const string ReviewConsumed = "Communication:Notification:ReviewConsumed";
	public const string ReviewInvalid = "Communication:Notification:ReviewInvalid";
	public const string ApprovalSetupRequired = "Communication:Notification:ApprovalSetupRequired";
	public const string HistoryInvalid = "Communication:Notification:HistoryInvalid";
	public const string RetryRequiresReview = "Communication:Notification:RetryRequiresReview";
	public const string CancelledBeforeDelivery = "Communication:Notification:CancelledBeforeDelivery";
	public const string StaffRetryRequested = "Communication:Notification:StaffRetryRequested";
	public const string TenantRequired = "Communication:Notification:TenantRequired";
	public const string UseWorkflow = "Communication:Notification:UseNotificationWorkflow";
	public const string PushEndpointInvalid = "Communication:Notification:PushEndpointInvalid";
	public const string PushKeyChanged = "Communication:Notification:PushKeyChanged";
	public const string PushSubscriptionInvalid = "Communication:Notification:PushSubscriptionInvalid";
	public const string RuntimeSnapshotInvalid = "Communication:Notification:RuntimeSnapshotInvalid";
	public const string StateConflict = "Communication:Notification:StateConflict";
	public const string LeaseLost = "Communication:Notification:LeaseLost";
	public const string RuleConfigurationInvalid = "Communication:Notification:RuleConfigurationInvalid";
}
