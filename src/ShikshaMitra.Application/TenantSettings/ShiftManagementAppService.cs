using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Entities;
using ShikshaMitra.TenantSettings.Formatting;

namespace ShikshaMitra.TenantSettings;

[Authorize]
public class ShiftManagementAppService(
	IRepository<ShiftManagement, Guid> repository,
	IRepository<Standard, Guid> standardRepository)
	: CrudAppService<ShiftManagement, ShiftManagementDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateShiftManagementDto>(repository),
	IShiftManagementAppService
{
	protected override string GetPolicyName { get; set; } = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.InstituteSetup.ShiftManagement.View;
	protected override string GetListPolicyName { get; set; } = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.InstituteSetup.ShiftManagement.View;
	protected override string CreatePolicyName { get; set; } = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.InstituteSetup.ShiftManagement.Add;
	protected override string UpdatePolicyName { get; set; } = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.InstituteSetup.ShiftManagement.Edit;
	protected override string DeletePolicyName { get; set; } = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.InstituteSetup.ShiftManagement.Delete;
	protected override async Task<IQueryable<ShiftManagement>> CreateFilteredQueryAsync(PagedAndSortedResultRequestDto input)
	{
		return (await base.CreateFilteredQueryAsync(input)).Where(shift => shift.TenantId == CurrentTenant.Id);
	}

	protected override async Task<ShiftManagement> GetEntityByIdAsync(Guid id)
	{
		return await Repository.FindAsync(shift => shift.Id == id && shift.TenantId == CurrentTenant.Id)
			?? throw new EntityNotFoundException(typeof(ShiftManagement), id);
	}

	public override async Task<ShiftManagementDto> CreateAsync(CreateUpdateShiftManagementDto input)
	{
		await CheckCreatePolicyAsync();
		ValidateShiftTimeRange(input);
		var rules = await LazyServiceProvider.LazyGetRequiredService<InstituteFieldFormattingStore>().GetAsync("shifts");
		input.ShiftName = InstituteFieldFormattingCatalog.Required(rules, "shiftName", input.ShiftName);
		return await base.CreateAsync(input);
	}

	public override Task<ShiftManagementDto> UpdateAsync(Guid id, CreateUpdateShiftManagementDto input)
	{
		ValidateShiftTimeRange(input);
		return base.UpdateAsync(id, input);
	}

	public override async Task DeleteAsync(Guid id)
	{
		await GetEntityByIdAsync(id);
		var isAssignedToStandard = await standardRepository.AnyAsync(x => x.ShiftId == id);

		if (isAssignedToStandard)
		{
			throw new BusinessException(
				HRErrorCodes.AttendancePolicyEvaluationInvalidShift,
				"Shift cannot be deleted because it is assigned to classes.");
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