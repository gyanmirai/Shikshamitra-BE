using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Library.Charges;
using ShikshaMitra.Library.Shared;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Library;

[Area("Library")]
[Route("api/library/charges")]
public class LibraryChargeController : AbpController
{
    private readonly ILibraryChargeAppService _chargeAppService;

    public LibraryChargeController(ILibraryChargeAppService chargeAppService)
    {
        _chargeAppService = chargeAppService;
    }

    [HttpPost]
    [HttpPost("create")]
    public async Task<IActionResult> CreateAsync([FromBody] CreateChargeRequest input)
    {
        var result = await _chargeAppService.CreateAsync(input);
        return StatusCode(201, result);
    }

    [HttpGet]
    public Task<PaginatedResponse<ChargeListItemDto>> GetListAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        return _chargeAppService.GetListAsync(page, pageSize);
    }

    [HttpGet("{id:guid}")]
    [HttpGet("details/{id:guid}")]
    public Task<ChargeDetailDto> GetAsync(Guid id)
    {
        return _chargeAppService.GetAsync(id);
    }

    [HttpPut("{id:guid}")]
    public Task<GenericUpdatedResponse> UpdateAsync(Guid id, [FromBody] UpdateChargeRequest input)
    {
        return _chargeAppService.UpdateAsync(id, input);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id)
    {
        await _chargeAppService.DeleteAsync(id);
        return NoContent();
    }
}
