using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.TenantSettings.Standards;

[Authorize]
public class StandardAppService(
	IStandardRepository standardRepository,
	StandardManager standardManager) : CrudAppService<
	Standard,
	StandardDto,
	Guid,
	PagedAndSortedResultRequestDto,
	CreateUpdateStandardDto>(standardRepository), IStandardAppService
{
	public override async Task<StandardDto> CreateAsync(CreateUpdateStandardDto input)
	{
		Standard standard = await standardManager.CreateAsync(
			input.EducationMediumStageId,
			input.Name
		);

		await standardRepository.InsertAsync(standard);
		return ObjectMapper.Map<Standard, StandardDto>(standard);
	}

	public override async Task<StandardDto> UpdateAsync(Guid id, CreateUpdateStandardDto input)
	{
		Standard standard = await standardRepository.GetAsync(id);
		await standardManager.UpdateAsync(standard, input.Name);
		return ObjectMapper.Map<Standard, StandardDto>(standard);
	}
}