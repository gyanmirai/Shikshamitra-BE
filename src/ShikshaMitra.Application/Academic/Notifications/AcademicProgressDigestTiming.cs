using System;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Academic.Notifications;

internal sealed record AcademicProgressDigestWindow(LearningTaskDigestWindow Period, int? MilestonePercentage)
{
	internal string Revision(NotificationSourceScopeDto scope) => MilestonePercentage.HasValue
		? LearningTaskSubmissionDigestTiming.Fingerprint(new { scope, Period, MilestonePercentage })
		: LearningTaskSubmissionDigestTiming.Revision(scope, Period);
}

[RemoteService(false)]
public sealed class AcademicProgressDigestTiming(NotificationEffectiveRuleResolver rules, NotificationRuleConfigurationCodec codec)
	: ITransientDependency
{
	internal async Task<AcademicProgressDigestWindow?> ResolveAsync(string eventKey, NotificationSourceScopeDto scope,
		DateTimeOffset at, TimeZoneInfo zone, CancellationToken token)
	{
		var rule = await rules.ResolveAsync(eventKey,
			NotificationRuleScope.Create(scope.AcademicYearId, scope.BoardId, scope.MediumId, scope.ClassId, scope.DivisionId), token);
		if (rule is not { IsEnabled: true }) return null;
		try
		{
			var timing = codec.Deserialize(rule.ConfigurationJson, AcademicNotificationCatalogue.GetDefinition(eventKey)).Timing;
			return new(LearningTaskSubmissionDigestTiming.ClosedWindow(at, zone, timing.ReminderLocalTime), timing.ProgressMilestonePercentage);
		}
		catch (BusinessException error) when (error.Code == NotificationErrorCodes.RuleConfigurationInvalid) { return null; }
	}
}
