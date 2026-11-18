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
	IRepository<CommunicationApprovalRule, Guid> approvalRules, IAsyncQueryableExecuter queries) : ITransientDependency
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
		if (snapshot.EligibleRecipients == null || snapshot.EligibleRecipients.Count > 10000)
			throw new BusinessException(NotificationErrorCodes.SourceUnavailable);

		var sourceRoster = snapshot.EligibleRecipients.ToHashSet();
		if (input.SelectedRecipients.Any(x => !sourceRoster.Contains(x))) throw new AbpAuthorizationException();
		var sourceContext = new NotificationIntentContext(Guid.Empty, context.TenantId, input.EventKey, input.SourceType,
			input.SourceId, snapshot.SourceRevision, now, input.SelectedRecipients);
		var partitions = partitioner.Partition(sourceContext, definition, snapshot);
		var email = await evaluator.FindEmailProviderAsync(channels, cancellationToken);

		var results = new List<AcademicNotificationManualPartition>();
		var samples = new List<AcademicNotificationPreviewSampleDto>();
		var issues = new HashSet<string>(StringComparer.Ordinal);
		foreach (var group in partitions.GroupBy(x => x.Scope))
		{
			var scope = group.Key;
			await scopeAccess.RequireAsync(new(scope.AcademicYearId, scope.BoardId, scope.MediumId, scope.ClassId, scope.DivisionId),
				context.TenantId, context.Scope, cancellationToken);
			var rule = await rules.ResolveAsync(input.EventKey, scope, cancellationToken);
			var configured = rule != null && rule.ConfigurationJson != "{}"
				? codec.Deserialize(rule.ConfigurationJson, definition, allowNoTemplates: !rule.IsEnabled) : null;
			var staff = group.SelectMany(x => x.Recipients).Where(x => x.Kind != "Student")
				.Select(x => x.Kind).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
			if (configured != null) staff = staff.Where(configured.StaffAudienceSelectors.Contains).ToArray();

			var configuration = new NotificationRuleRuntimeConfiguration(1, configured?.GuardianMode ?? NotificationGuardianMode.PrimaryOnly,
				configured?.AdditionalGuardianRelationships ?? [], configured?.IncludeStudentAccount == true,
				staff, channels, configured?.TemplateIds ?? [], (configured?.Timing ?? NotificationRuleTimingConfiguration.Default) with
				{ DelayMinutes = 0, BypassQuietHours = false }, configured?.FallbackLanguage ?? "en");
			var family = group.SelectMany(x => x.Recipients).Any(x => x.Kind == "Student");
			var kinds = NotificationTemplateSnapshotSelector.RecipientKinds(configuration, definition)
				.Where(x => family || x is not ("Guardian" or "StudentAccount")).ToArray();
			var selected = await templates.SelectAsync(context.TenantId, scope.MediumId, definition, configuration, kinds,
				input.TemplateId, cancellationToken);
			samples.AddRange(selected.Samples);
			if (selected.Revisions.Count == 0) issues.Add(NotificationErrorCodes.TemplateInvalid);
			configuration = configuration with { TemplateIds = selected.Headers };
			foreach (var partition in group)
			{
				var recipients = partition.Recipients.Where(x => x.Kind == "Student" || staff.Contains(x.Kind)).ToArray();
				if (recipients.Length == 0) continue;
				var runtime = new NotificationIntentRuntimeSnapshot(1, new(scope.AcademicYearId, scope.BoardId, scope.MediumId,
					scope.ClassId, scope.DivisionId), configuration);
				var current = new NotificationCurrentSource(context.TenantId, definition, runtime, recipients, recipients,
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

	private static CommunicationChannelType[] ValidateRequest(PreviewAcademicNotificationManualDto input, NotificationEventDefinition definition)
	{
		if (input.SelectedRecipients == null || input.SelectedRecipients.Count > 10000 ||
			input.SelectedRecipients.Distinct().Count() != input.SelectedRecipients.Count ||
			input.SelectedRecipients.Any(x => !NotificationSourcePartitioner.ValidReference(x, definition)) ||
			input.Channels == null || input.Channels.Count is 0 or > 4 || !input.Channels.Contains(CommunicationChannelType.InApp) ||
			input.Channels.Distinct().Count() != input.Channels.Count || input.Channels.Any(x => !definition.SupportedChannels.Contains(x)) ||
			input.TemplateId == Guid.Empty)
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
