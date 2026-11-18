using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.Communication.Notifications;

public enum NotificationDueTimeMode
{
	PrecedingSchoolDays,
	FixedOccurrence
}

public sealed record NotificationDueScanRequest(Guid TenantId, DateTimeOffset CycleStartedAt, DateTimeOffset Now,
	string? Continuation, int PageSize);

public sealed record NotificationDueSourceCandidate(string EventKey, Guid SourceId, string SourceRevision,
	NotificationSourceScopeDto Scope, DateTimeOffset OccurrenceAt, DateTimeOffset ExpiresAt, NotificationDueTimeMode TimeMode);

public sealed record NotificationDueSourcePage(IReadOnlyList<NotificationDueSourceCandidate> Candidates, string? Continuation);

public interface INotificationDueSource
{
	string SourceType { get; }

	/// <summary>Reads one keyset page of scoped occurrences. The source adapter must reload their eligibility at capture and dispatch.</summary>
	Task<NotificationDueSourcePage> ReadPageAsync(NotificationDueScanRequest request, CancellationToken cancellationToken);
}
