using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using ShikshaMitra.Application.Communication;
using Volo.Abp;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionParentRegistrationDeliveryArgs { public Guid? TenantId { get; set; } public Guid RegistrationId { get; set; } }

public class AdmissionParentRegistrationDeliveryJob(AdmissionParentRegistrationData data, AdmissionParentRegistrationAccess access,
	CommunicationAppService communication, IDataProtectionProvider protection, IConfiguration configuration,
	IUnitOfWorkManager units, ICurrentTenant tenant, IBackgroundJobManager jobs) : AsyncBackgroundJob<AdmissionParentRegistrationDeliveryArgs>, ITransientDependency
{
	public override async Task ExecuteAsync(AdmissionParentRegistrationDeliveryArgs args)
	{
		using var tenantScope = tenant.Change(args.TenantId);
		using (var claim = units.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = System.Data.IsolationLevel.Serializable }, requiresNew: true))
		{
			var grant = await data.Repo<AdmissionParentRegistration>().GetAsync(args.RegistrationId);
			if (grant.TenantId != args.TenantId) return;
			if (grant.DeliveryStatus == "Sending")
			{
				if (grant.DeliveryStartedAt > DateTime.UtcNow.AddMinutes(-30)) return;
				grant.SetDelivery("Unknown", "Sending was interrupted. Resend a new link after reviewing the delivery history.");
				await data.Repo<AdmissionParentRegistration>().UpdateAsync(grant, false);
				await claim.CompleteAsync(); return;
			}
			if (grant.DeliveryStatus != "Pending") return;
			try
			{
				var application = await access.AuthorizeAsync(grant);
				var recipient = JsonSerializer.Deserialize<AdmissionParentRecipientDto>(grant.RecipientJson)!;
				var cycle = await data.Repo<AdmissionCycle>().GetAsync(application.AdmissionCycleId);
				var campaignId = await communication.PrepareParentRegistrationAsync(grant, application, cycle, recipient);
				grant.SetDelivery("Sending", campaignId: campaignId, startedAt: DateTime.UtcNow);
				await jobs.EnqueueAsync(args, delay: TimeSpan.FromMinutes(31));
			}
			catch (BusinessException error) { grant.SetDelivery("Failed", CommunicationAppService.ParentRegistrationDeliveryError(error.Code)); }
			await data.Repo<AdmissionParentRegistration>().UpdateAsync(grant, false);
			await claim.CompleteAsync();
			if (grant.DeliveryStatus != "Sending") return;
		}
		using var sending = units.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = System.Data.IsolationLevel.Serializable }, requiresNew: true);
		var row = await data.Repo<AdmissionParentRegistration>().GetAsync(args.RegistrationId);
		if (row.DeliveryStatus != "Sending") return;
		try
		{
			var application = await access.AuthorizeAsync(row);
			var baseUrl = configuration["App:ClientUrl"] ?? configuration["App:AngularUrl"];
			if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
				throw new BusinessException("Communication:ClientUrlMissing");
			var token = protection.CreateProtector("AdmissionParentRegistration.v1").Unprotect(row.ProtectedToken);
			var link = baseUrl!.TrimEnd('/') + "/admission-registration#token=" + token;
			var result = await communication.PublishParentRegistrationAsync(row.CampaignId!.Value, row, application,
				JsonSerializer.Deserialize<AdmissionParentRecipientDto>(row.RecipientJson)!, link);
			row.SetDelivery(result.Status, result.Error);
		}
		catch (BusinessException error)
		{
			row.SetDelivery(error.Code == "Communication:ApprovalRequired" ? "PendingApproval" : "Failed", CommunicationAppService.ParentRegistrationDeliveryError(error.Code));
		}
		catch (Exception) { row.SetDelivery("Unknown", "Provider acceptance could not be confirmed. Review delivery history before resending a new link."); }
		await data.Repo<AdmissionParentRegistration>().UpdateAsync(row, false);
		await sending.CompleteAsync();
	}
}
