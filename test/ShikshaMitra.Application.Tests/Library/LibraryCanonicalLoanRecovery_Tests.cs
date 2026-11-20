using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Shared;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryCanonicalLoanRecovery_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task Reviewing_Legacy_Dates_Should_Preserve_Raw_Values_And_An_Unknown_Original_Due_Date()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var before = await Loans.GetAsync(context.LoanId!.Value);
		before.DueDateProvenance.ShouldBe((int)LibraryCivilDateProvenance.UnconfirmedStoredComponent);
		var result = await Loans.ReviewDatesAsync(before.Id, new()
		{
			Revision = before.Revision, IdempotencyKey = Guid.NewGuid().ToString("N"),
			SchoolIssueDate = new(2026, 11, 1), SchoolDueDate = new(2026, 11, 9),
			Reason = "Verified the issue register; earlier renewal history is unavailable."
		});
		result.Loan.RawIssueDate.ShouldBe(before.RawIssueDate);
		result.Loan.RawDueDate.ShouldBe(before.RawDueDate);
		result.Loan.SchoolDueDate.ShouldBe(new DateOnly(2026, 11, 9));
		result.Loan.OriginalSchoolDueDate.ShouldBeNull();
		result.Loan.DueDateProvenance.ShouldBe((int)LibraryCivilDateProvenance.ReviewedLegacy);
	}

	[Fact]
	public async Task A_Return_For_An_Old_Loan_Should_Not_Close_A_Newer_Loan_Of_The_Same_Copy_To_The_Same_Person()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var newerId = await WithUnitOfWorkAsync(async () =>
		{
			var loans = GetRequiredService<IRepository<BookIssue, Guid>>();
			var original = await loans.GetAsync(context.LoanId!.Value);
			original.Status = BookIssueStatus.Returned;
			original.ReturnDate = new(2026, 11, 10);
			await loans.UpdateAsync(original, true);
			var newer = new BookIssue(Guid.NewGuid(), original.BookCopyId, original.AccessionNumber, original.MemberId,
				original.MemberRole, new(2026, 11, 11), new(2026, 11, 25), original.TenantId);
			await loans.InsertAsync(newer, true);
			return newer.Id;
		});
		var error = await Should.ThrowAsync<BusinessException>(() => Loans.ReturnAsync(context.LoanId!.Value, new()
		{
			CopyId = context.Catalog.CopyId, Revision = "previous-loan-form", IdempotencyKey = Guid.NewGuid().ToString("N"),
			SchoolReturnDate = new(2026, 11, 17), UnknownOriginalDatesReason = "Original issue register is unavailable."
		}));
		error.Code.ShouldBe(LibraryErrorCodes.ActiveIssueNotFound);
		await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<BookIssue, Guid>>().GetAsync(newerId)).Status.ShouldBe(BookIssueStatus.Issued));
	}

	[Fact]
	public async Task A_Legacy_Loan_Without_Saved_Terms_Should_Not_Silently_Acquire_Todays_Renewal_Rules()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var before = await Loans.GetAsync(context.LoanId!.Value);
		(await Should.ThrowAsync<BusinessException>(() => Loans.RenewAsync(before.Id, new()
		{
			Revision = before.Revision, IdempotencyKey = Guid.NewGuid().ToString("N"), SchoolRenewalDate = new(2026, 11, 17)
		}))).Code.ShouldBe(LibraryCirculationErrorCodes.RenewalNotAllowed);
	}

	[Fact]
	public async Task A_Physical_Return_With_Reviewed_Unknown_History_Should_Work_Without_Finance_Setup()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var before = await Loans.GetAsync(context.LoanId!.Value);
		var result = await Loans.ReturnAsync(before.Id, new()
		{
			Revision = before.Revision, IdempotencyKey = Guid.NewGuid().ToString("N"), CopyId = context.Catalog.CopyId,
			SchoolReturnDate = new(2026, 11, 17), CopyStatus = (int)BookCopyStatus.Available,
			UnknownOriginalDatesReason = "Physical copy received; the original dated register is unavailable."
		});
		result.Loan.Status.ShouldBe((int)BookIssueStatus.Returned);
		result.Loan.SchoolReturnDate.ShouldBe(new DateOnly(2026, 11, 17));
		result.Loan.RawReturnDate.ShouldBeNull();
		result.Loan.OriginalSchoolDueDate.ShouldBeNull();
		result.AssessmentIds.ShouldBeEmpty();
		await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(context.Catalog.CopyId)).Status.ShouldBe(BookCopyStatus.Available));
	}

	[Fact]
	public async Task Carry_Forward_Return_Should_Close_The_Original_Loan_After_The_Academic_Year_Changes()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider));
		var originalDueDate = new DateOnly(2026, 4, 3);
		var loanId = await WithUnitOfWorkAsync(async () =>
		{
			var copies = GetRequiredService<IRepository<BookCopy, Guid>>();
			var copy = await copies.GetAsync(context.Catalog.CopyId);
			var tenant = GetRequiredService<ICurrentTenant>().Id;
			var actor = GetRequiredService<ICurrentUser>().Id!.Value;
			var loan = BookIssue.CreateCivil(Guid.NewGuid(), tenant, copy.Id, copy.AccessionNumber, context.StudentId, 0,
				new DateOnly(2026, 3, 27), originalDueDate,
				new(context.Catalog.OriginYearId, null, null, null, null, null, "{}", "{}", "{}", actor, DateTimeOffset.UtcNow));
			await GetRequiredService<IRepository<BookIssue, Guid>>().InsertAsync(loan, true);
			copy.Status = BookCopyStatus.Issued;
			await copies.UpdateAsync(copy, true);
			return loan.Id;
		});
		var saved = await Loans.GetAsync(loanId);
		var returned = await Loans.ReturnAsync(loanId, new()
		{
			CopyId = context.Catalog.CopyId, Revision = saved.Revision, IdempotencyKey = "rollover-return",
			SchoolReturnDate = new(2026, 4, 2), CopyStatus = (int)BookCopyStatus.Available
		});
		returned.Loan.Id.ShouldBe(loanId);
		returned.Loan.AcademicYearId.ShouldBe(context.Catalog.OriginYearId);
		returned.Loan.OriginalSchoolDueDate.ShouldBe(originalDueDate);
		returned.Loan.Status.ShouldBe((int)BookIssueStatus.Returned);
		returned.AssessmentIds.ShouldBeEmpty();
	}

	private ILibraryLoanAppService Loans => GetRequiredService<ILibraryLoanAppService>();
	private Task<LibraryCopyLifecycleTestContext> SeedAsync() =>
		WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider, onLoan: true));
}
