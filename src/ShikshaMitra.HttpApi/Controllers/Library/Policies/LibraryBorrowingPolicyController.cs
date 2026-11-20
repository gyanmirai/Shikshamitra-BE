using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Library.Shared;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Library.Policies;

[Area("Library")]
[Route("api/library/policies")]
public class LibraryBorrowingPolicyController(ILibraryBorrowingPolicyAppService service) : AbpController
{
	[HttpGet]
	public Task<PaginatedResponse<LibraryBorrowingPolicyVersionDto>> GetListAsync([FromQuery] Guid academicYearId,
		[FromQuery] int page = 1, [FromQuery] int pageSize = 20) => service.GetListAsync(academicYearId, page, pageSize);
	[HttpGet("options")]
	public Task<LibraryPolicyOptionsDto> GetOptionsAsync([FromQuery] Guid academicYearId) => service.GetOptionsAsync(academicYearId);
	[HttpGet("{versionId:guid}")]
	public Task<LibraryBorrowingPolicyVersionDto> GetAsync(Guid versionId) => service.GetAsync(versionId);
	[HttpPost]
	public async Task<IActionResult> CreateAsync([FromQuery] Guid academicYearId, [FromBody] LibraryBorrowingPolicyDraftRequest input) =>
		StatusCode(201, await service.CreateAsync(academicYearId, input));
	[HttpPut("{versionId:guid}")]
	public Task<LibraryBorrowingPolicyVersionDto> UpdateAsync(Guid versionId, [FromBody] LibraryBorrowingPolicyDraftRequest input) => service.UpdateAsync(versionId, input);
	[HttpPost("{versionId:guid}/publish")]
	public Task<LibraryBorrowingPolicyVersionDto> PublishAsync(Guid versionId, [FromBody] LibraryPolicyPublishRequest input) => service.PublishAsync(versionId, input);
	[HttpPost("{versionId:guid}/archive")]
	public Task<LibraryBorrowingPolicyVersionDto> ArchiveAsync(Guid versionId, [FromBody] LibraryPolicyArchiveRequest input) => service.ArchiveAsync(versionId, input);
	[HttpPost("resolve")]
	public Task<LibraryBorrowingPolicySnapshotDto> ResolveAsync([FromBody] LibraryPolicyResolveRequest input) => service.ResolveAsync(input);
}
