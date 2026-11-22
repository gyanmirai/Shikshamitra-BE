using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false)]
public sealed class SubstitutionReviewerEscalationPolicy(NotificationEffectiveRuleResolver rules,
	NotificationRuleConfigurationCodec codec, SubstitutionNotificationEvidence evidence) : ITransientDependency
{
	internal async Task<int?> GetDelayAsync(NotificationSourceScopeDto scope, CancellationToken token)
	{
		const string key = SubstitutionNotificationSource.AcknowledgementDue;
		var rule = await rules.ResolveAsync(key, NotificationRuleScope.Create(scope.AcademicYearId, scope.BoardId, scope.MediumId,
			scope.ClassId, scope.DivisionId), token);
		if (rule is not { IsEnabled: true }) return null;
		try { return codec.Deserialize(rule.ConfigurationJson, AcademicNotificationCatalogue.GetDefinition(key)).Timing.ReviewerEscalationMinutes; }
		catch (BusinessException error) when (error.Code == NotificationErrorCodes.RuleConfigurationInvalid) { return null; }
	}

	internal async Task<DateTimeOffset?> FindReminderAcceptanceAsync(Guid tenantId, SubstitutionNotificationSource.ScopedDuty match,
		NotificationRecipientRefDto substitute, CancellationToken token)
	{
		if (!SubstitutionNotificationSource.CanRemind(match.Duty)) return null;
		var receipts = await evidence.ReadAsync(tenantId, match.Duty.Id, token, SubstitutionNotificationSource.AcknowledgementDue,
			SubstitutionNotificationSource.Revision(match.Duty));
		return receipts.Where(x => x.Matches(substitute)).Select(x => (DateTimeOffset?)x.AcceptedAt).Min();
	}
}
