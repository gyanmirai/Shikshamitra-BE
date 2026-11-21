using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;

namespace ShikshaMitra.Library.Notifications;

public class LibraryNotificationDeliveryService(
	IRepository<LibraryNotificationSetting, Guid> settingRepository,
	IRepository<LibraryNotificationHistory, Guid> historyRepository,
	LibraryNotificationCatalog catalog,
	IBackgroundJobManager jobs,
	ILibraryInventoryLock inventoryLock,
	LibraryNotificationRecipientResolver recipients,
	ILibraryNotificationAuthorityService authority,
	ILibraryNotificationReminderEligibility eligibility,
	IClock clock,
	IGuidGenerator guidGenerator,
	ICurrentTenant currentTenant,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	public async Task<List<LibraryNotificationHistory>> DeliverAsync(LibraryNotificationDeliveryRequest request)
	{
		await inventoryLock.AcquireAsync(currentTenant.Id);
		var definition = catalog.Resolve(request.EventType);
		var settings = await EnsureSettingsAsync();
		var channels = request.Channels.Count == 0
			? new List<string> { LibraryNotificationChannels.InApp }
			: request.Channels.Select(channel => catalog.GetChannels().FirstOrDefault(option => option.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase))?.Channel
				?? throw new Volo.Abp.BusinessException(LibraryErrorCodes.NotificationChannelDisabled, "Choose a supported notification channel."))
				.Distinct(StringComparer.Ordinal).ToList();
		var delivered = new List<LibraryNotificationHistory>();

		foreach (var source in request.Recipients)
		{
			foreach (var channel in channels)
			{
				var destinations = await recipients.ResolveDestinationsAsync(source, channel);
				if (destinations.Count == 0) destinations = [source with { UserId = null, Email = null, Kind = "Unavailable" }];
				foreach (var recipient in destinations)
				{
				var setting = settings.FirstOrDefault(x =>
					x.EventType.Equals(definition.EventType, StringComparison.OrdinalIgnoreCase) &&
					x.RecipientRole.Equals(recipient.RecipientRole, StringComparison.OrdinalIgnoreCase) &&
					x.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase));

				if (setting == null)
				{
					continue;
				}

				var deduplicationKey = BuildDeduplicationKey(request, recipient, setting.Channel);
				if (!string.IsNullOrWhiteSpace(deduplicationKey))
				{
					var historyQuery = await historyRepository.GetQueryableAsync();
					var alreadyDelivered = await asyncExecuter.AnyAsync(historyQuery.Where(x => x.DeduplicationKey == deduplicationKey));
					if (alreadyDelivered)
					{
						continue;
					}
				}

				var placeholders = new Dictionary<string, string?>(request.Placeholders, StringComparer.OrdinalIgnoreCase)
				{
					["memberName"] = recipient.MemberName ?? recipient.RecipientName,
					["recipientName"] = recipient.RecipientName,
					["recipientEmail"] = recipient.Email
				};
				var subject = Render(request.SubjectOverride ?? setting.SubjectTemplate, placeholders);
				var message = Render(request.MessageOverride ?? setting.MessageTemplate, placeholders);
				var oversized = subject.Length > 300 || message.Length > 2000;
				var fullSubject = subject;
				var fullMessage = message;
				if (oversized) { subject = Bound(subject, 300); message = Bound(message, 2000); }

				var status = LibraryNotificationDeliveryStatuses.Queued;
				string? error = null;
				DateTime? sentAt = null;

				if (oversized)
				{
					status = LibraryNotificationDeliveryStatuses.Skipped;
					error = "Rendered notification exceeds the supported length. Shorten the template or its placeholders.";
				}
				else if (setting.IsComingSoon || !setting.IsChannelAvailable || !setting.IsActive)
				{
					status = LibraryNotificationDeliveryStatuses.Skipped;
					error = setting.IsComingSoon
						? "Notification event is coming soon."
						: !setting.IsChannelAvailable
							? "Notification channel is not available in this phase."
							: "Notification setting is disabled.";
					sentAt = null;
				}
				else if (setting.Channel.Equals(LibraryNotificationChannels.Email, StringComparison.OrdinalIgnoreCase))
				{
					if (string.IsNullOrWhiteSpace(recipient.Email))
					{
						status = LibraryNotificationDeliveryStatuses.Skipped;
						error = "Recipient email is missing.";
						sentAt = null;
					}
				}
				else if (setting.Channel == LibraryNotificationChannels.InApp && !recipient.UserId.HasValue)
				{
					status = LibraryNotificationDeliveryStatuses.Skipped;
					error = "In-app delivery requires a current verified recipient account. Configure the member or operational recipient association.";
				}
				else if (setting.Channel != LibraryNotificationChannels.InApp)
				{
					status = LibraryNotificationDeliveryStatuses.Skipped;
					error = "This notification channel is unavailable.";
				}

				var history = new LibraryNotificationHistory(
					guidGenerator.Create(),
					currentTenant.Id,
					definition.EventType,
					definition.EventGroup,
					definition.EventName,
					setting.Channel,
					recipient.RecipientRole,
					recipient.MemberId,
					recipient.RecipientName,
					recipient.Email,
					subject,
					message,
					status,
					error,
					request.RelatedEntityType,
					request.RelatedEntityId,
					deduplicationKey,
					request.ReminderDate,
					sentAt);

				history.BindRecipient(recipient.UserId);
				if (oversized)
				{
					history.ExtraProperties["RenderedSubjectOverflow"] = fullSubject;
					history.ExtraProperties["RenderedMessageOverflow"] = fullMessage;
				}
				history.ExtraProperties["RecipientKind"] = recipient.Kind;
				history.ExtraProperties["RecipientRelationship"] = recipient.Relationship;
				history.ExtraProperties["MemberName"] = recipient.MemberName;
				history.ExtraProperties["RecipientDestinationSignature"] = LibraryNotificationRecipientResolver.DestinationSignature(currentTenant.Id, recipient, setting.Channel);
				var validRelatedState = await eligibility.CaptureAsync(history, request.FineEvidence);
				authority.Capture(history, request.Origin ?? throw new Volo.Abp.Authorization.AbpAuthorizationException());
				if (status == LibraryNotificationDeliveryStatuses.Queued && !validRelatedState)
					history.MarkSkipped("The related Library record is no longer eligible for this notification.");
				else if (status == LibraryNotificationDeliveryStatuses.Queued && !recipient.IsActiveLibraryMember && !await eligibility.AllowsRecoveryAsync(history))
					history.MarkSkipped("Recipient is not an active library member and this notification has no eligible existing Library obligation or receipt.");
				else if (status == LibraryNotificationDeliveryStatuses.Queued && !await authority.CanDispatchAsync(history))
					history.MarkSkipped("The initiating user's current access no longer permits this notification.");
				else if (status == LibraryNotificationDeliveryStatuses.Queued && channel == LibraryNotificationChannels.Email && await recipients.HasOptedOutAsync(recipient))
					history.MarkSkipped("The recipient has opted out of this email destination.");
				else if (status == LibraryNotificationDeliveryStatuses.Queued && await IsWithinRepeatIntervalAsync(history, setting.RepeatIntervalDays))
					continue;
				else if (status == LibraryNotificationDeliveryStatuses.Queued && setting.Channel == LibraryNotificationChannels.InApp)
					history.DeliverToInbox(clock.Now.ToUniversalTime());
				else if (status == LibraryNotificationDeliveryStatuses.Queued)
					history.QueueForDelivery(clock.Now.ToUniversalTime());
				await historyRepository.InsertAsync(history, autoSave: true);
				if (history.Status == LibraryNotificationDeliveryStatuses.Queued)
					await jobs.EnqueueAsync(new LibraryNotificationDeliveryJobArgs { TenantId = currentTenant.Id, HistoryId = history.Id });
				delivered.Add(history);
				}
			}
		}

		return delivered;
	}

	private async Task<bool> IsWithinRepeatIntervalAsync(LibraryNotificationHistory row, int intervalDays)
	{
		if (!row.ReminderDate.HasValue || intervalDays <= 1 || !row.RelatedEntityId.HasValue) return false;
		var threshold = row.ReminderDate.Value.Date.AddDays(-intervalDays);
		var prior = await historyRepository.GetListAsync(history => history.TenantId == currentTenant.Id && history.EventType == row.EventType &&
			history.Channel == row.Channel && history.RecipientMemberId == row.RecipientMemberId && history.RecipientUserId == row.RecipientUserId &&
			history.RelatedEntityId == row.RelatedEntityId && history.ReminderDate > threshold && history.ReminderDate <= row.ReminderDate &&
			(history.Status == LibraryNotificationDeliveryStatuses.Sent || history.Status == LibraryNotificationDeliveryStatuses.Queued ||
			 history.Status == LibraryNotificationDeliveryStatuses.Processing || history.Status == LibraryNotificationDeliveryStatuses.Unconfirmed));
		return prior.Any(history => history.ExtraProperties.GetValueOrDefault("RecipientDestinationSignature")?.ToString() ==
			row.ExtraProperties.GetValueOrDefault("RecipientDestinationSignature")?.ToString());
	}

	public async Task<List<LibraryNotificationSetting>> EnsureSettingsAsync()
	{
		var query = await settingRepository.GetQueryableAsync();
		var existing = await asyncExecuter.ToListAsync(query);
		var existingKeys = existing
			.Select(x => BuildSettingKey(x.EventType, x.RecipientRole, x.Channel))
			.ToHashSet(StringComparer.OrdinalIgnoreCase);

		var missing = catalog
			.BuildDefaultSettings(guidGenerator.Create, currentTenant.Id)
			.Where(x => !existingKeys.Contains(BuildSettingKey(x.EventType, x.RecipientRole, x.Channel)))
			.ToList();

		if (missing.Count > 0)
		{
			await settingRepository.InsertManyAsync(missing, autoSave: true);
			existing.AddRange(missing);
		}

		return existing
			.OrderBy(x => x.EventGroup)
			.ThenBy(x => x.EventName)
			.ThenBy(x => x.Channel)
			.ToList();
	}

	internal async Task<List<LibraryNotificationHistory>> RecordReservationSkippedAsync(Guid requestId, Guid? memberId,
		string reason, string fingerprint, LibraryNotificationOrigin origin, string eventType = LibraryNotificationEventTypes.ReservedBookAvailable)
	{
		await inventoryLock.AcquireAsync(currentTenant.Id);
		var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint + "|" + reason)));
		var key = $"reservation-skipped|{currentTenant.Id:N}|{requestId:N}|{eventType}|{digest}";
		if (await historyRepository.AnyAsync(row => row.TenantId == currentTenant.Id && row.DeduplicationKey == key)) return [];
		var definition = catalog.Resolve(eventType);
		var row = new LibraryNotificationHistory(guidGenerator.Create(), currentTenant.Id, definition.EventType, definition.EventGroup,
			definition.EventName, LibraryNotificationChannels.InApp, LibraryNotificationRecipientRoles.Member, memberId,
			null, null, "Reservation notification skipped", reason, LibraryNotificationDeliveryStatuses.Skipped, reason,
			nameof(BookReservation), requestId, key, null, null);
		authority.Capture(row, origin);
		await historyRepository.InsertAsync(row, true);
		return [row];
	}

	internal async Task<List<LibraryNotificationHistory>> RecordSetupSkippedAsync(string reason, string? configurationFingerprint)
	{
		await inventoryLock.AcquireAsync(currentTenant.Id);
		var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(configurationFingerprint ?? reason)));
		var key = $"automatic-setup|{currentTenant.Id?.ToString("N") ?? "host"}|{fingerprint}";
		if (await historyRepository.AnyAsync(history => history.TenantId == currentTenant.Id && history.DeduplicationKey == key)) return [];
		var row = new LibraryNotificationHistory(guidGenerator.Create(), currentTenant.Id, "automaticReminderRun",
			LibraryNotificationEventGroups.Transactions, "Automatic reminder setup", LibraryNotificationChannels.InApp,
			LibraryNotificationRecipientRoles.Librarian, null, null, null, "Automatic Library reminders need configuration", reason,
			LibraryNotificationDeliveryStatuses.Skipped, reason, null, null, key, null, null);
		authority.Capture(row, LibraryNotificationOrigin.Scheduled);
		await historyRepository.InsertAsync(row, true);
		return [row];
	}

	public static string BuildSettingKey(string eventType, string recipientRole, string channel) =>
		$"{eventType}|{recipientRole}|{channel}";

	private static string? BuildDeduplicationKey(LibraryNotificationDeliveryRequest request, LibraryNotificationRecipient recipient, string channel)
	{
		if (request.ReminderDate == null || request.RelatedEntityId == null)
		{
			return request.DeduplicationKey;
		}

		var recipientKey = LibraryNotificationRecipientResolver.DestinationSignature(null, recipient, channel)[..24];
		return $"{request.EventType}|{channel}|{recipientKey}|{request.RelatedEntityType}|{request.RelatedEntityId:N}|{request.ReminderDate:yyyyMMdd}";
	}

	private static string Render(string template, IReadOnlyDictionary<string, string?> placeholders)
	{
		return Regex.Replace(template ?? string.Empty, "\\{\\{\\s*(?<key>[A-Za-z0-9_]+)\\s*\\}\\}", match =>
		{
			var key = match.Groups["key"].Value;
			return placeholders.TryGetValue(key, out var value) ? value ?? string.Empty : match.Value;
		});
	}

	private static string Bound(string value, int limit)
	{
		if (value.Length <= limit) return value;
		if (char.IsHighSurrogate(value[limit - 1])) limit--;
		return value[..limit];
	}
}

public sealed record LibraryNotificationDeliveryRequest(
	string EventType,
	IReadOnlyCollection<string> Channels,
	IReadOnlyCollection<LibraryNotificationRecipient> Recipients,
	Dictionary<string, string?> Placeholders,
	string? SubjectOverride,
	string? MessageOverride,
	string? RelatedEntityType,
	Guid? RelatedEntityId,
	DateTime? ReminderDate,
	string? DeduplicationKey,
	LibraryNotificationOrigin? Origin = null,
	LibraryFineReminderEvidence? FineEvidence = null);
