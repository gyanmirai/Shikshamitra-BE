using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Application.Contracts.Finance.Setting.IncomeCategory;
using ShikshaMitra.Application.Contracts.Finance.Setting.IncomeCategory.Dtos;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Setting;

[RemoteService]
[Area("Finance")]
[Route("finance/income-categories")]
public class IncomeCategoryController : AbpController
{
    private readonly IIncomeCategoryAppService _incomeCategoryAppService;

    public IncomeCategoryController(IIncomeCategoryAppService incomeCategoryAppService)
    {
        _incomeCategoryAppService = incomeCategoryAppService;
    }

    [HttpPost]
    public Task<IncomeCategoryResponse> CreateAsync([FromQuery] Guid academicYearId, [FromBody] CreateIncomeCategoryRequest input)
    {
        return _incomeCategoryAppService.CreateAsync(academicYearId, input);
    }

    [HttpGet]
    public Task<PaginatedIncomeCategoryResponse> GetListAsync(
        [FromQuery] Guid academicYearId, 
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 10, 
        [FromQuery] string? search = null)
    {
        return _incomeCategoryAppService.GetListAsync(academicYearId, page, pageSize, search);
    }

    [HttpGet("{incomeCategoryId}")]
    public Task<IncomeCategoryResponse> GetAsync(Guid incomeCategoryId)
    {
        return _incomeCategoryAppService.GetAsync(incomeCategoryId);
    }

    [HttpPut("{incomeCategoryId}")]
    public Task<IncomeCategoryResponse> UpdateAsync(Guid incomeCategoryId, [FromBody] UpdateIncomeCategoryRequest input)
    {
        return _incomeCategoryAppService.UpdateAsync(incomeCategoryId, input);
    }

    [HttpDelete("{incomeCategoryId}")]
    public Task DeleteAsync(Guid incomeCategoryId)
    {
        return _incomeCategoryAppService.DeleteAsync(incomeCategoryId);
    }
}
