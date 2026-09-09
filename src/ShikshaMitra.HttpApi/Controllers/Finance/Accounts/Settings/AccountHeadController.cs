using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Settings.Dtos;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Accounts.Settings;

[RemoteService]
[Area("finance")]
[Route("api/account-heads")]
[Authorize]
public class AccountHeadController(IAccountHeadAppService service) : AbpController
{
	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_AccountHead.Add)]
	public Task<AccountHeadResponse> CreateAsync([FromQuery] Guid academicYearId, [FromBody] AccountHeadUpsertRequest input) =>
		service.CreateAsync(academicYearId, input);

	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_AccountHead.View)]
	public Task<PagedResultDto<AccountHeadResponse>> GetListAsync([FromQuery] Guid academicYearId,
		[FromQuery] AccountHeadType? headType = null, [FromQuery] int page = 1,
		[FromQuery] int pageSize = 10, [FromQuery] string? search = null,
		[FromQuery] bool includeInactive = true) =>
		service.GetListAsync(academicYearId, headType, page, pageSize, search, includeInactive);

	[HttpGet("options")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_AccountHead.View)]
	public Task<List<AccountHeadOptionDto>> GetOptionsAsync([FromQuery] Guid academicYearId,
		[FromQuery] AccountHeadType? headType = null) => service.GetOptionsAsync(academicYearId, headType);

	[HttpGet("{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_AccountHead.View)]
	public Task<AccountHeadResponse> GetAsync(Guid id) => service.GetAsync(id);

	[HttpGet("{id:guid}/versions")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_AccountHead.View)]
	public Task<PagedResultDto<AccountSettingVersionHistoryItemDto>> GetVersionHistoryAsync(Guid id,
		[FromQuery] int skipCount = 0, [FromQuery] int maxResultCount = 50) =>
		service.GetVersionHistoryAsync(id, skipCount, maxResultCount);

	[HttpPut("{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_AccountHead.Edit)]
	public Task<AccountHeadResponse> UpdateAsync(Guid id, [FromBody] AccountHeadUpsertRequest input) => service.UpdateAsync(id, input);

	[HttpPatch("{id:guid}/status")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_AccountHead.ChangeStatus)]
	public Task<AccountHeadResponse> UpdateStatusAsync(Guid id, [FromBody] AccountSettingStatusUpdateRequest input) => service.UpdateStatusAsync(id, input);

	[HttpDelete("{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_AccountHead.Delete)]
	public Task DeleteAsync(Guid id) => service.DeleteAsync(id);
}
