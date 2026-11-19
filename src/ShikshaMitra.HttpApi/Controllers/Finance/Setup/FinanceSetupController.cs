using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Finance.Setup;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Finance.Setup;

[Authorize, ApiController, Route("api/finance/setup")]
public class FinanceSetupController(IFinanceSetupAppService service) : AbpController
{
	[HttpGet("{academicYearId:guid}")]
	public Task<FinanceSetupStateDto> GetAsync(Guid academicYearId) => service.GetAsync(academicYearId);
	[HttpPut("{academicYearId:guid}/progress")]
	public Task<FinanceSetupStateDto> SaveProgressAsync(Guid academicYearId, [FromBody] FinanceSetupProgressInput input) => service.SaveProgressAsync(academicYearId, input);
}
