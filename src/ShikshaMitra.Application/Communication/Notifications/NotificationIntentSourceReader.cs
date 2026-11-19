using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Communication.Notifications;

public sealed record NotificationCurrentSource(Guid TenantId, NotificationEventDefinition Definition, NotificationIntentRuntimeSnapshot Runtime,
	IReadOnlyList<NotificationRecipientRefDto> CapturedRecipients, IReadOnlyList<NotificationRecipientRefDto> CurrentRecipients,
	NotificationSourceSnapshot? Snapshot, DateTimeOffset? ExpiresAt, string? ExclusionCode);

[RemoteService(false)]
public sealed class NotificationIntentSourceReader(ICurrentTenant tenant, IEnumerable<INotificationEventDefinitionProvider> definitions,
	IEnumerable<INotificationSourceAdapter> adapters, NotificationIntentSnapshotCodec snapshots, NotificationSourcePartitioner partitions)
	: ITransientDependency
{
	public async Task<NotificationCurrentSource> LoadAsync(NotificationIntent intent, DateTimeOffset now, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (tenant.Id is not Guid tenantId || tenantId == Guid.Empty || intent.TenantId != tenantId)
			throw new BusinessException(NotificationErrorCodes.TenantRequired);
		var matchingDefinitions = definitions.SelectMany(x => x.GetAll()).Where(x => x.EventKey == intent.EventKey).Take(2).ToArray();
		if (matchingDefinitions.Length != 1 || matchingDefinitions[0].SourceType != intent.SourceType)
			throw new BusinessException(NotificationErrorCodes.SourceUnavailable);
		var definition = matchingDefinitions[0];
		var runtime = snapshots.Deserialize(intent.RuleConfigurationJson, definition);
		if ((intent.Origin == NotificationIntentOrigin.ReviewedManual) != (runtime.ReviewedDeliveryDigest != null))
			throw new BusinessException(NotificationErrorCodes.RuntimeSnapshotInvalid);
		var captured = NotificationRuntimeJson.Deserialize<NotificationRecipientRefDto[]>(
			intent.AffectedRecipientsJson, NotificationErrorCodes.RuntimeSnapshotInvalid);
		if (captured.Length is 0 or > 100 || captured.Distinct().Count() != captured.Length ||
			captured.Any(x => !NotificationSourcePartitioner.ValidReference(x, definition)))
			throw new BusinessException(NotificationErrorCodes.RuntimeSnapshotInvalid);
		var expiry = intent.ExpiresAt;
		if (expiry <= now) return Exclude("SourceExpired");
		var matchingAdapters = adapters.Where(x => x.EventKeys.Contains(intent.EventKey)).Take(2).ToArray();
		if (matchingAdapters.Length != 1) return Exclude(NotificationErrorCodes.SourceUnavailable);
		var context = new NotificationIntentContext(intent.Id, tenantId, intent.EventKey, intent.SourceType, intent.SourceId,
			intent.SourceRevision, intent.OccurredAt, captured, runtime.SourceScope, runtime.DueOccurrenceAt) { DuePhase = runtime.DuePhase };
		var source = await matchingAdapters[0].LoadAsync(context, cancellationToken);
		if (!source.IsEligible) return Exclude(NotificationErrorCodes.SourceUnavailable);
		if (source.SourceRevision != intent.SourceRevision) return Exclude("SourceRevisionChanged");
		if (source.ExpiresAt.HasValue && (!expiry.HasValue || source.ExpiresAt < expiry)) expiry = source.ExpiresAt;
		if (expiry <= now) return Exclude("SourceExpired");

		var scope = runtime.SourceScope;
		var scopeKey = NotificationRuleScope.Create(scope.AcademicYearId, scope.BoardId, scope.MediumId, scope.ClassId, scope.DivisionId).Key;
		// The current roster can lose captured references. Validate it before intersecting the two rosters.
		var current = partitions.Partition(context with { AffectedRecipients = [] }, definition, source)
			.Where(x => x.Scope.Key == scopeKey).SelectMany(x => x.Recipients).ToHashSet();
		return new(tenantId, definition, runtime, captured, captured.Where(current.Contains).ToArray(), source, expiry, null);

		NotificationCurrentSource Exclude(string code) => new(tenantId, definition, runtime, captured, [], null, expiry, code);
	}
}
