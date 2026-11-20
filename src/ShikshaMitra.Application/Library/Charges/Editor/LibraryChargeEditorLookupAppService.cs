using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Library.Setting.ChargeCategory;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library.Charges.Editor;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.Library.Charges.View)]
public partial class LibraryChargeEditorLookupAppService(IPermissionChecker permissions,
	ILibraryAccessService access, IRepository<LibraryChargeCategory, Guid> categories,
	LibraryChargeEditorMemberReader members, LibraryStoredLoanScope storedLoans,
	IRepository<LibraryMember, Guid> memberships, IRepository<BookCopy, Guid> copies,
	IRepository<Book, Guid> books, ILibraryChargeAppService charges)
	: ApplicationService, ILibraryChargeEditorLookupAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Library.Charges.Add)]
	public async Task<PaginatedResponse<LibraryChargeEditorCategoryDto>> GetCategoriesAsync(Guid academicYearId,
		int page = 1, int pageSize = 20, string? search = null)
	{
		await RequireAsync(ShikshaMitraSubmodulePermissions.Library.Charges.Add, academicYearId);
		var term = SearchTerm(search);
		var query = (await categories.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id && row.AcademicYearId == academicYearId);
		if (term.Length > 0) query = query.Where(row => row.ChargeType.ToLower().Contains(term));
		return await PageAsync(query.OrderBy(row => row.ChargeType).ThenBy(row => row.Id).Select(row => new LibraryChargeEditorCategoryDto
		{
			Id = row.Id, AcademicYearId = row.AcademicYearId, Name = row.ChargeType, Amount = row.Amount, IsFixed = row.IsFixed
		}), page, pageSize);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.Charges.Add)]
	public async Task<PaginatedResponse<LibraryChargeEditorMemberDto>> SearchMembersAsync(Guid academicYearId,
		string? search = null, int page = 1, int pageSize = 20) =>
		await members.SearchAsync(await RequireAsync(ShikshaMitraSubmodulePermissions.Library.Charges.Add, academicYearId), search, page, pageSize);

	[Authorize(ShikshaMitraSubmodulePermissions.Library.Charges.Add)]
	public async Task<LibraryChargeEditorMemberDto> ResolveMemberAsync(Guid academicYearId, string identifier) =>
		await members.ResolveAsync(await RequireAsync(ShikshaMitraSubmodulePermissions.Library.Charges.Add, academicYearId), identifier);

	[Authorize(ShikshaMitraSubmodulePermissions.Library.Charges.Update)]
	public async Task<LibraryChargeEditorContextDto> GetUpdateContextAsync(Guid chargeId)
	{
		await RequireAsync(ShikshaMitraSubmodulePermissions.Library.Charges.Update);
		var assessment = await charges.GetAsync(chargeId);
		// This is an assessment editor, so existing charge-detail contact/profile facts
		// are not copied into the editor's read model.
		assessment.Avatar = assessment.ClassDivision = assessment.RollNo = assessment.GrNo = assessment.ContactNo = null;
		var needsReview = !assessment.AcademicYearId.HasValue || assessment.AcademicYearProvenance == LibraryAcademicYearProvenance.Unconfirmed ||
			!assessment.SchoolChargeDate.HasValue || !assessment.SchoolDueDate.HasValue ||
			assessment.ChargeDateProvenance == LibraryCivilDateProvenance.UnconfirmedStoredComponent ||
			assessment.DueDateProvenance == LibraryCivilDateProvenance.UnconfirmedStoredComponent || assessment.Currency != "INR";
		var blocked = assessment.PaymentStatus != (int)ChargePaymentStatus.Pending
			? "This historical paid or waived assessment is read-only. Its local status does not establish a Finance receipt."
			: assessment.FinanceSourceLinkId.HasValue ? "This assessment is bound to Finance. Use its financial correction workflow."
			: needsReview ? "Review the assessment's school dates, currency and academic year before amending it." : null;
		return new() { Assessment = assessment, CanAmend = blocked == null, RequiresContextReview = needsReview, AmendmentBlockedReason = blocked };
	}

	private async Task<LibraryAcademicContext> RequireAsync(string action, Guid? academicYearId = null)
	{
		if (!CurrentTenant.Id.HasValue || !CurrentUser.Id.HasValue ||
			!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Charges.View) || !await permissions.IsGrantedAsync(action))
			throw new AbpAuthorizationException("Library assessment editor access denied.");
		return await access.GetContextAsync(academicYearId);
	}

	private async Task<PaginatedResponse<T>> PageAsync<T>(IQueryable<T> query, int page, int pageSize)
	{
		page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
		var count = await AsyncExecuter.CountAsync(query);
		var skip = ((long)page - 1) * pageSize;
		List<T> rows = skip >= count ? [] : await AsyncExecuter.ToListAsync(query.Skip((int)skip).Take(pageSize));
		return new() { Items = rows, TotalCount = count, CurrentPage = page, PageSize = pageSize, TotalPages = (int)Math.Ceiling(count / (double)pageSize) };
	}

	private static string SearchTerm(string? search)
	{
		var term = (search ?? string.Empty).Trim().ToLowerInvariant();
		if (term.Length > 100) throw new BusinessException(LibraryErrorCodes.MemberNotFound);
		return term;
	}
}
