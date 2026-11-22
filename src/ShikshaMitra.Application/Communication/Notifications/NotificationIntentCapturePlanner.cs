using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Communication.Notifications;

public sealed record NotificationIntentCaptureItem(Guid RuleId, int RuleRevision, NotificationRuleScope Scope,
	IReadOnlyList<NotificationRecipientRefDto> Recipients, string SnapshotJson, IReadOnlyList<Guid> TemplateRevisionIds,
	DateTimeOffset DueAt, DateTimeOffset? ExpiresAt, NotificationIntentState? CaptureOutcome = null, string? ErrorCode = null);

[RemoteService(false)]
public sealed class NotificationIntentCapturePlanner(ICurrentTenant tenant, IEnumerable<INotificationEventDefinitionProvider> definitions,
	IEnumerable<INotificationSourceAdapter> sources, NotificationSourcePartitioner partitions, NotificationEffectiveRuleResolver rules,
	NotificationRuleConfigurationCodec configuration, NotificationIntentSnapshotCodec snapshots, NotificationTemplateSnapshotSelector templates,
	NotificationSchoolTimeZone schoolZone, NotificationDeliveryTimingPolicy timing) : ITransientDependency
{
	private sealed record ConfigurationSnapshot(NotificationRuleRuntimeConfiguration? Value, string? ErrorCode);
	public NotificationEventDefinition Validate(NotificationEventInput input)
	{
		if (input == null || input.SourceId == Guid.Empty || input.ActorUserId == Guid.Empty || input.AffectedRecipients == null ||
			string.IsNullOrWhiteSpace(input.SourceRevision) || input.SourceRevision.Length > 128 ||
			string.IsNullOrWhiteSpace(input.EventKey) || input.EventKey.Length > 160 || string.IsNullOrWhiteSpace(input.SourceType) || input.SourceType.Length > 80)
			throw new BusinessException(NotificationErrorCodes.SourceUnavailable);
		var matches = definitions.SelectMany(x => x.GetAll()).Where(x => x.EventKey == input.EventKey).ToArray();
		if (matches.Length != 1) throw new BusinessException(NotificationErrorCodes.EventUnknown);
		var definition = matches[0];
		if (definition.TriggerType == NotificationTriggerType.Manual) throw new BusinessException(NotificationErrorCodes.UseWorkflow);
		if (definition.SourceType != input.SourceType || definition.TriggerType == NotificationTriggerType.Due && !input.DueAt.HasValue ||
			sources.Count(x => x.EventKeys.Contains(input.EventKey)) != 1) throw new BusinessException(NotificationErrorCodes.SourceUnavailable);
		if (input.DueOccurrence is { } occurrence)
		{
			if (definition.TriggerType != NotificationTriggerType.Due || occurrence.Scope == null ||
				occurrence.RuleId == Guid.Empty || occurrence.RuleRevision < 1 || occurrence.OccurrenceAt == default)
				throw new BusinessException(NotificationErrorCodes.SourceUnavailable);
			if (occurrence.Phase.HasValue && (occurrence.Phase != NotificationDuePhase.ReviewerEscalation ||
				input.EventKey != AcademicNotificationEventKeys.SubstitutionAcknowledgementDue || input.ActorUserId.HasValue ||
				input.ExpiresAt != occurrence.OccurrenceAt || input.DueAt >= input.ExpiresAt))
				throw new BusinessException(NotificationErrorCodes.SourceUnavailable);
			var scope = occurrence.Scope;
			try { _ = NotificationRuleScope.Create(scope.AcademicYearId, scope.BoardId, scope.MediumId, scope.ClassId, scope.DivisionId); }
			catch (ArgumentException) { throw new BusinessException(NotificationErrorCodes.SourceUnavailable); }
		}
		return definition;
	}
	public async Task<IReadOnlyList<NotificationIntentCaptureItem>> PlanAsync(NotificationEventInput input, NotificationEventDefinition definition,
		CancellationToken cancellationToken, IReadOnlySet<Guid>? capturedStudents = null)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (tenant.Id is not Guid tenantId || tenantId == Guid.Empty) throw new BusinessException(NotificationErrorCodes.TenantRequired);
		var occurrence = input.DueOccurrence;
		var context = new NotificationIntentContext(Guid.Empty, tenantId, input.EventKey, input.SourceType, input.SourceId,
			input.SourceRevision, input.OccurredAt, input.AffectedRecipients, occurrence?.Scope, occurrence?.OccurrenceAt) { DuePhase = occurrence?.Phase };
		var source = await sources.Single(x => x.EventKeys.Contains(input.EventKey)).LoadAsync(context, cancellationToken);
		var scoped = partitions.Partition(context, definition, source);
		var effectiveDefinition = NotificationSourceDeliveryPolicy.Restrict(definition, source);
		var expiry = input.ExpiresAt.HasValue && source.ExpiresAt.HasValue
			? (input.ExpiresAt.Value < source.ExpiresAt.Value ? input.ExpiresAt : source.ExpiresAt) : input.ExpiresAt ?? source.ExpiresAt;
		var resolved = new Dictionary<string, NotificationRule?>(StringComparer.Ordinal);
		var configured = new Dictionary<(Guid Id, int Revision), ConfigurationSnapshot>();
		var pinned = new Dictionary<(Guid Id, int Revision), IReadOnlyList<Guid>>();
		var pinErrors = new Dictionary<(Guid Id, int Revision), string>();
		Task<TimeZoneInfo>? zone = null;
		var results = new List<NotificationIntentCaptureItem>();
		foreach (var partition in scoped)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var scope = new NotificationSourceScopeDto(partition.Scope.AcademicYearId, partition.Scope.BoardId, partition.Scope.MediumId,
				partition.Scope.ClassId, partition.Scope.DivisionId);
			if (occurrence != null && occurrence.Scope != scope) continue;
			var recipients = capturedStudents == null ? partition.Recipients : partition.Recipients
				.Where(x => x.Kind == "Student" && x.StudentId is Guid studentId && !capturedStudents.Contains(studentId)).ToArray();
			if (recipients.Count == 0) continue;
			if (!resolved.TryGetValue(partition.Scope.Key, out var rule))
			{
				rule = await rules.ResolveAsync(input.EventKey, partition.Scope, cancellationToken); resolved[partition.Scope.Key] = rule;
			}
			if (rule is not { IsEnabled: true }) continue;
			if (occurrence != null && (rule.Id != occurrence.RuleId || rule.Revision != occurrence.RuleRevision)) continue;
			var key = (rule.Id, rule.Revision);
			if (!configured.TryGetValue(key, out var config))
			{
				try { config = new(NotificationSourceDeliveryPolicy.Restrict(configuration.Deserialize(rule.ConfigurationJson, definition),
					effectiveDefinition, source), null); }
				catch (BusinessException error) when (error.Code == NotificationErrorCodes.RuleConfigurationInvalid) { config = new(null, error.Code); }
				configured[key] = config;
			}
			if (config.Value == null)
			{
				results.Add(new(rule.Id, rule.Revision, partition.Scope, recipients, "{}", [], input.DueAt ?? input.OccurredAt,
					expiry, NotificationIntentState.NeedsReview, config.ErrorCode ?? NotificationErrorCodes.RuleConfigurationInvalid)); continue;
			}
			var selected = recipients.Where(x => NotificationSourceDeliveryPolicy.Includes(x, config.Value, effectiveDefinition)).ToArray();
			if (selected.Length == 0) continue;
			var snapshot = "{}"; var due = input.DueAt ?? input.OccurredAt;
			try
			{
				snapshot = snapshots.Serialize(new(1, scope, config.Value, occurrence?.OccurrenceAt.ToUniversalTime())
					{ DuePhase = occurrence?.Phase }, definition);
				if (occurrence?.Phase == null) due = due.AddMinutes(config.Value.Timing.DelayMinutes);
				if (capturedStudents != null && due < input.OccurredAt) due = input.OccurredAt;
				zone ??= schoolZone.GetAsync(cancellationToken);
				var decision = timing.Evaluate(due, await zone, config.Value.Timing.QuietHoursEnabled, config.Value.Timing.QuietStart,
					config.Value.Timing.QuietEnd, expiry, config.Value.Timing.BypassQuietHours);
				if (decision.IsExpired)
				{
					results.Add(new(rule.Id, rule.Revision, partition.Scope, selected, snapshot, [], due, expiry, NotificationIntentState.Expired, "SourceExpired")); continue;
				}
				due = decision.DueAt!.Value;
				if (pinErrors.TryGetValue(key, out var pinError)) throw new BusinessException(pinError);
				if (!pinned.TryGetValue(key, out var versions))
				{
					try { versions = await templates.PinAsync(config.Value, effectiveDefinition, cancellationToken); pinned[key] = versions; }
					catch (BusinessException error) when (error.Code == NotificationErrorCodes.TemplateInvalid) { pinErrors[key] = error.Code; throw; }
				}
				results.Add(new(rule.Id, rule.Revision, partition.Scope, selected, snapshot, versions, due, expiry));
			}
			catch (BusinessException error) when (error.Code is NotificationErrorCodes.TemplateInvalid or NotificationErrorCodes.RuleConfigurationInvalid or NotificationErrorCodes.RuntimeSnapshotInvalid)
			{
				results.Add(new(rule.Id, rule.Revision, partition.Scope, selected, snapshot, [], due, expiry, NotificationIntentState.NeedsReview, error.Code));
			}
			catch (ArgumentOutOfRangeException)
			{
				results.Add(new(rule.Id, rule.Revision, partition.Scope, selected, snapshot, [], input.DueAt ?? input.OccurredAt,
					expiry, NotificationIntentState.NeedsReview, NotificationErrorCodes.RuleConfigurationInvalid));
			}
		}
		return results;
	}
}
