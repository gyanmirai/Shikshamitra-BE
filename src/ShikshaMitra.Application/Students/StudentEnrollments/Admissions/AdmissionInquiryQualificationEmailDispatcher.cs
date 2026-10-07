using System;
using System.Net.Mail;
using System.Threading.Tasks;
using ShikshaMitra.Application.Communication;
using ShikshaMitra.Communication;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionInquiryQualificationEmailDispatcher(
	IRepository<AdmissionInquiry, Guid> inquiries, IRepository<AdmissionCycle, Guid> cycles,
	CommunicationAppService communications, IUnitOfWorkManager units, ICurrentTenant tenant) : ITransientDependency
{
	public static bool IsDue(AdmissionInquiry inquiry, DateTime now)
	{
		if (!inquiry.GetProperty<DateTime?>("QualificationEmailRequestedAt").HasValue) return false;
		var status = inquiry.GetProperty<string>("QualificationEmailStatus");
		if (status is "Sent" or "Queued" or "Skipped" or "Failed" or "NeedsReview") return false;
		var next = inquiry.GetProperty<DateTime?>("QualificationEmailNextAttemptAt");
		return !next.HasValue || next <= now;
	}

	public async Task DispatchAsync(Guid id)
	{
		Guid campaignId;
		using (var prepare = units.Begin(requiresNew: true, isTransactional: true))
		{
			var inquiry = await inquiries.GetAsync(id);
			if (inquiry.TenantId != tenant.Id || !IsDue(inquiry, DateTime.UtcNow)) return;
			if (inquiry.GetProperty<string>("QualificationEmailStatus") == "Processing")
			{
				// Transport may have succeeded before a process interruption. Never automatically resend it.
				SetState(inquiry, "NeedsReview", "Email delivery was interrupted. Review Communication history before retrying.");
				await inquiries.UpdateAsync(inquiry, false); await prepare.CompleteAsync(); return;
			}
			if (AdmissionInquiryStatusRules.Display(inquiry.Status) != AdmissionInquiryStatus.Interested)
			{
				SetState(inquiry, "Skipped", "Inquiry is no longer Interested.");
				await inquiries.UpdateAsync(inquiry, false); await prepare.CompleteAsync(); return;
			}
			if (!MailAddress.TryCreate(inquiry.Email, out var address) || address.Address != inquiry.Email)
			{
				SetState(inquiry, "Skipped", "No valid contact email was captured in the inquiry.");
				await inquiries.UpdateAsync(inquiry, false); await prepare.CompleteAsync(); return;
			}
			var campaign = await communications.PrepareInquiryQualificationAsync(inquiry,
				await cycles.GetAsync(inquiry.AdmissionCycleId));
			campaignId = campaign.Id;
			inquiry.SetProperty("QualificationEmailCampaignId", campaignId);
			inquiry.SetProperty("QualificationEmailRecipient", inquiry.Email);
			SetState(inquiry, "Processing", "Qualification email is being processed.");
			inquiry.SetProperty("QualificationEmailNextAttemptAt", DateTime.UtcNow.AddMinutes(10));
			await inquiries.UpdateAsync(inquiry, false);
			// The optimistic concurrency stamp commits the claim before any transport runs.
			await prepare.CompleteAsync();
		}
		using var delivery = units.Begin(requiresNew: true, isTransactional: true);
		var current = await inquiries.GetAsync(id);
		try
		{
			if (AdmissionInquiryStatusRules.Display(current.Status) != AdmissionInquiryStatus.Interested ||
				!string.Equals(current.Email, current.GetProperty<string>("QualificationEmailRecipient"), StringComparison.OrdinalIgnoreCase))
			{
				SetState(current, "Skipped", "Inquiry interest or contact email changed before delivery.");
				await inquiries.UpdateAsync(current, false); await delivery.CompleteAsync(); return;
			}
			var campaign = await communications.PublishInquiryQualificationAsync(campaignId, id);
			var status = campaign.Status switch
			{
				CommunicationCampaignStatus.PendingApproval => "PendingApproval",
				CommunicationCampaignStatus.Sent => "Sent",
				CommunicationCampaignStatus.Failed or CommunicationCampaignStatus.PartiallyFailed => "Failed",
				CommunicationCampaignStatus.Cancelled => "Skipped",
				_ => "Queued"
			};
			SetState(current, status, status switch
			{
				"PendingApproval" => "Qualification email is awaiting Communication approval.",
				"Failed" => "Email delivery failed or was skipped. Review Communication delivery history.",
				"Skipped" => "Qualification email was cancelled in Communication.",
				_ => "Qualification email handed to Communication."
			});
			current.SetProperty("QualificationEmailNextAttemptAt", DateTime.UtcNow.AddMinutes(5));
		}
		catch (BusinessException error) when (error.Code is "Communication:ProviderMissing" or "Communication:ApprovalRequired")
		{
			SetState(current, error.Code == "Communication:ApprovalRequired" ? "PendingApproval" : "Pending",
				"Qualification email is waiting for Communication provider or approval settings.");
			current.SetProperty("QualificationEmailNextAttemptAt", DateTime.UtcNow.AddMinutes(5));
		}
		catch (Exception)
		{
			SetState(current, "NeedsReview", "Email delivery could not complete. Review Communication history before retrying.");
		}
		await inquiries.UpdateAsync(current, false); await delivery.CompleteAsync();
	}

	private static void SetState(AdmissionInquiry inquiry, string status, string message)
	{
		inquiry.SetProperty("QualificationEmailStatus", status);
		inquiry.SetProperty("QualificationEmailMessage", message);
	}
}
