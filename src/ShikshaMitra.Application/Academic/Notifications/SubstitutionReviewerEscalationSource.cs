using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), ExposeServices(typeof(INotificationDueSource))]
public sealed class SubstitutionReviewerEscalationSource(SubstitutionNotificationSource duties,
	SubstitutionReviewerEscalationPolicy policy) : INotificationDueSource, ITransientDependency
{
	public string SourceType => SubstitutionNotificationSource.Type;
	public string ScanKey => SourceType + ".ReviewerEscalation";
	public IReadOnlyCollection<string> ScheduledEventKeys { get; } = [SubstitutionNotificationSource.AcknowledgementDue];

	public async Task<NotificationDueSourcePage> ReadPageAsync(NotificationDueScanRequest request, CancellationToken cancellationToken)
	{
		var (rows, continuation) = await duties.ReadOpenDutiesAsync(request, cancellationToken);
		var delays = new Dictionary<NotificationSourceScopeDto, int?>();
		var candidates = new List<NotificationDueSourceCandidate>();
		foreach (var match in rows)
		{
			var end = SubstitutionNotificationSource.PeriodEnd(match.Duty);
			if (request.Now >= end) continue;
			var scope = match.Scope;
			if (!delays.TryGetValue(scope, out var delay))
				delays[scope] = delay = await policy.GetDelayAsync(scope, cancellationToken);
			if (!delay.HasValue) continue;
			var substitute = await duties.FindSubstituteAsync(request.TenantId, match.Duty, cancellationToken);
			if (substitute == null) continue;
			var acceptedAt = await policy.FindReminderAcceptanceAsync(request.TenantId, match, substitute, cancellationToken);
			var readyAt = acceptedAt?.AddMinutes(delay.Value);
			if (!readyAt.HasValue || readyAt > request.Now) continue;
			candidates.Add(new(SubstitutionNotificationSource.AcknowledgementDue, match.Duty.Id,
				SubstitutionNotificationSource.Revision(match.Duty), scope, end, end, NotificationDueTimeMode.FixedOccurrence)
			{
				Phase = NotificationDuePhase.ReviewerEscalation, ScheduledAt = readyAt
			});
		}
		return new(candidates, continuation);
	}
}
