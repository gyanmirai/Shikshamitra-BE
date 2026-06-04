using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Settings.ConcessionType;
using ShikshaMitra.Finance.Fees.Settings.ConcessionType.Dtos;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Fees.Settings;

[RemoteService]
[Area("app")]
[Route("api")]
[Authorize]
public class ConcessionTypeController(
	IConcessionTypeAppService concessionTypeAppService
) : AbpController
{
	[HttpPost("{academicYearId}/concession-types")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ConcessionType.Add)]
	public async Task<Result> CreateAsync(Guid academicYearId, ConcessionTypeRequest concessionTypeRequest)
	{
		await concessionTypeAppService.CreateAsync(academicYearId, concessionTypeRequest);
		return Result.Success("Concession type created successfully.");
	}

	[HttpGet("{academicYearId}/concession-types")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ConcessionType.View)]
	public async Task<Result<PagedResultDto<ConcessionTypeResponse>>> GetListAsync(Guid academicYearId, int skipCount = 0, int maxResultCount = 10, string? searchTerm = null)
	{
		var concessionTypes = await concessionTypeAppService.GetListAsync(academicYearId, skipCount, maxResultCount, searchTerm);
		return Result.Success(concessionTypes);
	}

	[HttpGet("concession-types/{concessionTypeId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ConcessionType.View)]
	public async Task<Result<ConcessionTypeResponse>> GetAsync(Guid concessionTypeId)
	{
		var concessionType = await concessionTypeAppService.GetAsync(concessionTypeId);
		return Result.Success(concessionType);
	}

	[HttpPut("concession-types/{concessionTypeId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ConcessionType.Add)]
	public async Task<Result> UpdateAsync(Guid concessionTypeId, ConcessionTypeRequest concessionTypeUpdateRequest)
	{
		await concessionTypeAppService.UpdateAsync(concessionTypeId, concessionTypeUpdateRequest);
		return Result.Success("Concession type updated successfully.");
	}

	[HttpDelete("concession-types/{concessionTypeId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ConcessionType.Delete)]
	public async Task<Result> DeleteAsync(Guid concessionTypeId)
	{
		await concessionTypeAppService.DeleteAsync(concessionTypeId);
		return Result.Success("Concession type deleted successfully.");
	}
}
