using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.HR.Calendar.Holidays;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Calendar.Holidays;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Application.HR.Calendar.Holidays;

[RemoteService(false)]
public class HolidayAppService : CrudAppService<
	Holiday,
	HolidayDto,
	Guid,
	GetHolidayListRequestDto,
	CreateUpdateHolidayDto>, IHolidayAppService
{
	private readonly HolidayReadService _holidayReadService;
	private readonly HolidayMutationWorkflow _mutationWorkflow;

	public HolidayAppService(
		IHolidayRepository holidayRepository,
		HolidayReadService holidayReadService,
		HolidayMutationWorkflow mutationWorkflow)
		: base(holidayRepository)
	{
		_holidayReadService = holidayReadService;
		_mutationWorkflow = mutationWorkflow;
		GetPolicyName = ShikshaMitraSubmodulePermissions.HR.Calendar.View;
		GetListPolicyName = ShikshaMitraSubmodulePermissions.HR.Calendar.View;
		CreatePolicyName = ShikshaMitraSubmodulePermissions.HR.Calendar.Add;
		UpdatePolicyName = ShikshaMitraSubmodulePermissions.HR.Calendar.Add;
		DeletePolicyName = ShikshaMitraSubmodulePermissions.HR.Calendar.Delete;
	}

	public override Task<PagedResultDto<HolidayDto>> GetListAsync(GetHolidayListRequestDto input) =>
		_holidayReadService.GetListAsync(input);

	public Task<PagedResultDto<HolidayDto>> GetListByNameAsync(GetHolidayListRequestDto input) =>
		_holidayReadService.GetListByNameAsync(input);

	public Task<List<HolidayDto>> GetAllHolidaysAsync() =>
		_holidayReadService.GetAllHolidaysAsync();

	public override Task<HolidayDto> GetAsync(Guid id) =>
		_holidayReadService.GetAsync(id);

	public Task<List<HolidayDto>> GetByTypeAsync(Guid typeId) =>
		_holidayReadService.GetByTypeAsync(typeId);

	public Task<PagedResultDto<HolidayDto>> GetByStatusAsync(GetHolidayListRequestDto input) =>
		_holidayReadService.GetByStatusAsync(input);

	public override Task<HolidayDto> CreateAsync(CreateUpdateHolidayDto input) =>
		_mutationWorkflow.CreateAsync(input);

	public override Task<HolidayDto> UpdateAsync(Guid id, CreateUpdateHolidayDto input) =>
		_mutationWorkflow.UpdateAsync(id, input);
}
