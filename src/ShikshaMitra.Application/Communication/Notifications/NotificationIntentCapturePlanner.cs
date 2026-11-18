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
		return definition;
	}
	public async Task<IReadOnlyList<NotificationIntentCaptureItem>> PlanAsync(NotificationEventInput input, NotificationEventDefinition definition,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (tenant.Id is not Guid tenantId || tenantId == Guid.Empty) throw new BusinessException(NotificationErrorCodes.TenantRequired);
		var source = await sources.Single(x => x.EventKeys.Contains(input.EventKey)).LoadAsync(new(Guid.Empty, tenantId, input.EventKey,
			input.SourceType, input.SourceId, input.SourceRevision, input.OccurredAt, input.AffectedRecipients), cancellationToken);
		var scoped = partitions.Partition(new(Guid.Empty, tenantId, input.EventKey, input.SourceType, input.SourceId, input.SourceRevision,
			input.OccurredAt, input.AffectedRecipients), definition, source);
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
			if (!resolved.TryGetValue(partition.Scope.Key, out var rule))
			{
				rule = await rules.ResolveAsync(input.EventKey, partition.Scope, cancellationToken); resolved[partition.Scope.Key] = rule;
			}
			if (rule is not { IsEnabled: true }) continue;
			var key = (rule.Id, rule.Revision);
			if (!configured.TryGetValue(key, out var config))
			{
				try { config = new(configuration.Deserialize(rule.ConfigurationJson, definition), null); }
				catch (BusinessException error) when (error.Code == NotificationErrorCodes.RuleConfigurationInvalid) { config = new(null, error.Code); }
				configured[key] = config;
			}
			if (config.Value == null)
			{
				results.Add(new(rule.Id, rule.Revision, partition.Scope, partition.Recipients, "{}", [], input.DueAt ?? input.OccurredAt,
					expiry, NotificationIntentState.NeedsReview, config.ErrorCode ?? NotificationErrorCodes.RuleConfigurationInvalid)); continue;
			}
			var selected = partition.Recipients.Where(x => x.Kind == "Student"
				? definition.RecipientSelectors.Contains("PrimaryGuardian") || config.Value.IncludeStudentAccount
				: config.Value.StaffAudienceSelectors.Contains(x.Kind)).ToArray();
			if (selected.Length == 0) continue;
			var snapshot = "{}"; var due = input.DueAt ?? input.OccurredAt;
			try
			{
				snapshot = snapshots.Serialize(new(1, new(partition.Scope.AcademicYearId, partition.Scope.BoardId, partition.Scope.MediumId,
					partition.Scope.ClassId, partition.Scope.DivisionId), config.Value), definition);
				due = due.AddMinutes(config.Value.Timing.DelayMinutes);
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
					try { versions = await templates.PinAsync(config.Value, definition, cancellationToken); pinned[key] = versions; }
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
