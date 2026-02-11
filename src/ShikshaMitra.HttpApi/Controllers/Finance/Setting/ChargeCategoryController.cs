using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Application.Contracts.Finance.Setting.ChargeCategory;
using ShikshaMitra.Application.Contracts.Finance.Setting.ChargeCategory.Dtos;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Setting;

[Area("Finance")]
[Route("api/finance/charge-categories")]
public class ChargeCategoryController : AbpController
{
    private readonly IChargeCategoryAppService _chargeCategoryAppService;

    public ChargeCategoryController(IChargeCategoryAppService chargeCategoryAppService)
    {
        _chargeCategoryAppService = chargeCategoryAppService;
    }

    [HttpPost]
    public Task<ChargeCategoryResponse> CreateAsync([FromQuery] Guid academicYearId, [FromBody] CreateChargeCategoryRequest input)
    {
        return _chargeCategoryAppService.CreateAsync(academicYearId, input);
    }

    [HttpGet]
    public Task<PaginatedChargeCategoryResponse> GetListAsync(
        [FromQuery] Guid academicYearId, 
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 10, 
        [FromQuery] string? search = null)
    {
        return _chargeCategoryAppService.GetListAsync(academicYearId, page, pageSize, search);
    }

    [HttpGet("{chargeCategoryId}")]
    public Task<ChargeCategoryResponse> GetAsync(Guid chargeCategoryId)
    {
        return _chargeCategoryAppService.GetAsync(chargeCategoryId);
    }

    [HttpPut("{chargeCategoryId}")]
    public Task<ChargeCategoryResponse> UpdateAsync(Guid chargeCategoryId, [FromBody] UpdateChargeCategoryRequest input)
    {
        return _chargeCategoryAppService.UpdateAsync(chargeCategoryId, input);
    }

    [HttpDelete("{chargeCategoryId}")]
    public Task DeleteAsync(Guid chargeCategoryId)
    {
        return _chargeCategoryAppService.DeleteAsync(chargeCategoryId);
    }
}
