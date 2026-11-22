using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Notifications;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Communication.Notifications;

[RemoteService(false), DisableAuditing, ExposeServices(typeof(INotificationDueScheduler))]
public sealed class NotificationDueScheduler(ICurrentTenant tenant, IUnitOfWorkManager units, INotificationPersonalWriteLock writeLock,
	IRepository<NotificationScheduleCursor, Guid> cursors, IAsyncQueryableExecuter executer, IGuidGenerator guids,
	IEnumerable<INotificationDueSource> sources, IEnumerable<INotificationEventDefinitionProvider> definitions, NotificationEffectiveRuleResolver rules,
	NotificationRuleConfigurationCodec configuration, NotificationSchoolTimeZone schoolZone, AcademicNotificationWorkingDays calendar,
	NotificationReminderTimingPolicy timing, INotificationIntentWriter writer) : INotificationDueScheduler, ITransientDependency
{
	private sealed record CursorState(int SchemaVersion, DateTimeOffset? CycleStartedAt, string? Key);
	private sealed record ScanRule(Guid Id, int Revision, NotificationRuleTimingConfiguration Timing);
	private const int MaximumContinuationLength = 2048;

	public async Task<NotificationScanResult> ScanAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (tenant.Id is not Guid tenantId || tenantId == Guid.Empty) throw new BusinessException(NotificationErrorCodes.TenantRequired);
		if (units.Current != null) throw new BusinessException(NotificationErrorCodes.StateConflict);
		if (batchSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(batchSize));
		var adapters = sources.Select(source =>
		{
			var scanKey = source.ScanKey;
			return (Source: source, Key: string.IsNullOrEmpty(scanKey) ? source.SourceType : scanKey);
		}).ToArray();
		if (adapters.Length == 0) return new(0, 0, 0, false);
		if (adapters.Any(x => string.IsNullOrWhiteSpace(x.Source.SourceType) || x.Source.SourceType.Length > 80 ||
			string.IsNullOrWhiteSpace(x.Key) || x.Key.Length > 80) ||
			adapters.Select(x => x.Key).Distinct(StringComparer.Ordinal).Count() != adapters.Length)
			throw new BusinessException(NotificationErrorCodes.StateConflict);

		now = now.ToUniversalTime();
		using var unit = units.Begin(requiresNew: true, isTransactional: true, isolationLevel: IsolationLevel.ReadCommitted);
		await writeLock.AcquireAsync(["notification-due-scan"], cancellationToken);
		var keys = adapters.Select(x => SchedulerKey(x.Key)).ToArray();
		var current = await executer.ToListAsync((await cursors.GetQueryableAsync()).Where(x => x.TenantId == tenantId &&
			Enumerable.Contains(keys, x.SchedulerKey)), cancellationToken);
		var bySource = current.ToDictionary(x => x.SchedulerKey, StringComparer.Ordinal);
		var available = adapters.Select(x => (x.Source, Key: SchedulerKey(x.Key), Cursor: bySource.GetValueOrDefault(SchedulerKey(x.Key))))
			.Where(x => x.Cursor == null || x.Cursor.NextScanAt <= now)
			.OrderBy(x => x.Cursor?.NextScanAt).ThenBy(x => x.Key, StringComparer.Ordinal).Take(2).ToArray();
		if (available.Length == 0)
		{
			await unit.CompleteAsync(cancellationToken);
			return new(0, 0, 0, false);
		}

		var (source, key, cursor) = available[0];
		var previous = ReadContinuation(cursor, now);
		var startedAt = previous?.CycleStartedAt ?? now;
		var page = await source.ReadPageAsync(new(tenantId, startedAt, now, previous?.Key, batchSize), cancellationToken);
		if (page?.Candidates == null || page.Candidates.Count > batchSize ||
			(page.Continuation != null && (!ValidContinuation(page.Continuation) || page.Continuation == previous?.Key)))
			throw new BusinessException(NotificationErrorCodes.StateConflict);

		var created = await CapturePageAsync(source, page.Candidates, now, cancellationToken);
		var hasNextPage = page.Continuation != null;
		var snapshot = NotificationRuntimeJson.Serialize(new CursorState(1, hasNextPage ? startedAt : null, page.Continuation),
			NotificationErrorCodes.StateConflict);
		var nextScan = now.AddSeconds(hasNextPage ? 30 : 60);
		if (cursor == null)
		{
			cursor = new NotificationScheduleCursor(guids.Create(), tenantId, key, now);
			cursor.Advance(snapshot, nextScan);
			await cursors.InsertAsync(cursor, cancellationToken: cancellationToken);
		}
		else
		{
			cursor.Advance(snapshot, nextScan);
			await cursors.UpdateAsync(cursor, cancellationToken: cancellationToken);
		}
		await unit.CompleteAsync(cancellationToken);
		return new(page.Candidates.Count, created, page.Candidates.Count - created, hasNextPage || available.Length > 1);
	}

	private async Task<int> CapturePageAsync(INotificationDueSource source, IReadOnlyList<NotificationDueSourceCandidate> candidates,
		DateTimeOffset now, CancellationToken cancellationToken)
	{
		var events = source.ScheduledEventKeys ?? [];
		var dueDefinitions = definitions.SelectMany(x => x.GetAll()).Where(x => x.SourceType == source.SourceType &&
			x.TriggerType == NotificationTriggerType.Due && (events.Count == 0 || events.Contains(x.EventKey)))
			.ToDictionary(x => x.EventKey, StringComparer.Ordinal);
		var resolved = new Dictionary<(string, NotificationRuleScope), ScanRule?>();
		var workingDays = new Dictionary<(NotificationRuleScope, DateOnly), NotificationWorkingDays?>();
		Task<TimeZoneInfo>? schoolTimeZone = null;
		var created = 0;
		foreach (var candidate in candidates)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (candidate == null || candidate.ExpiresAt <= now || candidate.OccurrenceAt == default || candidate.SourceId == Guid.Empty ||
				string.IsNullOrWhiteSpace(candidate.SourceRevision) || candidate.SourceRevision.Length > 128 ||
				candidate.EventKey == null || candidate.Scope == null || !Enum.IsDefined(candidate.TimeMode) ||
				!dueDefinitions.TryGetValue(candidate.EventKey, out var definition)) continue;
			if (candidate.Phase.HasValue != candidate.ScheduledAt.HasValue ||
				(candidate.Phase.HasValue && (candidate.Phase != NotificationDuePhase.ReviewerEscalation ||
					candidate.EventKey != AcademicNotificationEventKeys.SubstitutionAcknowledgementDue ||
					candidate.TimeMode != NotificationDueTimeMode.FixedOccurrence))) continue;
			var inputScope = candidate.Scope;
			NotificationRuleScope scope;
			try { scope = NotificationRuleScope.Create(inputScope.AcademicYearId, inputScope.BoardId, inputScope.MediumId, inputScope.ClassId, inputScope.DivisionId); }
			catch (ArgumentException) { continue; }
			var key = (definition.EventKey, scope);
			if (!resolved.TryGetValue(key, out var rule))
				resolved[key] = rule = await ResolveRuleAsync(definition, scope, cancellationToken);
			if (rule == null) continue;

			DateTimeOffset? dueAt;
			if (candidate.TimeMode == NotificationDueTimeMode.FixedOccurrence)
				dueAt = timing.ResolveFixed(candidate.ScheduledAt ?? candidate.OccurrenceAt, candidate.ExpiresAt);
			else
			{
				schoolTimeZone ??= schoolZone.GetAsync(cancellationToken);
				var zone = await schoolTimeZone;
				var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(candidate.OccurrenceAt, zone).DateTime);
				var calendarKey = (scope, date);
				if (!workingDays.TryGetValue(calendarKey, out var days))
					workingDays[calendarKey] = days = await calendar.ReadAsync(scope, date, cancellationToken);
				dueAt = days == null ? null : timing.ResolveSchoolDay(candidate.OccurrenceAt, candidate.ExpiresAt, zone, rule.Timing, days);
			}
			if (!dueAt.HasValue || dueAt > now) continue;
			try
			{
				var result = await writer.WriteWithOutcomeAsync(new(candidate.EventKey, source.SourceType, candidate.SourceId,
					candidate.SourceRevision, now, [], DueAt: dueAt, ExpiresAt: candidate.ExpiresAt,
					DueOccurrence: new(inputScope, candidate.OccurrenceAt, rule.Id, rule.Revision) { Phase = candidate.Phase }), cancellationToken);
				if (result.Created || result.AddedRecipients) created++;
			}
			catch (BusinessException error) when (error.Code == NotificationErrorCodes.SourceUnavailable)
			{
				// A published source can change after enumeration and before its eligibility is checked.
			}
		}
		return created;
	}

	private async Task<ScanRule?> ResolveRuleAsync(NotificationEventDefinition definition, NotificationRuleScope scope, CancellationToken cancellationToken)
	{
		var rule = await rules.ResolveAsync(definition.EventKey, scope, cancellationToken);
		if (rule is not { IsEnabled: true }) return null;
		try { return new(rule.Id, rule.Revision, configuration.Deserialize(rule.ConfigurationJson, definition).Timing); }
		catch (BusinessException error) when (error.Code == NotificationErrorCodes.RuleConfigurationInvalid) { return null; }
	}

	private static CursorState? ReadContinuation(NotificationScheduleCursor? cursor, DateTimeOffset now)
	{
		if (cursor == null || cursor.CursorJson == "{}") return null;
		var next = NotificationRuntimeJson.Deserialize<CursorState>(cursor.CursorJson, NotificationErrorCodes.StateConflict);
		if (next.SchemaVersion != 1) throw new BusinessException(NotificationErrorCodes.StateConflict);
		if (next.Key == null && next.CycleStartedAt == null) return null;
		if (!ValidContinuation(next.Key) || !next.CycleStartedAt.HasValue || next.CycleStartedAt.Value == default || next.CycleStartedAt > now)
			throw new BusinessException(NotificationErrorCodes.StateConflict);
		return next;
	}

	private static bool ValidContinuation(string? key) =>
		!string.IsNullOrWhiteSpace(key) && key.Length <= MaximumContinuationLength && !key.Any(char.IsControl);

	private static string SchedulerKey(string sourceType) => "due:" + sourceType;
}
