using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.IssueReturn;

public interface ILibraryLoanAppService : IApplicationService
{
	Task<LibraryLoanDto> GetAsync(Guid loanId);
	Task<LibraryLoanCommandResultDto> CheckoutAsync(LibraryCheckoutRequest input);
	Task<LibraryLoanCommandResultDto> ReturnAsync(Guid loanId, LibraryLoanReturnRequest input);
	Task<LibraryLoanRenewalPlanDto> PreviewRenewalAsync(Guid loanId, LibraryLoanRenewalPlanRequest input);
	Task<LibraryLoanCommandResultDto> RenewAsync(Guid loanId, LibraryLoanRenewRequest input);
	Task<LibraryLoanCommandResultDto> ReviewDatesAsync(Guid loanId, LibraryLoanDateReviewRequest input);
}
