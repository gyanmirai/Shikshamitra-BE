namespace ShikshaMitra.Communication.Notifications;

public sealed record NotificationScanResult(int Examined, int Created, int Skipped, bool HasMore);
