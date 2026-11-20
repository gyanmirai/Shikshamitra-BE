using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Application.Communication;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Notifications;

internal sealed record AcademicNotificationManualPartition(NotificationCurrentSource Source, Guid? RuleId, int? RuleRevision,
	IReadOnlyList<Guid> TemplateRevisions, AcademicNotificationAudienceEvaluation Evaluation);

internal sealed record AcademicNotificationManualPlan(IReadOnlyList<AcademicNotificationManualPartition> Partitions,
	AcademicNotificationRulePreviewDto Summary, bool RequiresApproval);

[RemoteService(false), DisableAuditing]
public sealed class AcademicNotificationManualPlanner(AcademicNotificationCatalogue catalogue, NotificationSourcePartitioner partitioner,
	AcademicNotificationRuleScopeAccess scopeAccess, NotificationEffectiveRuleResolver rules, NotificationRuleConfigurationCodec codec,
	AcademicNotificationManualTemplates templates, AcademicNotificationAudienceEvaluator evaluator,
	IRepository<CommunicationApprovalRule, Guid> approvalRules, IAsyncQueryableExecuter queries,
	Student360NotificationAudience student360Audience) : ITransientDependency
{
	internal async Task<AcademicNotificationManualPlan> BuildAsync(PreviewAcademicNotificationManualDto input,
		AcademicNotificationAccessContext context, DateTimeOffset now, CancellationToken cancellationToken)
	{
		var definition = catalogue.Get(input.EventKey);
		var channels = ValidateRequest(input, definition);
		var snapshot = context.Source;
		if (!snapshot.IsEligible || snapshot.ExpiresAt <= now)
			return new([], AcademicNotificationRulePreview.Summarize(channels, [], [], [],
				[snapshot.ExclusionCode ?? NotificationErrorCodes.SourceUnavailable]), false);
		var partitions = await ReadAuthorizedPartitionsAsync(input, context, definition, now, cancellationToken);
		var isStudent360 = Student360NotificationAudience.Supports(input.EventKey);
		var requestedFamily = input.SelectedFamilyRelationships;
		if (requestedFamily != null)
		{
			var available = await student360Audience.ReadFamilyChoicesAsync(input.EventKey, context, cancellationToken);
			if (requestedFamily.Any(x => !available.Any(choice => choice.Relationship == x)))
				throw new BusinessException(NotificationErrorCodes.RecipientIneligible);
		}
		var additionalGuardians = requestedFamily?.Where(x => x != "self").Order(StringComparer.Ordinal).ToArray() ?? [];
		var effectiveDefinition = NotificationSourceDeliveryPolicy.Restrict(definition, snapshot);
		if (channels.Any(x => !effectiveDefinition.SupportedChannels.Contains(x)))
			return new([], AcademicNotificationRulePreview.Summarize(channels, [], [], [], ["ChannelsExcludedBySource"]), false);
		var email = await evaluator.FindEmailProviderAsync(channels, cancellationToken);

		var results = new List<AcademicNotificationManualPartition>();
		var samples = new List<AcademicNotificationPreviewSampleDto>();
		var issues = new HashSet<string>(StringComparer.Ordinal);
		foreach (var group in partitions.GroupBy(x => x.Scope))
		{
			var scope = group.Key;
			var rule = await rules.ResolveAsync(input.EventKey, scope, cancellationToken);
			var configured = rule != null && rule.ConfigurationJson != "{}"
				? codec.Deserialize(rule.ConfigurationJson, definition, allowNoTemplates: !rule.IsEnabled) : null;
			var staff = group.SelectMany(x => x.Recipients).Where(x => x.Kind != "Student")
				.Select(x => x.Kind).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
			if (!isStudent360 && configured != null) staff = staff.Where(configured.StaffAudienceSelectors.Contains).ToArray();

			var guardianMode = isStudent360
				? additionalGuardians.Length == 0 ? NotificationGuardianMode.PrimaryOnly : NotificationGuardianMode.PrimaryAndSelected
				: configured?.GuardianMode ?? NotificationGuardianMode.PrimaryOnly;
			var configuration = new NotificationRuleRuntimeConfiguration(1, guardianMode,
				isStudent360 ? additionalGuardians : configured?.AdditionalGuardianRelationships ?? [],
				isStudent360 ? requestedFamily?.Contains("self") == true : configured?.IncludeStudentAccount == true,
				staff, channels, configured?.TemplateIds ?? [], (configured?.Timing ?? NotificationRuleTimingConfiguration.Default) with
				{ DelayMinutes = 0, BypassQuietHours = false }, configured?.FallbackLanguage ?? "en");
			configuration = NotificationSourceDeliveryPolicy.Restrict(configuration, effectiveDefinition, snapshot);
			var family = group.SelectMany(x => x.Recipients).Any(x => x.Kind == "Student");
			var kinds = NotificationTemplateSnapshotSelector.RecipientKinds(configuration, effectiveDefinition)
				.Where(x => family || x is not ("Guardian" or "StudentAccount"))
				.Where(x => x != "Guardian" || requestedFamily == null || additionalGuardians.Length != 0).ToArray();
			if (snapshot.DeliveryLimits != null && kinds.Length == 0) continue;
			var selected = await templates.SelectAsync(context.TenantId, scope.MediumId, effectiveDefinition, configuration, kinds,
				input.TemplateId, cancellationToken);
			samples.AddRange(selected.Samples);
			if (selected.Revisions.Count == 0) issues.Add(NotificationErrorCodes.TemplateInvalid);
			configuration = configuration with { TemplateIds = selected.Headers };
			foreach (var partition in group)
			{
				var recipients = partition.Recipients.Where(x => snapshot.DeliveryLimits == null
					? x.Kind == "Student" || staff.Contains(x.Kind)
					: NotificationSourceDeliveryPolicy.Includes(x, configuration, effectiveDefinition)).ToArray();
				if (recipients.Length == 0) continue;
				var runtime = new NotificationIntentRuntimeSnapshot(1, new(scope.AcademicYearId, scope.BoardId, scope.MediumId,
					scope.ClassId, scope.DivisionId), configuration) { SelectedFamilyRelationships = requestedFamily };
				var current = new NotificationCurrentSource(context.TenantId, effectiveDefinition, runtime, recipients, recipients,
					snapshot, snapshot.ExpiresAt, null);
				var evaluated = await evaluator.EvaluateAsync(current, selected.Revisions, email, now, cancellationToken);
				if (evaluated.BlockingIssue != null) issues.Add(evaluated.BlockingIssue);
				results.Add(new(current, rule?.Id, rule?.Revision, selected.Revisions, evaluated));
			}
		}

		var prepared = results.SelectMany(x => x.Evaluation.Prepared).ToArray();
		if (!prepared.Any(x => x.ReviewFingerprint != null)) issues.Add("NoEligibleDeliveries");
		var summary = AcademicNotificationRulePreview.Summarize(channels, results.SelectMany(x => x.Evaluation.Decisions).ToArray(),
			prepared, samples.DistinctBy(x => x.RevisionId).ToArray(), issues);
		var requiresApproval = summary.EligibleRecipientCount > 100 ||
			await HasApprovalRuleAsync(context.TenantId, channels, summary.EligibleRecipientCount, cancellationToken);
		return new(results, summary, requiresApproval);
	}

	internal async Task EnsureScopeAsync(PreviewAcademicNotificationManualDto input, AcademicNotificationAccessContext context,
		CancellationToken cancellationToken)
	{
		if (context.Source.IsEligible)
			await ReadAuthorizedPartitionsAsync(input, context, catalogue.Get(input.EventKey), DateTimeOffset.UtcNow, cancellationToken);
	}

	private async Task<IReadOnlyList<NotificationSourcePartition>> ReadAuthorizedPartitionsAsync(
		PreviewAcademicNotificationManualDto input, AcademicNotificationAccessContext context,
		NotificationEventDefinition definition, DateTimeOffset now, CancellationToken cancellationToken)
	{
		var snapshot = context.Source;
		if (snapshot.EligibleRecipients == null || snapshot.EligibleRecipients.Count > 10000)
			throw new BusinessException(NotificationErrorCodes.SourceUnavailable);
		var sourceRoster = snapshot.EligibleRecipients.ToHashSet();
		if (input.SelectedRecipients.Any(x => !sourceRoster.Contains(x))) throw new AbpAuthorizationException();
		var sourceContext = new NotificationIntentContext(Guid.Empty, context.TenantId, input.EventKey, input.SourceType,
			input.SourceId, snapshot.SourceRevision, now, input.SelectedRecipients);
		var partitions = partitioner.Partition(sourceContext, definition, snapshot);
		foreach (var scope in partitions.Select(x => x.Scope).Distinct())
			await scopeAccess.RequireAsync(new(scope.AcademicYearId, scope.BoardId, scope.MediumId, scope.ClassId, scope.DivisionId),
				context.TenantId, context.Scope, cancellationToken);
		return partitions;
	}

	private static CommunicationChannelType[] ValidateRequest(PreviewAcademicNotificationManualDto input, NotificationEventDefinition definition)
	{
		if (input.SelectedRecipients == null || input.SelectedRecipients.Count > 10000 ||
			input.SelectedRecipients.Distinct().Count() != input.SelectedRecipients.Count ||
			input.SelectedRecipients.Any(x => !NotificationSourcePartitioner.ValidReference(x, definition)) ||
			input.Channels == null || input.Channels.Count is 0 or > 4 || !input.Channels.Contains(CommunicationChannelType.InApp) ||
			input.Channels.Distinct().Count() != input.Channels.Count || input.Channels.Any(x => !definition.SupportedChannels.Contains(x)) ||
			input.TemplateId == Guid.Empty)
			throw new BusinessException(NotificationErrorCodes.RecipientIneligible);
		var isStudent360 = Student360NotificationAudience.Supports(definition.EventKey);
		if ((isStudent360 && input.SelectedRecipients.Count == 0) ||
			(input.SelectedFamilyRelationships is { } family && (!isStudent360 ||
				!Student360NotificationAudience.ValidSelection(family) || !input.SelectedRecipients.Any(x => x.Kind == "Student"))))
			throw new BusinessException(NotificationErrorCodes.RecipientIneligible);
		return input.Channels.Order().ToArray();
	}

	private async Task<bool> HasApprovalRuleAsync(Guid tenantId, CommunicationChannelType[] channels, int eligible,
		CancellationToken cancellationToken)
	{
		var rows = await queries.ToListAsync((await approvalRules.GetQueryableAsync()).Where(x =>
			x.TenantId == tenantId && !x.IsDeleted && x.IsEnabled && x.RequiresApproval), cancellationToken);
		return rows.Any(x => CommunicationAppService.ApprovalRuleMatches(x, NotificationCampaignTypes.Academic,
			CommunicationSourceModule.Academic, CommunicationImportanceLevel.Normal, channels, eligible));
	}
}
