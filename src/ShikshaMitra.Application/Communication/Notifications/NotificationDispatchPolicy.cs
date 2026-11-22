using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Communication.Notifications;

public sealed record NotificationRecipientDispatchDecision(NotificationRuntimeRecipient Recipient, string LanguageCode,
	DateTimeOffset? DueAt, string? ExclusionCode);

public sealed record NotificationDispatchPolicyResult(string? ExclusionCode,
	IReadOnlyList<NotificationRecipientDispatchDecision> Recipients);

[RemoteService(false), DisableAuditing]
public sealed class NotificationDispatchPolicy(ICurrentTenant tenant, NotificationEffectiveRuleResolver rules,
	IRepository<NotificationPreference, Guid> preferences, IRepository<ConsentRecord, Guid> consents, IAsyncQueryableExecuter executer,
	NotificationSchoolTimeZone schoolZone, NotificationDeliveryTimingPolicy timing) : ITransientDependency
{
	private sealed record QuietWindow(TimeOnly Start, TimeOnly End);
	private sealed record PreferenceDecision(string Language, IReadOnlyList<QuietWindow> Windows, string? ExclusionCode);
	private sealed record ConsentDecisionRow(string Destination, string RecipientType, Guid? RecipientId,
		CommunicationConsentStatus Status, DateTimeOffset? ConsentCapturedAt, DateTimeOffset? RevokedAt);

	public async Task<NotificationDispatchPolicyResult> EvaluateAsync(NotificationIntent intent, NotificationCurrentSource source,
		IReadOnlyList<NotificationRuntimeRecipient> recipients, DateTimeOffset now, bool emailRequiresOptIn,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (tenant.Id is not Guid tenantId || tenantId == Guid.Empty || intent.TenantId != tenantId || source.TenantId != tenantId)
			throw new BusinessException(NotificationErrorCodes.TenantRequired);
		if (source.Definition.EventKey != intent.EventKey) throw new BusinessException(NotificationErrorCodes.RuntimeSnapshotInvalid);
		if (source.ExclusionCode != null) return new(source.ExclusionCode, []);
		var expiresAt = Earlier(source.ExpiresAt, intent.ExpiresAt);
		if (expiresAt <= now) return new("SourceExpired", []);
		var scope = source.Runtime.SourceScope;
		if (intent.Origin == NotificationIntentOrigin.Automatic && !await rules.IsEnabledForIntentAsync(intent,
			NotificationRuleScope.Create(scope.AcademicYearId, scope.BoardId, scope.MediumId, scope.ClassId, scope.DivisionId), cancellationToken))
			return new("RuleDisabled", []);
		return await EvaluateRecipientsAsync(tenantId, source, recipients, now, intent.DueAt, expiresAt, emailRequiresOptIn, cancellationToken);
	}

	internal Task<NotificationDispatchPolicyResult> EvaluatePreviewAsync(NotificationCurrentSource source,
		IReadOnlyList<NotificationRuntimeRecipient> recipients, DateTimeOffset now, DateTimeOffset dueAt,
		bool emailRequiresOptIn, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (tenant.Id is not Guid tenantId || tenantId == Guid.Empty || source.TenantId != tenantId)
			throw new BusinessException(NotificationErrorCodes.TenantRequired);
		if (source.ExclusionCode != null) return Task.FromResult(new NotificationDispatchPolicyResult(source.ExclusionCode, []));
		if (source.ExpiresAt <= now) return Task.FromResult(new NotificationDispatchPolicyResult("SourceExpired", []));
		return EvaluateRecipientsAsync(tenantId, source, recipients, now, dueAt, source.ExpiresAt, emailRequiresOptIn, cancellationToken);
	}

	private async Task<NotificationDispatchPolicyResult> EvaluateRecipientsAsync(Guid tenantId, NotificationCurrentSource source,
		IReadOnlyList<NotificationRuntimeRecipient> recipients, DateTimeOffset now, DateTimeOffset dueAt, DateTimeOffset? expiresAt,
		bool emailRequiresOptIn, CancellationToken cancellationToken)
	{
		var config = source.Runtime.Configuration;
		var roster = source.CurrentRecipients.ToHashSet();
		var available = recipients.Where(x => x.IsEligible && roster.Contains(x.SourceRecipient) && config.Channels.Contains(x.Channel)).Distinct().ToArray();
		var settings = await ReadPreferencesAsync(tenantId, source.Definition, available, cancellationToken);
		var preferenceDecisions = available.ToDictionary(x => x, x => ApplyPreferences(x, config, settings));
		var reachable = available.Where(x => preferenceDecisions[x].ExclusionCode == null).ToArray();
		var email = await ReadConsentsAsync(tenantId, reachable, CommunicationChannelType.Email, cancellationToken);
		var whatsApp = await ReadConsentsAsync(tenantId, reachable, CommunicationChannelType.WhatsApp, cancellationToken);
		var due = now < dueAt ? dueAt : now;
		Task<TimeZoneInfo>? zone = null;
		var results = new List<NotificationRecipientDispatchDecision>(recipients.Count);
		foreach (var recipient in recipients)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (!preferenceDecisions.TryGetValue(recipient, out var preference))
			{
				results.Add(new(recipient, config.FallbackLanguage, null, recipient.ExclusionCode ?? NotificationErrorCodes.RecipientIneligible));
				continue;
			}
			var reason = preference.ExclusionCode ?? EvaluateConsent(recipient, email, whatsApp, emailRequiresOptIn, now);
			DateTimeOffset? allowedAt = null;
			if (reason == null)
			{
				if (config.Timing.BypassQuietHours || preference.Windows.Count == 0)
				{
					if (expiresAt <= due) reason = "SourceExpired";
					else allowedAt = due.ToUniversalTime();
				}
				else
				{
					zone ??= schoolZone.GetAsync(cancellationToken);
					(allowedAt, reason) = FindDueTime(due, expiresAt, await zone, preference.Windows);
				}
			}
			results.Add(new(recipient, preference.Language, allowedAt, reason));
		}
		return new(null, results);
	}

	private async Task<ILookup<(Guid User, Guid? Student), NotificationPreference>> ReadPreferencesAsync(Guid tenantId,
		NotificationEventDefinition definition, NotificationRuntimeRecipient[] recipients, CancellationToken cancellationToken)
	{
		var users = recipients.Select(x => x.Recipient.UserId).OfType<Guid>().Distinct().ToArray();
		var students = recipients.Select(x => x.Recipient.StudentId).OfType<Guid>().Distinct().ToArray();
		var category = definition.EventKey.Split('.')[0] + "." + definition.Category;
		var rows = users.Length == 0 ? [] : await executer.ToListAsync((await preferences.GetQueryableAsync()).Where(x =>
			x.TenantId == tenantId && !x.IsDeleted && x.Category == category && Enumerable.Contains(users, x.UserId) &&
			(x.StudentId == null || Enumerable.Contains(students, x.StudentId.Value))), cancellationToken);
		return rows.ToLookup(x => (x.UserId, x.StudentId));
	}

	private static PreferenceDecision ApplyPreferences(NotificationRuntimeRecipient recipient, NotificationRuleRuntimeConfiguration config,
		ILookup<(Guid User, Guid? Student), NotificationPreference> preferences)
	{
		var language = config.FallbackLanguage;
		var windows = new List<QuietWindow>();
		if (config.Timing.QuietHoursEnabled) windows.Add(new(config.Timing.QuietStart, config.Timing.QuietEnd));
		if (recipient.Recipient.UserId is not Guid userId) return new(language, windows, null);
		var student = recipient.Recipient.StudentId;
		var rows = preferences[(userId, null)];
		if (student.HasValue) rows = rows.Concat(preferences[(userId, student)]);
		var current = rows.ToArray();
		if (current.GroupBy(x => x.StudentId).Any(x => x.Count() > 1)) return new(language, [], "PreferenceConfigurationInvalid");
		foreach (var row in current)
		{
			NotificationPreferenceConfiguration preference;
			try
			{
				preference = NotificationRuntimeJson.Deserialize<NotificationPreferenceConfiguration>(row.ConfigurationJson, "PreferenceConfigurationInvalid");
				if (preference.EnabledChannels == null || preference.EnabledChannels.Count > 4 ||
					preference.EnabledChannels.Any(x => !Enum.IsDefined(x) || x == CommunicationChannelType.Sms) ||
					row.LanguageCode is not { Length: > 0 and <= 16 } || CultureInfo.GetCultureInfo(row.LanguageCode).Name.Length == 0)
					return new(language, [], "PreferenceConfigurationInvalid");
			}
			catch (BusinessException error) when (error.Code == "PreferenceConfigurationInvalid") { return new(language, [], error.Code); }
			catch (CultureNotFoundException) { return new(language, [], "PreferenceConfigurationInvalid"); }
			if (!preference.EnabledChannels.Contains(recipient.Channel)) return new(language, [], "ChannelDisabledByPreference");
			language = row.LanguageCode;
			if (!preference.QuietHoursEnabled) continue;
			if (!ParseTime(preference.QuietHoursStart, out var start) || !ParseTime(preference.QuietHoursEnd, out var end) || start == end)
				return new(language, [], "PreferenceConfigurationInvalid");
			windows.Add(new(start, end));
		}
		return new(language, windows, null);
	}

	private async Task<ILookup<string, ConsentDecisionRow>> ReadConsentsAsync(Guid tenantId, NotificationRuntimeRecipient[] recipients,
		CommunicationChannelType channel, CancellationToken cancellationToken)
	{
		var destinations = recipients.Where(x => x.Channel == channel && x.Destination != null)
			.Select(x => NormalizeDestination(channel, x.Destination!)).Distinct(StringComparer.Ordinal).ToArray();
		var rows = destinations.Length == 0 ? [] : await executer.ToListAsync((await consents.GetQueryableAsync()).Where(x =>
				x.TenantId == tenantId && !x.IsDeleted && x.Channel == channel &&
				Enumerable.Contains(destinations, channel == CommunicationChannelType.Email ? x.Destination.ToLower() : x.Destination))
			.Select(x => new ConsentDecisionRow(x.Destination, x.RecipientType, x.RecipientId, x.Status, x.ConsentCapturedAt, x.RevokedAt)), cancellationToken);
		return rows.ToLookup(x => NormalizeDestination(channel, x.Destination), StringComparer.Ordinal);
	}

	private static string? EvaluateConsent(NotificationRuntimeRecipient recipient, ILookup<string, ConsentDecisionRow> email,
		ILookup<string, ConsentDecisionRow> whatsApp, bool emailRequiresOptIn, DateTimeOffset now)
	{
		if (recipient.Channel is not (CommunicationChannelType.Email or CommunicationChannelType.WhatsApp))
			return recipient.Recipient.UserId.HasValue ? null : "VerifiedAccountRequired";
		if (string.IsNullOrWhiteSpace(recipient.Destination)) return NotificationErrorCodes.RecipientIneligible;
		var rows = (recipient.Channel == CommunicationChannelType.Email ? email : whatsApp)
			[NormalizeDestination(recipient.Channel, recipient.Destination)];
		if (rows.Any(x => x.Status is CommunicationConsentStatus.OptedOut or CommunicationConsentStatus.Revoked || x.RevokedAt.HasValue))
			return "ConsentRevoked";
		if (!emailRequiresOptIn && recipient.Channel == CommunicationChannelType.Email) return null;
		return rows.Any(x => x.Status == CommunicationConsentStatus.OptedIn && x.ConsentCapturedAt <= now &&
			MatchesConsentOwner(recipient.Recipient, x)) ? null : "ConsentRequired";
	}

	private static bool MatchesConsentOwner(NotificationRecipientRefDto recipient, ConsentDecisionRow consent) =>
		consent.RecipientId is Guid owner && recipient.Kind switch
		{
			"Guardian" => consent.RecipientType == "ParentGuardian" && owner == recipient.StudentId,
			"StudentAccount" => consent.RecipientType == "Student" && owner == recipient.StudentId,
			_ => consent.RecipientType == "User" && owner == recipient.UserId ||
				consent.RecipientType == "Employee" && owner == recipient.EmployeeId
		};

	private (DateTimeOffset? DueAt, string? Reason) FindDueTime(DateTimeOffset proposed, DateTimeOffset? expiresAt,
		TimeZoneInfo zone, IReadOnlyList<QuietWindow> windows)
	{
		var due = proposed.ToUniversalTime();
		for (var pass = 0; pass < 8; pass++)
		{
			var next = due;
			foreach (var window in windows)
			{
				var allowed = timing.Evaluate(due, zone, true, window.Start, window.End, expiresAt);
				if (allowed.IsExpired) return (null, "SourceExpired");
				if (allowed.DueAt > next) next = allowed.DueAt.Value;
			}
			if (next == due) return (due, null);
			due = next;
		}
		// A recurring set of quiet windows can leave no available school time.
		return (null, "QuietHoursUnavailable");
	}

	private static bool ParseTime(string? value, out TimeOnly time) =>
		TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

	private static string NormalizeDestination(CommunicationChannelType channel, string destination) =>
		channel == CommunicationChannelType.Email ? destination.ToLowerInvariant() : destination;

	private static DateTimeOffset? Earlier(DateTimeOffset? first, DateTimeOffset? second) =>
		!first.HasValue || second < first ? second : first;
}
