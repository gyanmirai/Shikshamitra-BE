using System;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Academic.Notifications;

internal sealed record LearningTaskDigestWindow(DateTimeOffset Start, DateTimeOffset End, DateTimeOffset ExpiresAt);

[RemoteService(false)]
public sealed class LearningTaskSubmissionDigestTiming(NotificationEffectiveRuleResolver rules, NotificationRuleConfigurationCodec configuration)
	: ITransientDependency
{
	internal async Task<LearningTaskDigestWindow?> ResolveAsync(string eventKey, NotificationSourceScopeDto scope, DateTimeOffset at,
		TimeZoneInfo timeZone, bool requireEnabled, CancellationToken cancellationToken)
	{
		var rule = await rules.ResolveAsync(eventKey,
			NotificationRuleScope.Create(scope.AcademicYearId, scope.BoardId, scope.MediumId, scope.ClassId, scope.DivisionId), cancellationToken);
		if (requireEnabled && rule is not { IsEnabled: true }) return null;

		var cutoff = NotificationRuleTimingConfiguration.Default.ReminderLocalTime;
		if (rule != null)
		{
			try
			{
				cutoff = configuration.Deserialize(rule.ConfigurationJson, AcademicNotificationCatalogue.GetDefinition(eventKey)).Timing.ReminderLocalTime;
			}
			catch (BusinessException error) when (error.Code == NotificationErrorCodes.RuleConfigurationInvalid) { return null; }
		}
		return ClosedWindow(at, timeZone, cutoff);
	}

	internal static LearningTaskDigestWindow ClosedWindow(DateTimeOffset at, TimeZoneInfo timeZone, TimeOnly cutoff)
	{
		var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, timeZone).DateTime);
		var end = Boundary(date);
		if (end > at)
		{
			date = date.AddDays(-1);
			end = Boundary(date);
		}
		return new(Boundary(date.AddDays(-1)), end, Boundary(date.AddDays(1)));

		DateTimeOffset Boundary(DateOnly day) =>
			NotificationDeliveryTimingPolicy.Boundary(day.ToDateTime(cutoff), timeZone, later: false);
	}

	internal static string Revision(NotificationSourceScopeDto scope, LearningTaskDigestWindow window) => Fingerprint(new { scope, window });

	internal static string Fingerprint<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
