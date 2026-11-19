using System;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

public class PayslipDeliveryBackgroundJob(ICurrentTenant tenant, IUnitOfWorkManager units,
	IPayrollInputRevisionStore fence, IRepository<PayslipDelivery, Guid> deliveries,
	IRepository<PayslipDocument, Guid> documents, IEmployeeRepository employees,
	ISalarySlipRepository slips, PayslipIssuanceService issuance, IEmailSender emailSender, IClock clock,
	IBackgroundJobManager jobs, IdentityUserManager users, ICurrentPrincipalAccessor principals,
	IPermissionChecker permissions, SalarySlipAccessService access, ILogger<PayslipDeliveryBackgroundJob> logger)
	: AsyncBackgroundJob<PayslipDeliveryJobArgs>, ITransientDependency
{
	public override async Task ExecuteAsync(PayslipDeliveryJobArgs args)
	{
		using var tenantScope = tenant.Change(args.TenantId);
		Guid documentId;
		using (var claim = units.Begin(requiresNew: true, isTransactional: true))
		{
			await fence.LockAsync();
			var delivery = await deliveries.GetAsync(args.DeliveryId);
			if (delivery.TenantId != args.TenantId) return;
			if (delivery.Status == "PROCESSING")
			{
				// A fresh worker may still own this attempt. Never steal its SMTP send.
				if (delivery.StartedAt?.ToUniversalTime() > clock.Now.ToUniversalTime().AddMinutes(-30)) return;
				delivery.Finish("UNCERTAIN", clock.Now, "SEND_OUTCOME_UNKNOWN");
				await deliveries.UpdateAsync(delivery);
				await claim.CompleteAsync();
				return;
			}
			if (delivery.Status != "QUEUED") return;
			if (!await IsAuthorizedAsync(delivery))
			{
				delivery.Finish("FAILED", clock.Now, "ACCESS_REVOKED");
				await deliveries.UpdateAsync(delivery);
				await claim.CompleteAsync();
				return;
			}
			var document = await documents.GetAsync(delivery.DocumentId);
			var slip = await slips.GetAsync(document.SalarySlipId);
			if (slip.Status is not (SalarySlipStatus.Finalized or SalarySlipStatus.Paid) ||
				(await issuance.GetCurrentAsync(slip.Id)).Id != document.Id)
			{
				delivery.Finish("SUPERSEDED", clock.Now, "DOCUMENT_REPLACED");
				await deliveries.UpdateAsync(delivery);
				await claim.CompleteAsync();
				return;
			}
			var employee = await employees.GetAsync(delivery.EmployeeId, includeDetails: false);
			if (!MailAddress.TryCreate(employee.Email, out var recipient) || recipient.Address != employee.Email?.Trim())
			{
				delivery.Finish("FAILED", clock.Now, "EMPLOYEE_EMAIL_REQUIRED");
				await deliveries.UpdateAsync(delivery);
				await claim.CompleteAsync();
				return;
			}
			delivery.Start(recipient.Address, clock.Now);
			documentId = document.Id;
			await deliveries.UpdateAsync(delivery);
			await jobs.EnqueueAsync(new PayslipDeliveryJobArgs { TenantId = args.TenantId, DeliveryId = args.DeliveryId },
				delay: TimeSpan.FromMinutes(31));
			await claim.CompleteAsync();
		}

		// Persisted PROCESSING survives a crash. Hold the fence while checking currency and sending,
		// so a replacement cannot commit between that check and the provider call.
		using var send = units.Begin(requiresNew: true, isTransactional: true);
		await fence.LockAsync();
		var attempt = await deliveries.GetAsync(args.DeliveryId);
		if (attempt.Status != "PROCESSING") return;
		var issued = await documents.GetAsync(documentId);
		var currentEmployee = await employees.GetAsync(attempt.EmployeeId, includeDetails: false);
		if (!await IsAuthorizedAsync(attempt))
		{
			attempt.Finish("FAILED", clock.Now, "ACCESS_REVOKED");
		}
		else if ((await issuance.GetCurrentAsync(issued.SalarySlipId)).Id != issued.Id)
		{
			attempt.Finish("SUPERSEDED", clock.Now, "DOCUMENT_REPLACED");
		}
		else if (currentEmployee.Email?.Trim() != attempt.Recipient)
		{
			attempt.Finish("FAILED", clock.Now, "RECIPIENT_CHANGED");
		}
		else
		{
			using var message = new MailMessage();
			message.To.Add(attempt.Recipient!);
			message.Subject = $"Payslip {issued.MonthYear}";
			message.Body = "Your finalized payslip is attached. Please contact HR for any discrepancy.";
			message.Attachments.Add(new Attachment(new MemoryStream(issued.Pdf, writable: false),
				$"Payslip_{issued.MonthYear}_R{issued.Revision}.pdf", "application/pdf"));
			try
			{
				await emailSender.SendAsync(message);
				attempt.Finish("PROVIDER_ACCEPTED", clock.Now);
			}
			catch (Exception exception)
			{
				// SMTP exceptions can occur after acceptance. Do not infer that resending is safe.
				// Provider messages can contain credentials or document facts. Log only the exception class.
				logger.LogWarning("Payslip email transport failed with {ExceptionType}.", exception.GetType().Name);
				attempt.Finish("UNCERTAIN", clock.Now, "SEND_OUTCOME_UNKNOWN");
			}
		}
		await deliveries.UpdateAsync(attempt);
		await send.CompleteAsync();
	}

	private async Task<bool> IsAuthorizedAsync(PayslipDelivery attempt)
	{
		if (!attempt.RequestedBy.HasValue || attempt.RequiredPermission is not
			(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.Add or
			ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.Lock)) return false;
		var user = await users.FindByIdAsync(attempt.RequestedBy.Value.ToString());
		if (user == null || !user.IsActive || user.TenantId != attempt.TenantId) return false;
		var claims = new System.Collections.Generic.List<Claim>
		{
			new(AbpClaimTypes.UserId, user.Id.ToString()), new(AbpClaimTypes.UserName, user.UserName)
		};
		if (attempt.TenantId.HasValue) claims.Add(new Claim(AbpClaimTypes.TenantId, attempt.TenantId.Value.ToString()));
		claims.AddRange((await users.GetRolesAsync(user)).Select(role => new Claim(AbpClaimTypes.Role, role)));
		using var principal = principals.Change(new ClaimsPrincipal(new ClaimsIdentity(claims, "PayslipDelivery",
			AbpClaimTypes.UserName, AbpClaimTypes.Role)));
		if (!await permissions.IsGrantedAsync(attempt.RequiredPermission)) return false;
		try { await access.EnsureEmployeeScopeAllowedAsync(attempt.EmployeeId); }
		catch (BusinessException) { return false; }
		return true;
	}
}
