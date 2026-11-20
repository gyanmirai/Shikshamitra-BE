using System;
using System.Net.Mail;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.Library.Notifications;

public class LibraryNotificationDeliveryJobArgs
{
	public Guid? TenantId { get; set; }
	public Guid HistoryId { get; set; }
}

public class LibraryNotificationDeliveryJob(
	IRepository<LibraryNotificationHistory, Guid> histories,
	IRepository<LibraryNotificationSetting, Guid> settings,
	ILibraryNotificationRecipientResolver recipients,
	ILibraryNotificationAuthorityService authority,
	ILibraryNotificationReminderEligibility eligibility,
	ILibraryInventoryLock inventoryLock,
	IEmailSender sender,
	IBackgroundJobManager jobs,
	IUnitOfWorkManager units,
	ICurrentTenant tenant,
	IClock clock) : AsyncBackgroundJob<LibraryNotificationDeliveryJobArgs>, ITransientDependency
{
	public override async Task ExecuteAsync(LibraryNotificationDeliveryJobArgs args)
	{
		using var tenantScope = tenant.Change(args.TenantId);
		var claimId = Guid.NewGuid();
		string destination;
		string subject;
		string message;
		using (var claim = units.Begin(requiresNew: true, isTransactional: true))
		{
			await inventoryLock.AcquireAsync(tenant.Id);
			var row = await histories.FindAsync(args.HistoryId);
			if (row == null || row.TenantId != args.TenantId) return;
			var now = clock.Now.ToUniversalTime();
			if (row.Status == LibraryNotificationDeliveryStatuses.Processing)
			{
				row.RecoverInterrupted(now);
				await histories.UpdateAsync(row, autoSave: false);
				await claim.CompleteAsync();
				return;
			}
			if (!row.IsDue(now)) return;
			var blocked = await DeliveryBlockedReasonAsync(row);
			if (blocked != null)
			{
				row.MarkSkipped(blocked);
				await histories.UpdateAsync(row, autoSave: false);
				await claim.CompleteAsync();
				return;
			}
			row.Claim(now, claimId);
			destination = row.RecipientEmail!;
			subject = row.Subject;
			message = row.Message;
			await histories.UpdateAsync(row, autoSave: false);
			// A restart after transport acceptance must become uncertain, never an automatic duplicate send.
			await jobs.EnqueueAsync(args, delay: TimeSpan.FromMinutes(11));
			await claim.CompleteAsync();
		}

		using (var verify = units.Begin(requiresNew: true, isTransactional: true))
		{
			var row = await histories.FindAsync(args.HistoryId);
			if (row == null || !row.HasClaim(claimId)) return;
			var blocked = await DeliveryBlockedReasonAsync(row);
			if (blocked != null)
			{
				row.MarkSkipped(blocked);
				await histories.UpdateAsync(row, false);
				await verify.CompleteAsync();
				return;
			}
			await verify.CompleteAsync();
		}

		var accepted = false;
		var unknown = false;
		var transient = false;
		try
		{
			await sender.SendAsync(destination, subject, message, isBodyHtml: false);
			accepted = true;
		}
		catch (SmtpException error) when ((int)error.StatusCode >= 400)
		{
			transient = (int)error.StatusCode < 500;
		}
		catch (Exception)
		{
			unknown = true;
		}

		using var finish = units.Begin(requiresNew: true, isTransactional: true);
		await inventoryLock.AcquireAsync(tenant.Id);
		var current = await histories.FindAsync(args.HistoryId);
		if (current == null || current.TenantId != args.TenantId || !current.HasClaim(claimId)) return;
		var completedAt = clock.Now.ToUniversalTime();
		if (accepted) current.MarkSent(completedAt);
		else current.RecordFailure(completedAt, unknown, transient);
		await histories.UpdateAsync(current, autoSave: false);
		if (current.Status == LibraryNotificationDeliveryStatuses.Queued)
			await jobs.EnqueueAsync(args, delay: current.NextAttemptAt!.Value - completedAt);
		await finish.CompleteAsync();
	}

	private async Task<string?> DeliveryBlockedReasonAsync(LibraryNotificationHistory row)
	{
		if (!await authority.CanDispatchAsync(row))
			return "The initiating user's current access no longer permits this notification. Send a new notification with current authorization.";
		if (row.Channel != LibraryNotificationChannels.Email || string.IsNullOrWhiteSpace(row.RecipientEmail))
			return "Recipient email is missing or the delivery channel is unavailable.";
		var setting = await settings.FindAsync(x => x.EventType == row.EventType &&
			x.RecipientRole == row.RecipientRole && x.Channel == row.Channel);
		if (setting == null || !setting.IsActive || !setting.IsChannelAvailable || setting.IsComingSoon)
			return "Notification setting is disabled or unavailable.";
		if (!await recipients.IsCurrentDestinationAsync(row))
			return "Library membership, recipient association or the saved contact changed. Review the current recipient before sending.";
		if (await recipients.HasOptedOutAsync(row)) return "The recipient has opted out of this email destination.";
		return await eligibility.IsCurrentAsync(row) ? null : "The related Library reminder is no longer due.";
	}
}
