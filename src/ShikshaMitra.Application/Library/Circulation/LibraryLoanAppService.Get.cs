using System;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.IssueReturn;

namespace ShikshaMitra.Library.Circulation;

public partial class LibraryLoanAppService
{
	private async Task<LibraryLoanDto> ReadLoanForGetAsync(Guid id)
	{
		var loan = await RequireReadableLoanAsync(id);
		var result = await ReadLoanAsync(loan);
		result.SchoolContext = await schoolContexts.ReadAsync(await access.GetContextAsync());
		result.CanReviewDates = loan.HasReviewableSchoolDates() &&
			await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.IssueReturn.ReviewDates);
		return result;
	}
}
