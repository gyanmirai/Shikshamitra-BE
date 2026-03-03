using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Application.Contracts.Finance.Setting.ConcessionType;
using ShikshaMitra.Application.Contracts.Finance.Setting.ConcessionType.Dtos;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Setting;

[RemoteService]
[Area("app")]

[Route("api")]
public class ConcessionTypeController : AbpController
{
	private readonly IConcessionTypeAppService _concessionTypeAppService;

	public ConcessionTypeController(IConcessionTypeAppService concessionTypeAppService)
	{
		_concessionTypeAppService = concessionTypeAppService;
	}

	[HttpPost("concession-types")]
	public Task<MutationResponse> CreateAsync(Guid academicYearId, ConcessionTypeCreateRequest input)
	{
		return _concessionTypeAppService.CreateAsync(academicYearId, input);
	}

	[HttpGet("all-concession-types")]
	public Task<ConcessionTypePaginatedResponse> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10, string? search = null)
	{
		return _concessionTypeAppService.GetListAsync(academicYearId, page, pageSize, search);
	}

	[HttpGet("concession-types/{concessionTypeId}")]
	public Task<ConcessionTypeResponse> GetAsync(Guid concessionTypeId)
	{
		return _concessionTypeAppService.GetAsync(concessionTypeId);
	}

	[HttpPut("concession-types/{concessionTypeId}")]
	public Task<MutationResponse> UpdateAsync(Guid concessionTypeId, ConcessionTypeUpdateRequest input)
	{
		return _concessionTypeAppService.UpdateAsync(concessionTypeId, input);
	}

	[HttpDelete("concession-types/{concessionTypeId}")]
	public Task<MutationResponse> DeleteAsync(Guid concessionTypeId)
	{
		return _concessionTypeAppService.DeleteAsync(concessionTypeId);
	}
}
