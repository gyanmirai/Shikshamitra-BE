using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

[RemoteService(false)]
public class PayslipDeliveryAppService(IRepository<PayslipDocument, Guid> documents,
	IRepository<PayslipDelivery, Guid> deliveries, ISalarySlipRepository slips,
	PayslipIssuanceService issuance, SalarySlipAccessService access,
	IPayrollInputRevisionStore fence, PayslipDeliveryQueue queue,
	IRepository<IdentityUser, Guid> identityUsers) : ApplicationService, IPayslipDeliveryAppService
{
	[UnitOfWork(true)]
	public virtual async Task<PayslipDeliveryDto> SendAsync(SendPayslipDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.Add);
		await fence.LockAsync();
		var document = await documents.GetAsync(input.DocumentId);
		await access.EnsureEmployeeScopeAllowedAsync(document.EmployeeId);
		var slip = await slips.GetAsync(document.SalarySlipId);
		if (slip.Status is not (SalarySlipStatus.Finalized or SalarySlipStatus.Paid) ||
			(await issuance.GetCurrentAsync(slip.Id)).Id != document.Id)
			throw new BusinessException(HRErrorCodes.PayslipConflict);
		var delivery = await queue.QueueAsync(document, input.IdempotencyKey, automatic: false);
		var requesterNames = await GetRequesterNamesAsync([delivery]);
		return ToDto(delivery, document.Revision, requesterNames.GetValueOrDefault(delivery.RequestedBy ?? Guid.Empty));
	}

	public async Task<List<PayslipDeliveryDto>> GetAsync(Guid salarySlipId)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.View);
		var slip = await slips.GetAsync(salarySlipId);
		await access.EnsureEmployeeScopeAllowedAsync(slip.EmployeeId);
		var documentQuery = await documents.GetQueryableAsync();
		var versions = await AsyncExecuter.ToListAsync(documentQuery.Where(x => x.SalarySlipId == salarySlipId)
			.Select(x => new { x.Id, x.Revision }));
		var ids = versions.Select(x => x.Id).ToList();
		var attempts = await deliveries.GetListAsync(x => ids.Contains(x.DocumentId));
		var requesterNames = await GetRequesterNamesAsync(attempts);
		return attempts.OrderByDescending(x => x.CreationTime)
			.Select(x => ToDto(x, versions.Single(v => v.Id == x.DocumentId).Revision,
				requesterNames.GetValueOrDefault(x.RequestedBy ?? Guid.Empty))).ToList();
	}

	private async Task<Dictionary<Guid, string>> GetRequesterNamesAsync(IEnumerable<PayslipDelivery> attempts)
	{
		var actorIds = attempts.Where(x => x.RequestedBy.HasValue).Select(x => x.RequestedBy!.Value).Distinct().ToList();
		if (actorIds.Count == 0) return [];
		var tenantId = CurrentTenant.Id;
		var query = await identityUsers.GetQueryableAsync();
		var actors = await AsyncExecuter.ToListAsync(query.Where(x => x.TenantId == tenantId && Enumerable.Contains(actorIds, x.Id))
			.Select(x => new { x.Id, x.Name, x.Surname, x.UserName }));
		return actors.ToDictionary(x => x.Id, x =>
		{
			var name = string.Join(" ", new[] { x.Name, x.Surname }.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!.Trim()));
			return string.IsNullOrWhiteSpace(name) ? x.UserName : name;
		});
	}

	private static PayslipDeliveryDto ToDto(PayslipDelivery delivery, int revision, string? requesterName) => new(delivery.Id,
		delivery.DocumentId, revision, delivery.Recipient, delivery.Status, delivery.ErrorCategory, delivery.Automatic,
		delivery.RequestedBy, delivery.CreationTime, delivery.StartedAt, delivery.CompletedAt)
	{ RequestedByDisplayName = requesterName, IdempotencyKey = delivery.RequestId };
}
