using System;
using System.Threading.Tasks;
using ShikshaMitra.HR;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.TenantSettings;

public class ShiftReferenceValidator(
	IRepository<ShiftManagement, Guid> shiftRepository,
	ICurrentTenant currentTenant) : ITransientDependency
{
	public async Task EnsureExistsAsync(Guid? shiftId)
	{
		if (!shiftId.HasValue)
		{
			return;
		}

		if (!await shiftRepository.AnyAsync(x => x.Id == shiftId.Value && x.TenantId == currentTenant.Id))
		{
			throw new BusinessException(
				HRErrorCodes.AttendancePolicyEvaluationShiftRequired,
				"Selected shift is no longer available. Please choose another shift.");
		}
	}
}
