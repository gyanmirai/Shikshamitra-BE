using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Circulation;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library.Policies;

[RemoteService(false)]
public class LibraryPolicyContextReader(ILibraryAccessService access, IRepository<Book, Guid> books,
	IRepository<BookCopy, Guid> copies, IRepository<Standard, Guid> standards,
	LibraryCirculationMemberReader borrowers) : ApplicationService, ITransientDependency
{
	public Task<LibraryBorrowingPolicyContext> ReadAsync(LibraryPolicyResolveRequest input, DateOnly asOfDate) =>
		ReadAsync(input, asOfDate, null);

	internal async Task<LibraryBorrowingPolicyContext> ReadAsync(LibraryPolicyResolveRequest input, DateOnly asOfDate,
		LibraryCirculationMemberDto? resolvedBorrower)
	{
		await access.GetContextAsync(input.AcademicYearId);
		BookCopy? copy = null;
		if (input.CopyId.HasValue)
		{
			copy = await copies.FindAsync(input.CopyId.Value) ?? throw new BusinessException(LibraryErrorCodes.CopyNotFound);
			if (input.BookId.HasValue && input.BookId != copy.BookId) throw new BusinessException(LibraryErrorCodes.PolicyInvalid);
		}
		var bookId = input.BookId ?? copy?.BookId;
		var categoryId = bookId.HasValue ? await AsyncExecuter.FirstOrDefaultAsync((await books.GetQueryableAsync())
			.Where(x => x.Id == bookId.Value && x.TenantId == CurrentTenant.Id).Select(x => (Guid?)x.CategoryId)) : null;
		if (bookId.HasValue && !categoryId.HasValue) throw new BusinessException(LibraryErrorCodes.BookNotFound);
		var role = input.Role;
		Guid? stageId = null;
		Guid? standardId = null;
		if (input.MemberId.HasValue)
		{
			var borrower = resolvedBorrower ?? await borrowers.ResolveAsync(input.MemberId.Value.ToString());
			var student = await AsyncExecuter.FirstOrDefaultAsync((await access.GetStudentsAsync()).Where(x => x.Id == borrower.Id)
				.Select(x => new { x.AcademicYearId, x.AcademicMediumStageId, x.StandardId }));
			role = borrower.Role;
			if (student != null)
			{
				if (student.AcademicYearId != input.AcademicYearId)
					throw new BusinessException(LibraryErrorCodes.PolicyInvalid, "Use the borrower's current academic context to resolve a new borrowing policy. Historical loans retain their saved policy.");
				stageId = student.AcademicMediumStageId;
				standardId = student.StandardId;
				if (standardId.HasValue)
				{
					var standard = await standards.GetAsync(standardId.Value);
					if (stageId.HasValue && stageId != standard.EducationMediumStageId) throw new BusinessException(LibraryErrorCodes.PolicyInvalid);
					stageId = standard.EducationMediumStageId;
				}
			}
		}
		return new(input.AcademicYearId, role, asOfDate, categoryId, copy?.SectionId, stageId, standardId);
	}
}
