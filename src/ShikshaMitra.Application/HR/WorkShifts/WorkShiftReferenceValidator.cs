using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
namespace ShikshaMitra.HR.WorkShifts;
public class WorkShiftReferenceValidator(IRepository<EmployeeWorkShift, Guid> shifts, ICurrentTenant tenant) : ITransientDependency
{
	public async Task EnsureExistsAsync(Guid? id)
	{
		if (!id.HasValue || !tenant.Id.HasValue || !await shifts.AnyAsync(x => x.Id == id && x.TenantId == tenant.Id && x.IsActive))
			throw new BusinessException(HRErrorCodes.WorkShiftInvalid, "Select an active employee work shift owned by this school.");
	}
}