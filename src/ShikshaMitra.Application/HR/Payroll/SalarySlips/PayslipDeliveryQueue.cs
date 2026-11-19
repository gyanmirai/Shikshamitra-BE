using System;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Users;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

public class PayslipDeliveryQueue(IRepository<PayslipDelivery, Guid> deliveries,
	IBackgroundJobManager jobs, IGuidGenerator ids, ICurrentUser user) : ITransientDependency
{
	public async Task<PayslipDelivery> QueueAsync(PayslipDocument document, Guid requestId, bool automatic,
		string requiredPermission = ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.Add)
	{
		if (requestId == Guid.Empty) throw new BusinessException(HRErrorCodes.PayslipConflict);
		var existing = await deliveries.FindAsync(x => x.DocumentId == document.Id && x.RequestId == requestId);
		if (existing != null) return existing;
		var delivery = new PayslipDelivery(ids.Create(), document.TenantId, document.Id,
			document.EmployeeId, requestId, automatic, user.Id, requiredPermission);
		await deliveries.InsertAsync(delivery, autoSave: false);
		await jobs.EnqueueAsync(new PayslipDeliveryJobArgs { TenantId = document.TenantId, DeliveryId = delivery.Id });
		return delivery;
	}
}
