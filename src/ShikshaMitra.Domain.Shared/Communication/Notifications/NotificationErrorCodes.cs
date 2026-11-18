namespace ShikshaMitra.Communication.Notifications;

public static class NotificationErrorCodes
{
	public const string EventUnknown = "Communication:Notification:EventUnknown";
	public const string SourceUnavailable = "Communication:Notification:SourceUnavailable";
	public const string PrimaryGuardianNotConfigured = "Communication:Notification:PrimaryGuardianNotConfigured";
	public const string ProviderUnavailable = "Communication:Notification:ProviderUnavailable";
	public const string TemplateInvalid = "Communication:Notification:TemplateInvalid";
	public const string RequiredVariableMissing = "Communication:Notification:RequiredVariableMissing";
	public const string RecipientIneligible = "Communication:Notification:RecipientIneligible";
	public const string ReviewChanged = "Communication:Notification:ReviewChanged";
	public const string ReviewExpired = "Communication:Notification:ReviewExpired";
	public const string UnknownAcceptance = "Communication:Notification:UnknownAcceptance";
}
