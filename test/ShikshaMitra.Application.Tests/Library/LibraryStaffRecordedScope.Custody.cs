using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Custody;
using ShikshaMitra.Library.InventoryVerification;
using ShikshaMitra.Library.IssueReturn;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryStaffRecordedScope_Tests
{
	[Fact]
	public async Task Classroom_Custody_Should_Keep_Personal_Loans_Separate_And_Allow_A_Reviewed_Partial_Return()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		var holding = fixture.Context.Catalog;
		var spare = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<BookCopy, Guid>>().InsertAsync(new(
			Guid.NewGuid(), holding.BookId, 2, "CLASSROOM-0002", holding.SectionId, holding.ShelfId,
			GetRequiredService<ICurrentTenant>().Id), true));
		var day = new DateOnly(2026, 11, 17);
		var app = GetRequiredService<ILibraryCustodyAppService>();
		var input = new LibraryCustodyHandoffInput(holding.YearId, fixture.EmployeeId, fixture.Context.BorrowerClassId,
			null, "Two classroom reading copies", day, day.AddDays(5), [holding.CopyId, spare.Id]);
		var plan = await app.PreviewHandoffAsync(input);
		plan.Errors.ShouldBeEmpty();
		plan.Copies.Count.ShouldBe(2);
		var handoff = new LibraryCustodyHandoffCommand(input, plan.Revision!, "classroom-handoff");
		var session = await app.HandoffAsync(handoff);
		(await app.GetAsync(session.Id)).PendingCount.ShouldBe(2);
		var stock = GetRequiredService<ILibraryStockVerificationAppService>();
		var countInput = new LibraryStockCountInput("Classroom custody count", holding.SectionId, holding.ShelfId, day);
		var countPreview = await stock.PreviewAsync(countInput);
		var count = await stock.OpenAsync(new(countInput, countPreview.Revision, "custody-stock-count"));
		(await stock.GetReconciliationAsync(count.Id)).Items.Select(row => row.Finding).Distinct()
			.ShouldHaveSingleItem().ShouldBe(LibraryStockFinding.InCustody);
		var checkout = await GetRequiredService<ILibraryIssuePlanAppService>().PreviewAsync(new()
		{
			CopyId = holding.CopyId, MemberId = fixture.Context.StudentId, IssueDate = day,
			RequestedDueDate = day.AddDays(7), AcademicYearId = holding.YearId
		});
		checkout.CanIssue.ShouldBeFalse();
		var copyActions = GetRequiredService<ILibraryCopyLifecycleAppService>();
		var physical = await copyActions.GetStateAsync(holding.BookId, holding.CopyId);
		await Should.ThrowAsync<BusinessException>(() => copyActions.ChangeConditionAsync(holding.BookId, holding.CopyId,
			new() { Status = BookCopyStatus.Available, Reason = "Generic catalogue edit cannot receive custody", Revision = physical.Revision }));

		var returning = new LibraryCustodyReturnInput(day.AddDays(1), [new(holding.CopyId, BookCopyStatus.Available)], "One copy brought back");
		var returnPlan = await app.PreviewReturnAsync(session.Id, returning);
		returnPlan.Errors.ShouldBeEmpty();
		var receiptCommand = new LibraryCustodyReturnCommand(returning, returnPlan.Revision!, "classroom-receipt");
		var receipt = await app.ReturnAsync(session.Id, receiptCommand);
		(await app.ReturnAsync(session.Id, receiptCommand)).Id.ShouldBe(receipt.Id);
		(await app.HandoffAsync(handoff)).Id.ShouldBe(session.Id);
		(await app.GetAsync(session.Id)).PendingCount.ShouldBe(1);
		await WithUnitOfWorkAsync(async () =>
		{
			var copies = GetRequiredService<IRepository<BookCopy, Guid>>();
			(await copies.GetAsync(holding.CopyId)).Status.ShouldBe(BookCopyStatus.Available);
			(await copies.GetAsync(spare.Id)).Status.ShouldBe(BookCopyStatus.InCustody);
			(await GetRequiredService<IRepository<BookIssue, Guid>>().CountAsync(row => row.BookCopyId == holding.CopyId ||
				row.BookCopyId == spare.Id)).ShouldBe(0);
		});
	}
}
