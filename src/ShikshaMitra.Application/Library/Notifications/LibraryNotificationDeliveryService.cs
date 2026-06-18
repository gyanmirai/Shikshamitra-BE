using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Notifications;

public class LibraryNotificationDeliveryService(
	IRepository<LibraryNotificationSetting, Guid> settingRepository,
	IRepository<LibraryNotificationHistory, Guid> historyRepository,
	LibraryNotificationCatalog catalog,
	IEmailSender emailSender,
	IGuidGenerator guidGenerator,
	ICurrentTenant currentTenant,
	IAsyncQueryableExecuter asyncExecuter,
	ILogger<LibraryNotificationDeliveryService> logger)
	: ITransientDependency
{
	public async Task<List<LibraryNotificationHistory>> DeliverAsync(LibraryNotificationDeliveryRequest request)
	{
		var definition = catalog.Resolve(request.EventType);
		var settings = await EnsureSettingsAsync();
		var channels = request.Channels.Count == 0
			? new List<string> { LibraryNotificationChannels.InApp }
			: request.Channels.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		var delivered = new List<LibraryNotificationHistory>();

		foreach (var recipient in request.Recipients)
		{
			foreach (var channel in channels)
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
					["memberName"] = recipient.RecipientName,
					["recipientName"] = recipient.RecipientName,
					["recipientEmail"] = recipient.Email
				};
				var subject = Render(request.SubjectOverride ?? setting.SubjectTemplate, placeholders);
				var message = Render(request.MessageOverride ?? setting.MessageTemplate, placeholders);

				var status = LibraryNotificationDeliveryStatuses.Sent;
				string? error = null;
				DateTime? sentAt = DateTime.UtcNow;

				if (setting.IsComingSoon || !setting.IsChannelAvailable || !setting.IsActive)
				{
					status = LibraryNotificationDeliveryStatuses.Skipped;
					error = setting.IsComingSoon
						? "Notification event is coming soon."
						: !setting.IsChannelAvailable
							? "Notification channel is not available in this phase."
							: "Notification setting is disabled.";
					sentAt = null;
				}
				else if (!recipient.IsActiveLibraryMember)
				{
					status = LibraryNotificationDeliveryStatuses.Skipped;
					error = "Recipient is not an active library member.";
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
					else
					{
						try
						{
							await emailSender.SendAsync(recipient.Email, subject, message, isBodyHtml: false);
						}
						catch (Exception ex)
						{
							logger.LogWarning(ex, "Failed to send Library notification {EventType} to {RecipientEmail}.", definition.EventType, recipient.Email);
							status = LibraryNotificationDeliveryStatuses.Failed;
							error = ex.Message;
							sentAt = null;
						}
					}
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

				await historyRepository.InsertAsync(history, autoSave: true);
				delivered.Add(history);
			}
		}

		return delivered;
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

	public static string BuildSettingKey(string eventType, string recipientRole, string channel) =>
		$"{eventType}|{recipientRole}|{channel}";

	private static string? BuildDeduplicationKey(LibraryNotificationDeliveryRequest request, LibraryNotificationRecipient recipient, string channel)
	{
		if (request.ReminderDate == null || request.RelatedEntityId == null)
		{
			return request.DeduplicationKey;
		}

		var recipientKey = recipient.MemberId?.ToString("N") ?? recipient.RecipientRole;
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
	string? DeduplicationKey);
