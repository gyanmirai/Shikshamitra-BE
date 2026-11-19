using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.TenantSettings.InitialSetup;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Auditing;

namespace ShikshaMitra.Controllers.TenantSettings;

[Authorize, Route("api/institute-setup/initial")]
public class InitialSetupController(IInitialSetupAppService service) : AbpController
{
	[HttpGet] public Task<InitialSetupDto> GetAsync() => service.GetAsync();
	[HttpPut("draft")] public Task<InitialSetupDto> SaveDraftAsync([FromBody] SaveInitialSetupDraftInput input) => service.SaveDraftAsync(input);
	[HttpPut("profile")] public Task<InitialSetupDto> SaveProfileAsync([FromBody] SaveSetupProfileInput input) => service.SaveProfileAsync(input);
	[HttpPut("academic-year")] public Task<InitialSetupDto> SaveYearAsync([FromBody] SaveSetupYearInput input) => service.SaveYearAsync(input);
	[HttpPut("structure")] public Task<InitialSetupDto> SaveStructureAsync([FromBody] SaveSetupStructureInput input) => service.SaveStructureAsync(input);
	[HttpPut("advance")] public Task<InitialSetupDto> AdvanceAsync([FromBody] AdvanceSetupInput input) => service.AdvanceAsync(input);
	[HttpPut("complete")] public Task<InitialSetupDto> CompleteAsync([FromBody] SetupConcurrencyInput input) => service.CompleteAsync(input);
	[HttpPost("users"), DisableAuditing] public Task<InitialSetupDto> CreateUserAsync([FromBody] CreateSetupUserInput input) => service.CreateUserAsync(input);
}
