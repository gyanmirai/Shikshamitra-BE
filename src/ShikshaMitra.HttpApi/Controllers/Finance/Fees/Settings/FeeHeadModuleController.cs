using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.Dtos;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Fees.Settings;

[RemoteService]
[Area("finance")]
[Route("api/fee-head-modules")]
[Authorize]
public class FeeHeadModuleController(
	IFeeHeadModuleAppService feeHeadModuleAppService
) : AbpController
{
    [HttpGet]
    public Task<List<FeeHeadModuleDto>> GetListAsync()
    {
        return feeHeadModuleAppService.GetListAsync();
    }
}
