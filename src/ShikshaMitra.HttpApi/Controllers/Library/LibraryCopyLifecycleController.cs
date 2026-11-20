using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Shared;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Library;

[Area("Library")]
[Route("api/library/books/{bookId:guid}/copies/{copyId:guid}")]
public class LibraryCopyLifecycleController(ILibraryCopyLifecycleAppService lifecycle) : AbpController
{
	[HttpGet("state")]
	public Task<LibraryCopyStateDto> GetStateAsync(Guid bookId, Guid copyId) => lifecycle.GetStateAsync(bookId, copyId);

	[HttpGet("move-options")]
	public Task<LibraryCopyMoveOptionsDto> GetMoveOptionsAsync(Guid bookId, Guid copyId, [FromQuery] Guid? academicYearId = null) =>
		lifecycle.GetMoveOptionsAsync(bookId, copyId, academicYearId);

	[HttpGet("events")]
	public Task<PaginatedResponse<LibraryCopyEventDto>> GetEventsAsync(Guid bookId, Guid copyId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
		lifecycle.GetEventsAsync(bookId, copyId, page, pageSize);

	[HttpPost("condition")]
	public Task<LibraryCopyStateDto> ChangeConditionAsync(Guid bookId, Guid copyId, [FromBody] LibraryCopyConditionRequest input) =>
		lifecycle.ChangeConditionAsync(bookId, copyId, input);

	[HttpPost("move")]
	public Task<LibraryCopyStateDto> MoveAsync(Guid bookId, Guid copyId, [FromBody] LibraryCopyMoveRequest input) =>
		lifecycle.MoveAsync(bookId, copyId, input);

	[HttpPost("write-off")]
	public Task<LibraryCopyStateDto> WriteOffAsync(Guid bookId, Guid copyId, [FromBody] LibraryCopyWriteOffRequest input) =>
		lifecycle.WriteOffAsync(bookId, copyId, input);
}
