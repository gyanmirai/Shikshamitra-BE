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
[Area("finance")]
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
	public async Task<Result<PagedResultDto<ConcessionTypeResponse>>> GetListAsync(
		Guid academicYearId,
		int skipCount = 0,
		int maxResultCount = 10,
		string? searchTerm = null,
		bool includeInactive = false,
		bool? isActive = null,
		[FromQuery] string? filters = null)
	{
		var concessionTypes = await concessionTypeAppService.GetListAsync(
			academicYearId, skipCount, maxResultCount, searchTerm, includeInactive, isActive, filters);
		return Result.Success(concessionTypes);
	}

	[HttpGet("concession-types/document-defaults")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ConcessionType.View)]
	public async Task<Result<ConcessionTypeDocumentDefaultsResponse>> GetDocumentDefaultsAsync()
	{
		return Result.Success(await concessionTypeAppService.GetDocumentDefaultsAsync());
	}

	[HttpGet("concession-types/{concessionTypeId:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ConcessionType.View)]
	public async Task<Result<ConcessionTypeResponse>> GetAsync(Guid concessionTypeId)
	{
		var concessionType = await concessionTypeAppService.GetAsync(concessionTypeId);
		return Result.Success(concessionType);
	}

	[HttpGet("concession-types/{concessionTypeId:guid}/versions")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ConcessionType.View)]
	public async Task<Result<PagedResultDto<ConcessionTypeVersionHistoryItemDto>>> GetVersionHistoryAsync(
		Guid concessionTypeId,
		int skipCount = 0,
		int maxResultCount = 50)
	{
		var history = await concessionTypeAppService.GetVersionHistoryAsync(
			concessionTypeId, skipCount, maxResultCount);
		return Result.Success(history);
	}

	[HttpPut("concession-types/{concessionTypeId:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ConcessionType.Edit)]
	public async Task<Result> UpdateAsync(Guid concessionTypeId, ConcessionTypeRequest concessionTypeUpdateRequest)
	{
		await concessionTypeAppService.UpdateAsync(concessionTypeId, concessionTypeUpdateRequest);
		return Result.Success("Concession type updated successfully.");
	}

	[HttpPatch("concession-types/{concessionTypeId:guid}/status")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ConcessionType.ChangeStatus)]
	public async Task<Result<ConcessionTypeResponse>> UpdateStatusAsync(
		Guid concessionTypeId,
		ConcessionTypeStatusUpdateRequest input)
	{
		return Result.Success(await concessionTypeAppService.UpdateStatusAsync(concessionTypeId, input));
	}

	[HttpDelete("concession-types/{concessionTypeId:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ConcessionType.Delete)]
	public async Task<Result> DeleteAsync(Guid concessionTypeId)
	{
		await concessionTypeAppService.DeleteAsync(concessionTypeId);
		return Result.Success("Concession type deleted successfully.");
	}
}
