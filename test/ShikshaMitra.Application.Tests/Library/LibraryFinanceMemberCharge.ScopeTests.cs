using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	// M05: a readable current profile is not authority over an old assessment.
	[Theory]
	[InlineData(FinanceExternalPayerKind.Student)]
	[InlineData(FinanceExternalPayerKind.Employee)]
	public async Task Member_Charge_History_Should_Exclude_Old_Source_For_A_Moved_In_Current_Target(FinanceExternalPayerKind kind)
	{
		var moved = await SeedMemberChargeMovedInSourceAsync(kind);
		var owner = moved.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var denied = await RestrictMemberChargeReadAsync(owner);
		await AssertMemberChargeReadAuthorityAsync(owner, denied);
		await AssertMovedInMemberChargePreconditionsAsync(moved);
		var before = JsonSerializer.Serialize(await ConsumerRawSourceAsync(owner.AssessmentId));

		var first = await WithUnitOfWorkAsync(() => MemberChargeReadService(owner).GetMemberChargesAsync(owner.PersonId, 1, 1));
		var second = await WithUnitOfWorkAsync(() => MemberChargeReadService(owner).GetMemberChargesAsync(owner.PersonId, 2, 1));

		first.TotalCount.ShouldBe(0); first.Items.ShouldBeEmpty();
		second.TotalCount.ShouldBe(0); second.Items.ShouldBeEmpty();
		JsonSerializer.Serialize(await ConsumerRawSourceAsync(owner.AssessmentId)).ShouldBe(before);
	}

	// M06 control: this endpoint remains member-profile-gated. Recorded source
	// authority alone does not grant the current Employee's private profile.
	[Fact]
	public async Task Member_Charge_History_Should_Keep_The_Current_Target_Profile_Gate_For_Recorded_Only_Staff_Authority()
	{
		var moved = await SeedMovedTypedCashAsync();
		var owner = moved.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		await SetMovedCashReaderScopeAsync(moved, currentPlacement: false);
		var denied = await RestrictMemberChargeReadAsync(owner);
		await AssertMemberChargeReadAuthorityAsync(owner, denied);
		await WithUnitOfWorkAsync(async () =>
		{
			var context = await GetRequiredService<ILibraryAccessService>().GetContextAsync(owner.YearId);
			context.UserScope.HasTenantWideScope().ShouldBeFalse();
			context.AllowedAcademicYearIds.ShouldContain(owner.YearId);
			(await GetRequiredService<LibraryStoredLoanScope>().CanReadAsync(moved.Source.LoanId, context.UserScope)).ShouldBeTrue();
			var employees = await GetRequiredService<ILibraryAccessService>().GetEmployeesAsync(context.UserScope);
			(await GetRequiredService<IAsyncQueryableExecuter>().AnyAsync(employees.Where(row => row.Id == owner.PersonId))).ShouldBeFalse();
			var source = await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(owner.AssessmentId);
			source.TenantId.ShouldBe(owner.TenantId); source.MemberId.ShouldBe(owner.PersonId);
			source.BookIssueId.ShouldBe(moved.Source.LoanId); source.AcademicYearId.ShouldBe(owner.YearId);
			source.FinanceSourceLinkId.ShouldNotBeNull();
			var currentAssignment = (await GetRequiredService<IRepository<EmployeeOrganizationAssignment, Guid>>()
				.GetListAsync(row => row.TenantId == owner.TenantId && row.EmployeeId == owner.PersonId)).ShouldHaveSingleItem();
			currentAssignment.EducationMediumStageId.ShouldBe(moved.Source.Current.StageId);
		});
		var before = JsonSerializer.Serialize(await ConsumerRawSourceAsync(owner.AssessmentId));

		var error = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
			MemberChargeReadService(owner).GetMemberChargesAsync(owner.PersonId, 1, 1)));

		error.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		JsonSerializer.Serialize(await ConsumerRawSourceAsync(owner.AssessmentId)).ShouldBe(before);
	}
}

