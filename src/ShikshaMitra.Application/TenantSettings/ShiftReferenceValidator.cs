using System;
using System.Threading.Tasks;
using ShikshaMitra.HR;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.TenantSettings;

public class ShiftReferenceValidator(
	IRepository<ShiftManagement, Guid> shiftRepository) : ITransientDependency
{
	public async Task EnsureExistsAsync(Guid? shiftId)
	{
		if (!shiftId.HasValue)
		{
			return;
		}

		if (!await shiftRepository.AnyAsync(x => x.Id == shiftId.Value))
		{
			throw new BusinessException(
				HRErrorCodes.AttendancePolicyEvaluationShiftRequired,
				"Selected shift is no longer available. Please choose another shift.");
		}
	}
}
