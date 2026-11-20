using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Library;

[Authorize]
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/library/circulation/loans")]
public class LibraryLoanController(ILibraryLoanAppService loans, ILibraryCirculationReadAppService reads) : AbpController
{
	[HttpGet]
	public Task<PaginatedResponse<IssueReturnItemDto>> GetListAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
		[FromQuery] Guid? academicYearId = null, [FromQuery] DateOnly? asOfDate = null) => reads.GetListAsync(page, pageSize, academicYearId, asOfDate);

	[HttpGet("{loanId:guid}")]
	public Task<LibraryLoanDto> GetAsync(Guid loanId) => loans.GetAsync(loanId);

	[HttpPost]
	public Task<LibraryLoanCommandResultDto> CheckoutAsync([FromBody] LibraryCheckoutRequest input) => loans.CheckoutAsync(input);

	[HttpPost("{loanId:guid}/return")]
	public Task<LibraryLoanCommandResultDto> ReturnAsync(Guid loanId, [FromBody] LibraryLoanReturnRequest input) => loans.ReturnAsync(loanId, input);

	[HttpPost("{loanId:guid}/renewal-preview")]
	public Task<LibraryLoanRenewalPlanDto> PreviewRenewalAsync(Guid loanId, [FromBody] LibraryLoanRenewalPlanRequest input) => loans.PreviewRenewalAsync(loanId, input);

	[HttpPost("{loanId:guid}/renew")]
	public Task<LibraryLoanCommandResultDto> RenewAsync(Guid loanId, [FromBody] LibraryLoanRenewRequest input) => loans.RenewAsync(loanId, input);

	[HttpPost("{loanId:guid}/dates/review")]
	public Task<LibraryLoanCommandResultDto> ReviewDatesAsync(Guid loanId, [FromBody] LibraryLoanDateReviewRequest input) => loans.ReviewDatesAsync(loanId, input);
}
