using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.TenantSettings.Divisions;

[Authorize]
public class DivisionAppService(
	IDivisionRepository divisionRepository,
	DivisionManager divisionManager,
	AcademicOrganizationScopeAccessService scopeAccessService) : CrudAppService<
		Division,
		DivisionDto,
		Guid,
		PagedAndSortedResultRequestDto,
		CreateUpdateDivisionDto>(divisionRepository),
	IDivisionAppService
{
	public override async Task<DivisionDto> CreateAsync(CreateUpdateDivisionDto input)
	{
		Division division = await divisionManager.CreateAsync(
			input.StandardId,
			input.Name,
			input.Strength,
			input.RoomNumber
		);

		await divisionRepository.InsertAsync(division);
		var dto = ObjectMapper.Map<Division, DivisionDto>(division);
		await scopeAccessService.ResolveRoomIdsAsync(new[] { dto });
		return dto;
	}

	public override async Task<DivisionDto> UpdateAsync(Guid id, CreateUpdateDivisionDto input)
	{
		Division division = await divisionRepository.GetAsync(id);
		await divisionManager.UpdateAsync(division, input.Name, input.Strength, input.RoomNumber);
		await divisionRepository.UpdateAsync(division);
		var dto = ObjectMapper.Map<Division, DivisionDto>(division);
		await scopeAccessService.ResolveRoomIdsAsync(new[] { dto });
		return dto;
	}

	public override async Task<DivisionDto> GetAsync(Guid id)
	{
		var dto = await base.GetAsync(id);
		await scopeAccessService.ResolveRoomIdsAsync(new[] { dto });
		return dto;
	}

	public override async Task<PagedResultDto<DivisionDto>> GetListAsync(
		PagedAndSortedResultRequestDto input)
	{
		var result = await base.GetListAsync(input);

		result.Items = (await scopeAccessService.FilterDivisionsAsync(result.Items)).ToList();
		await scopeAccessService.ResolveRoomIdsAsync(result.Items);
		result.TotalCount = result.Items.Count;

		return result;
	}
}
