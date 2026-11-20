using System;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Charges;
using ShikshaMitra.Library.Shared;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	// M09: the initial historical text disagrees with its retained physical ID.
	[Fact]
	public async Task Member_Charge_History_Should_Resolve_A_Linked_Loan_Through_Its_Stable_Copy_Id()
	{
		var fixture = await SeedMemberChargeCopyConflictAsync();
		var owner = fixture.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var denied = await RestrictMemberChargeReadAsync(owner);
		await AssertMemberChargeReadAuthorityAsync(owner, denied);
		await AssertMemberChargeTargetAsync(owner);
		await WithUnitOfWorkAsync(async () =>
		{
			var context = await GetRequiredService<ILibraryAccessService>().GetContextAsync(owner.YearId);
			context.UserScope.HasTenantWideScope().ShouldBeFalse();
			(await GetRequiredService<LibraryStoredLoanScope>().CanReadAsync(fixture.LoanId, context.UserScope)).ShouldBeTrue();
			var loan = await GetRequiredService<IRepository<BookIssue, Guid>>().GetAsync(fixture.LoanId);
			loan.BookCopyId.ShouldBe(fixture.CopyId); loan.AccessionNumber.ShouldBe(fixture.RecordedAccession);
			var copies = GetRequiredService<IRepository<BookCopy, Guid>>();
			(await copies.GetAsync(fixture.CopyId)).AccessionNumber.ShouldNotBe(fixture.RecordedAccession);
			(await copies.GetAsync(fixture.DecoyCopyId)).AccessionNumber.ShouldBe(fixture.RecordedAccession);
			var query = await ConsumerPositionService(owner).GetQueryAsync(new(
				LibraryChargePositionReadPurpose.MemberCharges, owner.YearId, [owner.AssessmentId]));
			(await GetRequiredService<IAsyncQueryableExecuter>().SingleAsync(query)).Evidence.ShouldBe(FinanceExternalPositionEvidence.Verified);
		});
		// Existing member-loan presentation establishes the already-supported
		// stable-copy and reconciliation semantics before the charge-row SUT.
		var borrowed = await WithUnitOfWorkAsync(() => MemberChargeReadService(owner).GetBorrowedBooksAsync(owner.PersonId, 1, 10));
		var retained = borrowed.Items.ShouldHaveSingleItem();
		retained.Id.ShouldBe(fixture.LoanId); retained.CopyId.ShouldBe(fixture.CopyId); retained.BookId.ShouldBe(fixture.BookId);
		retained.BookTitle.ShouldBe(fixture.Title); retained.CopyLinkSource.ShouldBe("CopyId");
		retained.RequiresCopyReconciliation.ShouldBeTrue();
		var before = JsonSerializer.Serialize(await ConsumerRawSourceAsync(owner.AssessmentId));

		var result = await WithUnitOfWorkAsync(() => MemberChargeReadService(owner).GetMemberChargesAsync(owner.PersonId, 1, 10));

		result.TotalCount.ShouldBe(1);
		var row = result.Items.ShouldHaveSingleItem();
		row.Id.ShouldBe(owner.AssessmentId); row.BookTitle.ShouldBe(fixture.Title); row.AccessionNo.ShouldBe(fixture.RecordedAccession);
		var json = JsonSerializer.SerializeToElement(row, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
		json.GetProperty("bookId").GetGuid().ShouldBe(fixture.BookId);
		json.GetProperty("copyId").GetGuid().ShouldBe(fixture.CopyId);
		json.GetProperty("copyLinkSource").GetString().ShouldBe("CopyId");
		json.GetProperty("requiresCopyReconciliation").GetBoolean().ShouldBeTrue();
		JsonSerializer.Serialize(await ConsumerRawSourceAsync(owner.AssessmentId)).ShouldBe(before);
	}
}

