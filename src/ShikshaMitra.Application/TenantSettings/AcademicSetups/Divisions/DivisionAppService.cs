using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using Volo.Abp.Authorization;
using Volo.Abp.Uow;

namespace ShikshaMitra.TenantSettings.Divisions;

[Authorize]
public class DivisionAppService(
	IDivisionRepository divisionRepository,
	DivisionManager divisionManager,
	IStandardRepository standardRepository,
	AcademicReorderAccessService reorderAccessService,
	AcademicOrganizationScopeAccessService scopeAccessService,
	AcademicHierarchyQueryScope queryScope,
	AcademicHierarchyReader hierarchy) : CrudAppService<
		Division,
		DivisionDto,
		Guid,
		PagedAndSortedResultRequestDto,
		CreateUpdateDivisionDto>(divisionRepository),
	IDivisionAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.Edit)]
	[UnitOfWork(true)]
	public async Task ReorderAsync(Guid standardId, ReorderAcademicChildrenDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.Edit);
		var standard = await standardRepository.GetAsync(standardId, includeDetails: false);
		var siblings = await divisionRepository.GetListAsync(x => x.StandardId == standardId && x.TenantId == CurrentTenant.Id);
		await reorderAccessService.CheckDivisionsAsync(standard, siblings);
		divisionManager.Reorder(siblings, input.Ids);
		await divisionRepository.UpdateManyAsync(siblings, autoSave: true);
	}

	protected override IQueryable<Division> ApplySorting(IQueryable<Division> query, PagedAndSortedResultRequestDto input) =>
		string.IsNullOrWhiteSpace(input.Sorting)
			? query.OrderBy(x => x.StandardId).ThenBy(x => x.SortOrder).ThenBy(x => x.Name).ThenBy(x => x.Id)
			: base.ApplySorting(query, input);

	public override async Task<DivisionDto> CreateAsync(CreateUpdateDivisionDto input)
	{
		Division division = await divisionManager.CreateAsync(
			input.StandardId,
			input.Name,
			input.Strength,
			input.RoomNumber ?? string.Empty
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
		await CheckGetPolicyAsync();
		var query = await queryScope.DivisionsAsync(await divisionRepository.GetQueryableAsync());
		return (await hierarchy.ReadDivisionsAsync(query.Where(x => x.Id == id))).FirstOrDefault()
			?? throw new Volo.Abp.Authorization.AbpAuthorizationException();
	}

	public override async Task<PagedResultDto<DivisionDto>> GetListAsync(
		PagedAndSortedResultRequestDto input)
	{
		await CheckGetListPolicyAsync();
		var query = await queryScope.DivisionsAsync(await base.CreateFilteredQueryAsync(input));
		var total = await AsyncExecuter.CountAsync(query);
		var items = await hierarchy.ReadDivisionsAsync(ApplyPaging(ApplySorting(query, input), input));
		return new(total, items);
	}
}
