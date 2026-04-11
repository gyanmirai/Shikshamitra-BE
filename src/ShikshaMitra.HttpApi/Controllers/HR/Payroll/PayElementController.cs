using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Payroll.Settings;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Payroll;

/// <summary>
/// API Controller for managing payroll pay elements.
/// </summary>
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/payroll/pay-elements")]
[Authorize]
public class PayElementController : AbpControllerBase
{
    private readonly IPayElementAppService _payElementAppService;

    public PayElementController(IPayElementAppService payElementAppService)
    {
        _payElementAppService = payElementAppService;
    }

    /// <summary>
    /// Get paginated list of pay elements.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<PayElementDto>), StatusCodes.Status200OK)]
    public virtual async Task<PagedResultDto<PayElementDto>> GetListAsync([FromQuery] PagedAndSortedResultRequestDto input)
    {
        return await _payElementAppService.GetListAsync(input);
    }

    /// <summary>
    /// Get a pay element by ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(PayElementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<PayElementDto> GetAsync(Guid id)
    {
        return await _payElementAppService.GetAsync(id);
    }

    /// <summary>
    /// Create a new pay element.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PayElementDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public virtual async Task<ActionResult<PayElementDto>> CreateAsync([FromBody] CreateUpdatePayElementDto input)
    {
        var result = await _payElementAppService.CreateAsync(input);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Update an existing pay element.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(PayElementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<PayElementDto> UpdateAsync(Guid id, [FromBody] CreateUpdatePayElementDto input)
    {
        return await _payElementAppService.UpdateAsync(id, input);
    }

    /// <summary>
    /// Delete a pay element.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<IActionResult> DeleteAsync(Guid id)
    {
        await _payElementAppService.DeleteAsync(id);
        return NoContent();
    }
}
