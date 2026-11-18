using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Timing;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), DisableAuditing]
public sealed class AcademicNotificationRulePreview(NotificationSourcePartitioner partitioner,
	IEnumerable<INotificationRuntimeRecipientResolver> resolvers, NotificationDispatchPolicy policy,
	NotificationDispatchMaterializer materializer, NotificationEmailRoute emailRoute, NotificationTemplateSnapshotSelector snapshots,
	AcademicNotificationRuleTemplates templates, NotificationTemplatePreviewService templatePreview, IClock clock) : ITransientDependency
{
	internal async Task<AcademicNotificationRulePreviewDto> BuildAsync(AcademicNotificationAccessContext context, Guid sourceId,
		NotificationEventDefinition definition, NotificationRuleScope requestedScope, NotificationRuleRuntimeConfiguration configuration,
		CancellationToken cancellationToken)
	{
		var now = new DateTimeOffset(clock.Now.ToUniversalTime());
		var source = context.Source;
		if (!source.IsEligible || source.ExpiresAt <= now)
			return Summarize(configuration.Channels, [], [], [], [source.ExclusionCode ?? NotificationErrorCodes.SourceUnavailable]);
		if (source.EligibleRecipients == null || source.EligibleRecipients.Count > 10000)
			throw new BusinessException(NotificationErrorCodes.SourceUnavailable);

		var sourceContext = new NotificationIntentContext(Guid.Empty, context.TenantId, definition.EventKey,
			definition.SourceType, sourceId, source.SourceRevision, now, []);
		var selections = partitioner.Partition(sourceContext, definition, source)
			.Where(x => requestedScope.Contains(x.Scope))
			.Select(x => x with { Recipients = x.Recipients.Where(r => AppliesTo(r, configuration, definition)).ToArray() })
			.Where(x => x.Recipients.Count != 0).ToArray();
		await templates.EnsureAsync(context.TenantId, requestedScope, definition, configuration, enabled: false, cancellationToken);
		var sampleRevisions = await templates.LoadSampleRevisionsAsync(context.TenantId, requestedScope, definition, configuration, cancellationToken);
		var samples = sampleRevisions.GroupBy(x => (x.RecipientKind, x.Channel))
			.Select(group => group.FirstOrDefault(x => x.LanguageCode == configuration.FallbackLanguage) ?? group.First())
			.Select(x => new AcademicNotificationPreviewSampleDto(x.TemplateId, x.Id, x.RecipientKind, x.Channel,
				x.LanguageCode, true, templatePreview.RenderSynthetic(x, definition))).ToArray();

		var issues = new HashSet<string>(StringComparer.Ordinal);
		if (selections.Length == 0) issues.Add("NoRecipientsInRuleScope");
		if (configuration.StaffAudienceSelectors.Contains("ConfiguredReviewer") &&
			!selections.SelectMany(x => x.Recipients).Any(x => x.Kind == "ConfiguredReviewer")) issues.Add("ReviewerNotConfigured");
		var pins = await GetPinsAsync(configuration, definition, cancellationToken);
		if (pins.Count == 0) issues.Add(NotificationErrorCodes.TemplateInvalid);
		if (selections.Length == 0) return Summarize(configuration.Channels, [], [], samples, issues);

		var matching = resolvers.Where(x => x.Supports(definition.EventKey)).Take(2).ToArray();
		if (matching.Length != 1) throw new BusinessException(NotificationErrorCodes.SourceUnavailable);
		var email = configuration.Channels.Contains(CommunicationChannelType.Email) ? await emailRoute.FindAsync(cancellationToken) : null;
		var decisions = new List<NotificationRecipientDispatchDecision>();
		var prepared = new List<NotificationPreparedDispatch>();
		foreach (var selected in selections)
		{
			var scope = selected.Scope;
			var runtime = new NotificationIntentRuntimeSnapshot(1, new(scope.AcademicYearId, scope.BoardId,
				scope.MediumId, scope.ClassId, scope.DivisionId), configuration);
			var current = new NotificationCurrentSource(context.TenantId, definition, runtime, selected.Recipients,
				selected.Recipients, source, source.ExpiresAt, null);
			var candidates = await matching[0].ResolveAsync(current, cancellationToken);
			var policyResult = await policy.EvaluatePreviewAsync(current, candidates, now,
				now.AddMinutes(configuration.Timing.DelayMinutes), email?.ConsentRequired == true, cancellationToken);
			if (policyResult.ExclusionCode != null)
			{
				issues.Add(policyResult.ExclusionCode);
				continue;
			}
			decisions.AddRange(policyResult.Recipients);
			if (pins.Count > 0)
				prepared.AddRange(await materializer.PreparePreviewAsync(current, policyResult, email, pins, cancellationToken));
			else
				prepared.AddRange(policyResult.Recipients.Select(x =>
					new NotificationPreparedDispatch(x.Recipient, x.LanguageCode, null, x.ExclusionCode ?? NotificationErrorCodes.TemplateInvalid)));
		}
		return Summarize(configuration.Channels, decisions, prepared, samples, issues);
	}

	private async Task<IReadOnlyList<Guid>> GetPinsAsync(NotificationRuleRuntimeConfiguration configuration,
		NotificationEventDefinition definition, CancellationToken cancellationToken)
	{
		if (configuration.TemplateIds.Count == 0) return [];
		try { return await snapshots.PinAsync(configuration, definition, cancellationToken); }
		catch (BusinessException error) when (error.Code == NotificationErrorCodes.TemplateInvalid) { return []; }
	}

	private static bool AppliesTo(NotificationRecipientRefDto recipient, NotificationRuleRuntimeConfiguration configuration,
		NotificationEventDefinition definition) => recipient.Kind == "Student"
			? definition.RecipientSelectors.Contains("PrimaryGuardian") || configuration.IncludeStudentAccount
			: configuration.StaffAudienceSelectors.Contains(recipient.Kind);

	private static AcademicNotificationRulePreviewDto Summarize(IReadOnlyList<CommunicationChannelType> channels,
		IReadOnlyList<NotificationRecipientDispatchDecision> decisions, IReadOnlyList<NotificationPreparedDispatch> prepared,
		IReadOnlyList<AcademicNotificationPreviewSampleDto> samples, IEnumerable<string> issues)
	{
		var eligible = decisions.Where(x => x.ExclusionCode == null && x.DueAt.HasValue).Select(x => x.Recipient).ToArray();
		var summary = channels.Select(channel =>
		{
			var rows = prepared.Where(x => x.Recipient.Channel == channel).ToArray();
			var exclusions = rows.Where(x => x.ExclusionCode != null).GroupBy(x => x.ExclusionCode!, StringComparer.Ordinal)
				.OrderBy(x => x.Key, StringComparer.Ordinal)
				.Select(x => new AcademicNotificationPreviewExclusionDto(x.Key, x.Select(y => y.Recipient.Recipient).Distinct().Count())).ToArray();
			return new AcademicNotificationPreviewChannelDto(channel,
				eligible.Where(x => x.Channel == channel).Select(x => x.Recipient).Distinct().Count(),
				rows.Count(x => x.ExclusionCode == null), exclusions);
		}).ToArray();
		return new(eligible.Select(x => x.Recipient).Distinct().Count(), summary, samples, issues.Order(StringComparer.Ordinal).ToArray());
	}
}
