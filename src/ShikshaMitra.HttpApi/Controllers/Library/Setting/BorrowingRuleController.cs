using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Library.Setting.BorrowingRules;
using ShikshaMitra.Library.Setting.BorrowingRules.Dtos;
using ShikshaMitra.Library.Shared;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Library.Setting;

[Area("Library")]
[Route("api/borrowing-rules")]
public class BorrowingRuleController : AbpController
{
	private readonly IBorrowingRuleAppService _service;

	public BorrowingRuleController(IBorrowingRuleAppService service)
	{
		_service = service;
	}

	[HttpGet]
	public Task<PaginatedResponse<BorrowingRuleDto>> GetListAsync(
		[FromQuery] Guid academicYearId,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 10)
	{
		return _service.GetListAsync(academicYearId, page, pageSize);
	}

	[HttpPost]
	public Task<GenericUpdatedResponse> UpdateRulesAsync(
		[FromQuery] Guid academicYearid,
		[FromBody] BorrowingRulesUpdateRequest input)
	{
		return _service.UpdateRulesAsync(academicYearid, input);
	}
}
