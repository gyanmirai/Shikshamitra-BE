using System.Threading.Tasks;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryStaffRecordedScope_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Each_Recorded_Staff_Assignment_Should_Retain_Loan_Access_After_Current_Assignments_Are_Replaced(bool useSecondAssignment)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		var loan = await CheckoutStaffAsync(fixture);
		await MoveStaffAsync(fixture);
		await SetReaderStageAsync(fixture, useSecondAssignment ? fixture.SecondStageId : fixture.Context.StageId);
		var scope = await GetRequiredService<IUserScopeAccessService>().GetCurrentUserScopeAsync();
		(await WithUnitOfWorkAsync(() => GetRequiredService<LibraryStoredLoanScope>().CanReadAsync(loan.Id, scope))).ShouldBeTrue();
		(await GetRequiredService<ILibraryLoanAppService>().GetAsync(loan.Id)).Id.ShouldBe(loan.Id);
	}

	[Fact]
	public async Task A_Later_Staff_Assignment_Should_Not_Grant_Access_To_A_Loan_Issued_Under_Different_Assignments()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		var loan = await CheckoutStaffAsync(fixture);
		await MoveStaffAsync(fixture);
		await SetReaderStageAsync(fixture, fixture.LaterStageId);
		(await Should.ThrowAsync<BusinessException>(() => GetRequiredService<ILibraryLoanAppService>().GetAsync(loan.Id)))
			.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
	}
}
