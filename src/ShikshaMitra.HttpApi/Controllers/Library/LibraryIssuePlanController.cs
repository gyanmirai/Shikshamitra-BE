using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Library.IssueReturn;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Library;

[Area("Library")]
[Route("api/library/circulation/issues")]
public class LibraryIssuePlanController(ILibraryIssuePlanAppService plans) : AbpController
{
	[HttpPost("preview")]
	public Task<LibraryIssuePlanDto> PreviewAsync([FromBody] LibraryIssuePlanRequest input) => plans.PreviewAsync(input);
}
