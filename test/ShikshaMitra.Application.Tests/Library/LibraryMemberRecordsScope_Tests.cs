using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryStaffRecordedScope_Tests
{
	[Theory]
	[InlineData(false, "borrowed")]
	[InlineData(false, "history")]
	[InlineData(false, "reserved")]
	[InlineData(true, "borrowed")]
	[InlineData(true, "history")]
	[InlineData(true, "reserved")]
	public async Task H_Member_Records_Current_Profile_Access_Should_Not_Reveal_Prior_Assignment_Rows_Or_Counts(bool staff, string kind)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		var memberId = staff ? fixture.EmployeeId : fixture.Context.StudentId;
		var loan = await CheckoutStatisticsMemberAsync(fixture, staff);
		var requestId = await SeedStatisticsRequestAsync(fixture, memberId);
		await MoveStatisticsBorrowerAndActorAsync(fixture, staff, originalScope: false);
		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<ILibraryAccessService>().GetMemberIdsAsync()).ShouldContain(memberId);
			var scope = await GetRequiredService<IUserScopeAccessService>().GetCurrentUserScopeAsync();
			(await GetRequiredService<LibraryStoredLoanScope>().CanReadAsync(loan.Id, scope)).ShouldBeFalse();
			(await GetRequiredService<LibraryStoredReservationScope>().CanReadAsync(requestId, scope)).ShouldBeFalse();
		});
		var app = GetRequiredService<ILibraryMemberAppService>();
		if (kind == "borrowed")
		{
			var page = await app.GetBorrowedBooksAsync(memberId);
			page.TotalCount.ShouldBe(0); page.Items.ShouldBeEmpty();
		}
		else if (kind == "history")
		{
			var page = await app.GetBorrowHistoryAsync(memberId);
			page.TotalCount.ShouldBe(0); page.Items.ShouldBeEmpty();
		}
		else
		{
			var page = await app.GetReservedBooksAsync(memberId);
			page.TotalCount.ShouldBe(0); page.Items.ShouldBeEmpty();
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task H_Member_Records_Recorded_Loan_Access_Should_Not_Grant_The_Reassigned_Current_Profile_Or_Contacts(bool staff)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		var memberId = staff ? fixture.EmployeeId : fixture.Context.StudentId;
		var loan = await CheckoutStatisticsMemberAsync(fixture, staff);
		await MoveStatisticsBorrowerAndActorAsync(fixture, staff, originalScope: true);
		await WithUnitOfWorkAsync(async () =>
		{
			var scope = await GetRequiredService<IUserScopeAccessService>().GetCurrentUserScopeAsync();
			(await GetRequiredService<LibraryStoredLoanScope>().CanReadAsync(loan.Id, scope)).ShouldBeTrue();
			(await GetRequiredService<ILibraryAccessService>().GetMemberIdsAsync()).ShouldNotContain(memberId);
		});

		(await Should.ThrowAsync<BusinessException>(() => GetRequiredService<IBookIssueAppService>().GetMemberAsync(memberId)))
			.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
	}
}
