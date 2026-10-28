using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.AttendanceAndLeaves;

[Area("app")]
[RemoteService(Name = "Default")]
[Authorize]
[Route("api/leave-policies/impact")]
public class LeavePolicyImpactController(ILeavePolicyImpactAppService service) : AbpControllerBase
{
	[HttpPost("preview")]
	public Task<LeavePolicyImpactReviewDto> PreviewAsync([FromBody] LeavePolicyImpactRequest input) => service.PreviewAsync(input);

	[HttpPost("details")]
	public Task<LeavePolicyImpactPageDto> DetailsAsync([FromBody] LeavePolicyImpactDetailsRequest input) => service.GetDetailsAsync(input);
}
