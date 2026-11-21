using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Library;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/library/books")]
public class LibraryCopyHistoryController(ILibraryCopyHistoryAppService history) : AbpController
{
	[HttpGet("{bookId:guid}/copies/{copyId:guid}/history")]
	public Task<PaginatedResponse<IssueReturnItemDto>> GetAsync(Guid bookId, Guid copyId,
		[FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] Guid? academicYearId = null,
		[FromQuery] DateOnly? asOfDate = null) => history.GetAsync(bookId, copyId, page, pageSize, academicYearId, asOfDate);
}
