using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentRecords;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryCanonicalCheckout_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task Checkout_Should_Save_Exact_Civil_Dates_And_Actor_Without_Inventing_Raw_Instants()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var input = await RequestAsync(context);
		var result = await Loans.CheckoutAsync(input);
		result.Loan.SchoolIssueDate.ShouldBe(input.IssueDate);
		result.Loan.SchoolDueDate.ShouldBe(input.RequestedDueDate);
		result.Loan.OriginalSchoolDueDate.ShouldBe(input.RequestedDueDate);
		result.Loan.AcademicYearId.ShouldBe(context.Catalog.YearId);
		result.Loan.RawIssueDate.ShouldBeNull();
		result.Loan.RawDueDate.ShouldBeNull();
		result.Loan.IssuedByUserId.ShouldBe(GetRequiredService<ICurrentUser>().Id);
		result.Loan.Policy!.IsLegacyManualDate.ShouldBeTrue();
	}

	[Fact]
	public async Task Retrying_The_Same_Checkout_Should_Return_The_Original_Loan_And_One_Copy_Event()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var input = await RequestAsync(context);
		var first = await Loans.CheckoutAsync(input);
		var retry = await Loans.CheckoutAsync(input);
		retry.Loan.Id.ShouldBe(first.Loan.Id);
		retry.WasReplay.ShouldBeTrue();
		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<IRepository<BookIssue, Guid>>().CountAsync(row => row.BookCopyId == input.CopyId)).ShouldBe(1);
			(await GetRequiredService<IRepository<LibraryCopyEvent, Guid>>().CountAsync(row => row.CopyId == input.CopyId &&
				row.EventType == LibraryCopyEventType.Issued)).ShouldBe(1);
		});
	}

	[Fact]
	public async Task A_Checkout_Key_Should_Not_Be_Reused_With_A_Different_Due_Date()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var input = await RequestAsync(context);
		await Loans.CheckoutAsync(input);
		input.RequestedDueDate = input.RequestedDueDate!.Value.AddDays(1);
		(await Should.ThrowAsync<BusinessException>(() => Loans.CheckoutAsync(input))).Code
			.ShouldBe(LibraryCirculationErrorCodes.CommandKeyReused);
	}

	[Fact]
	public async Task A_Changed_Copy_Should_Require_A_Fresh_Plan_Before_Checkout()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var input = await RequestAsync(context);
		await WithUnitOfWorkAsync(async () =>
		{
			var copies = GetRequiredService<IRepository<BookCopy, Guid>>();
			var copy = await copies.GetAsync(input.CopyId);
			copy.Status = BookCopyStatus.UnderRepair;
			await copies.UpdateAsync(copy, true);
		});
		(await Should.ThrowAsync<BusinessException>(() => Loans.CheckoutAsync(input))).Code
			.ShouldBe(LibraryCirculationErrorCodes.PlanChanged);
		await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<BookIssue, Guid>>().AnyAsync(row => row.BookCopyId == input.CopyId)).ShouldBeFalse());
	}

	[Fact]
	public async Task Former_Borrower_Can_Return_The_Original_Loan_And_Cannot_Borrow_Again()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			var students = GetRequiredService<IRepository<Student, Guid>>();
			var borrower = await students.GetAsync(context.StudentId);
			borrower.ChangeStatus(StudentLifeCycleStatusEnum.Enrolled);
			await students.UpdateAsync(borrower, true);
		});
		var input = await RequestAsync(context);
		var issued = await Loans.CheckoutAsync(input);
		await WithUnitOfWorkAsync(async () =>
		{
			var students = GetRequiredService<IRepository<Student, Guid>>();
			var borrower = await students.GetAsync(context.StudentId);
			borrower.ChangeOperationalStatus(StudentStatus.Left, "Approved departure with a physical obligation.");
			await students.UpdateAsync(borrower, true);
		});
		var existing = await Loans.GetAsync(issued.Loan.Id);
		var returned = await Loans.ReturnAsync(existing.Id, new()
		{
			Revision = existing.Revision, IdempotencyKey = "post-exit-return", CopyId = input.CopyId,
			SchoolReturnDate = new(2026, 11, 18), CopyStatus = (int)BookCopyStatus.Available
		});
		returned.Loan.Id.ShouldBe(issued.Loan.Id);
		returned.Loan.AcademicYearId.ShouldBe(issued.Loan.AcademicYearId);
		returned.Loan.OriginalSchoolDueDate.ShouldBe(issued.Loan.OriginalSchoolDueDate);
		returned.Loan.Status.ShouldBe((int)BookIssueStatus.Returned);
		returned.AssessmentIds.ShouldBeEmpty();
		var another = await GetRequiredService<ILibraryIssuePlanAppService>().PreviewAsync(new()
		{
			CopyId = input.CopyId, MemberId = context.StudentId, IssueDate = new(2026, 11, 19),
			RequestedDueDate = new(2026, 11, 30)
		});
		another.CanIssue.ShouldBeFalse();
	}

	private ILibraryLoanAppService Loans => GetRequiredService<ILibraryLoanAppService>();
	private Task<LibraryCopyLifecycleTestContext> SeedAsync() =>
		WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider));
	private async Task<LibraryCheckoutRequest> RequestAsync(LibraryCopyLifecycleTestContext context)
	{
		var input = new LibraryCheckoutRequest
		{
			CopyId = context.Catalog.CopyId, MemberId = context.StudentId, IssueDate = new(2026, 11, 17),
			RequestedDueDate = new(2026, 11, 30), IdempotencyKey = Guid.NewGuid().ToString("N")
		};
		input.Revision = (await GetRequiredService<ILibraryIssuePlanAppService>().PreviewAsync(new()
		{
			CopyId = input.CopyId, MemberId = input.MemberId, AcademicYearId = input.AcademicYearId,
			IssueDate = input.IssueDate, RequestedDueDate = input.RequestedDueDate
		})).Revision;
		return input;
	}
}
