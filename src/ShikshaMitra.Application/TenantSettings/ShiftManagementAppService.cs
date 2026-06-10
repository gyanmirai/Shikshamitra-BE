using System;
using System.Threading.Tasks;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.TenantSettings;

public class ShiftManagementAppService(
	IRepository<ShiftManagement, Guid> repository,
	IRepository<Employee, Guid> employeeRepository,
	IRepository<Standard, Guid> standardRepository)
	: CrudAppService<ShiftManagement, ShiftManagementDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateShiftManagementDto>(repository),
	IShiftManagementAppService
{
	public override Task<ShiftManagementDto> CreateAsync(CreateUpdateShiftManagementDto input)
	{
		ValidateShiftTimeRange(input);
		return base.CreateAsync(input);
	}

	public override Task<ShiftManagementDto> UpdateAsync(Guid id, CreateUpdateShiftManagementDto input)
	{
		ValidateShiftTimeRange(input);
		return base.UpdateAsync(id, input);
	}

	public override async Task DeleteAsync(Guid id)
	{
		var isAssignedToEmployee = await employeeRepository.AnyAsync(x => x.ShiftId == id);
		var isAssignedToStandard = await standardRepository.AnyAsync(x => x.ShiftId == id);

		if (isAssignedToEmployee || isAssignedToStandard)
		{
			throw new BusinessException(
				HRErrorCodes.AttendancePolicyEvaluationInvalidShift,
				"Shift cannot be deleted because it is assigned to employees or classes.");
		}

		await base.DeleteAsync(id);
	}

	private static void ValidateShiftTimeRange(CreateUpdateShiftManagementDto input)
	{
		if (input.EndTime <= input.StartTime)
		{
			throw new BusinessException(
				HRErrorCodes.AttendancePolicyEvaluationInvalidShift,
				"Shift end time must be after shift start time. Overnight shifts are not supported.");
		}
	}
}
