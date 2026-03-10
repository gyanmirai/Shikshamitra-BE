using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;
using ShikshaMitra.Security.Permissions;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.ConcessionRequests.Dtos;

namespace ShikshaMitra.Controllers.Finance;

[Authorize(ShikshaMitraPermissions.Finance.ConcessionRequest.Default)]
[Route("api/app/finance/concessionrequest")]
public class ConcessionRequestController : AbpController, IConcessionRequestAppService
{
    private readonly IConcessionRequestAppService _appService;

    public ConcessionRequestController(IConcessionRequestAppService appService)
    {
        _appService = appService;
    }

    [HttpGet("{id}")]
    public virtual Task<ConcessionRequestDto> GetAsync(Guid id)
    {
        return _appService.GetAsync(id);
    }

    [HttpGet]
    public virtual Task<PagedResultDto<ConcessionRequestDto>> GetListAsync(GetConcessionRequestListRequestDto input)
    {
        return _appService.GetListAsync(input);
    }

    [HttpPost]
    public virtual Task<ConcessionRequestDto> CreateAsync(CreateUpdateConcessionRequestDto input)
    {
        return _appService.CreateAsync(input);
    }

    [HttpPut("{id}")]
    public virtual Task<ConcessionRequestDto> UpdateAsync(Guid id, CreateUpdateConcessionRequestDto input)
    {
        return _appService.UpdateAsync(id, input);
    }

    [HttpDelete("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _appService.DeleteAsync(id);
    }
}
