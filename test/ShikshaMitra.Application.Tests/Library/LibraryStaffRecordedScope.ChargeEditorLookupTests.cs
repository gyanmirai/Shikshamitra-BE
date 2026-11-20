using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Charges;
using ShikshaMitra.Library.Charges.Editor;
using ShikshaMitra.Library.Shared;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryStaffRecordedScope_Tests
{
	[Fact]
	public async Task Charge_Editor_Lookups_Should_Work_Under_Their_Own_Add_And_View_Grants()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedChargeEditorLookupAsync();
		var service = await ActualChargeEditorLookupsAsync(
			ShikshaMitraSubmodulePermissions.Library.Charges.View,
			ShikshaMitraSubmodulePermissions.Library.Charges.Add);
		var category = await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(fixture.Source.SourceId)).ChargeCategoryId);

		var categories = await WithUnitOfWorkAsync(() => service.GetCategoriesAsync(fixture.Scope.Context.Catalog.YearId,
			search: "Reviewed staff assessment"));
		var categoryOption = categories.Items.Single();
		categoryOption.Id.ShouldBe(category);
		categoryOption.AcademicYearId.ShouldBe(fixture.Scope.Context.Catalog.YearId);
		categoryOption.Name.ShouldBe("Reviewed staff assessment");
		categoryOption.Amount.ShouldBe(75m);
		categoryOption.IsFixed.ShouldBeFalse();
		var members = await WithUnitOfWorkAsync(() => service.SearchMembersAsync(fixture.Scope.Context.Catalog.YearId, fixture.Identifier));
		members.TotalCount.ShouldBe(1);
		var member = members.Items.Single();
		member.Id.ShouldBe(fixture.Scope.EmployeeId);
		member.MemberName.ShouldBe("Staff Borrower");
		member.MemberIdentifier.ShouldBe(fixture.Identifier);
		member.Role.ShouldBe(1);
		member.HasActiveLibraryMembership.ShouldBeTrue();
		member.CanAssignDirect.ShouldBeTrue();
		member.RequiresLoanSource.ShouldBeFalse();
		var resolved = await WithUnitOfWorkAsync(() => service.ResolveMemberAsync(fixture.Scope.Context.Catalog.YearId, fixture.Identifier));
		resolved.Id.ShouldBe(member.Id);
		resolved.MemberIdentifier.ShouldBe(member.MemberIdentifier);
		var loans = await WithUnitOfWorkAsync(() => service.GetLoanSourcesAsync(fixture.Scope.Context.Catalog.YearId, fixture.Scope.EmployeeId));
		loans.TotalCount.ShouldBe(1);
		loans.Items.Single().LoanId.ShouldBe(fixture.Loan.Id);
		loans.Items.Single().CanonicalMemberId.ShouldBe(fixture.Scope.EmployeeId);
		loans.Items.Single().LoanAcademicYearId.ShouldBe(fixture.Scope.Context.Catalog.YearId);
		loans.Items.Single().Role.ShouldBe(1);
		loans.Items.Single().CopyId.ShouldBe(fixture.Scope.Context.Catalog.CopyId);
	}

	[Theory]
	[InlineData("Categories")]
	[InlineData("SearchMembers")]
	[InlineData("ResolveMember")]
	[InlineData("LoanSources")]
	[InlineData("UpdateContext")]
	public async Task Charge_Editor_Lookup_Should_Require_The_Exact_Owning_Action(string operation)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedChargeEditorLookupAsync();
		var service = await ActualChargeEditorLookupsAsync(ShikshaMitraSubmodulePermissions.Library.Charges.View);

		await Should.ThrowAsync<AbpAuthorizationException>(() => CallChargeEditorLookupAsync(service, fixture, operation));
	}

	[Fact]
	public async Task Charge_Editor_Lookups_Should_Require_View_Even_When_Both_Actions_Are_Granted()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedChargeEditorLookupAsync();
		var service = await ActualChargeEditorLookupsAsync(
			ShikshaMitraSubmodulePermissions.Library.Charges.Add,
			ShikshaMitraSubmodulePermissions.Library.Charges.Update);

		foreach (var operation in new[] { "Categories", "SearchMembers", "ResolveMember", "LoanSources", "UpdateContext" })
			await Should.ThrowAsync<AbpAuthorizationException>(() => CallChargeEditorLookupAsync(service, fixture, operation));
	}

	[Fact]
	public async Task Charge_Editor_Category_Lookup_Should_Filter_Tenant_Year_And_Search_Before_Counting_And_Paging()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedChargeEditorLookupAsync();
		var categories = await SeedChargeEditorCategoryIsolationAsync(fixture);
		var service = await ActualChargeEditorLookupsAsync(
			ShikshaMitraSubmodulePermissions.Library.Charges.View,
			ShikshaMitraSubmodulePermissions.Library.Charges.Add);

		var first = await WithUnitOfWorkAsync(() => service.GetCategoriesAsync(fixture.Scope.Context.Catalog.YearId, page: 1, pageSize: 1, search: categories.Search));
		var second = await WithUnitOfWorkAsync(() => service.GetCategoriesAsync(fixture.Scope.Context.Catalog.YearId, page: 2, pageSize: 1, search: categories.Search));
		var third = await WithUnitOfWorkAsync(() => service.GetCategoriesAsync(fixture.Scope.Context.Catalog.YearId, page: 3, pageSize: 1, search: categories.Search));

		first.TotalCount.ShouldBe(2);
		second.TotalCount.ShouldBe(2);
		third.TotalCount.ShouldBe(2);
		first.Items.Count.ShouldBe(1);
		second.Items.Count.ShouldBe(1);
		third.Items.ShouldBeEmpty();
		var foundIds = first.Items.Concat(second.Items).Select(row => row.Id).ToArray();
		foundIds.Distinct().Count().ShouldBe(2);
		foundIds.ShouldContain(categories.FirstId);
		foundIds.ShouldContain(categories.SecondId);
		foundIds.ShouldNotContain(categories.OtherYearId);
		foundIds.ShouldNotContain(categories.ForeignTenantId);
		first.Items.Concat(second.Items).ShouldAllBe(row => row.AcademicYearId == fixture.Scope.Context.Catalog.YearId);
		// A different real school's year must not become selectable merely because this actor
		// has tenant-wide authority in the current school.
		await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() => service.GetCategoriesAsync(categories.ForeignYearId,
			search: categories.Search)));
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Charge_Editor_Loan_Source_Discovery_Should_Use_Recorded_Staff_Scope_Instead_Of_Current_Placement(bool originalScope)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedChargeEditorLookupAsync();
		if (originalScope) await SetReaderStageAsync(fixture.Scope, fixture.Scope.Context.StageId);
		else await SetActualFinanceReaderStageAsync(fixture.Scope, fixture.Scope.LaterStageId);
		await AssertChargeHandoffScopeAsync(fixture.Scope, fixture.Loan.Id, storedLoan: originalScope, currentEmployee: !originalScope);
		var service = await ActualChargeEditorLookupsAsync(
			ShikshaMitraSubmodulePermissions.Library.Charges.View,
			ShikshaMitraSubmodulePermissions.Library.Charges.Add);

		var members = await WithUnitOfWorkAsync(() => service.SearchMembersAsync(fixture.Scope.Context.Catalog.YearId, fixture.Identifier));
		members.TotalCount.ShouldBe(1);
		var employee = members.Items.Single();
		employee.Id.ShouldBe(fixture.Scope.EmployeeId);
		employee.Role.ShouldBe(1);
		employee.RequiresLoanSource.ShouldBe(originalScope);
		employee.CanAssignDirect.ShouldBe(!originalScope);
		var sources = await WithUnitOfWorkAsync(() => service.GetLoanSourcesAsync(fixture.Scope.Context.Catalog.YearId, fixture.Scope.EmployeeId, pageSize: 1));
		var next = await WithUnitOfWorkAsync(() => service.GetLoanSourcesAsync(fixture.Scope.Context.Catalog.YearId, fixture.Scope.EmployeeId, page: 2, pageSize: 1));
		sources.TotalCount.ShouldBe(originalScope ? 1 : 0);
		next.TotalCount.ShouldBe(originalScope ? 1 : 0);
		next.Items.ShouldBeEmpty();
		if (originalScope)
		{
			var loan = sources.Items.Single();
			loan.LoanId.ShouldBe(fixture.Loan.Id);
			loan.CanonicalMemberId.ShouldBe(fixture.Scope.EmployeeId);
			loan.LoanAcademicYearId.ShouldBe(fixture.Scope.Context.Catalog.YearId);
			loan.SchoolIssueDate.ShouldBe(new DateOnly(2026, 11, 17));
			loan.SchoolDueDate.ShouldBe(new DateOnly(2026, 11, 24));
		}
		else sources.Items.ShouldBeEmpty();
	}

	[Fact]
	public async Task Charge_Editor_Update_Context_Should_Require_Update_Instead_Of_Add_And_Preserve_The_Existing_Source()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedChargeEditorLookupAsync();
		await SetReaderStageAsync(fixture.Scope, fixture.Scope.Context.StageId);
		await AssertChargeHandoffScopeAsync(fixture.Scope, fixture.Loan.Id, storedLoan: true, currentEmployee: false);
		var service = await ActualChargeEditorLookupsAsync(
			ShikshaMitraSubmodulePermissions.Library.Charges.View,
			ShikshaMitraSubmodulePermissions.Library.Charges.Update);

		var result = await WithUnitOfWorkAsync(() => service.GetUpdateContextAsync(fixture.Source.SourceId));

		result.Assessment.Id.ShouldBe(fixture.Source.SourceId);
		result.Assessment.MemberId.ShouldBe(fixture.Scope.EmployeeId);
		result.Assessment.BookIssueId.ShouldBe(fixture.Loan.Id);
		result.Assessment.AcademicYearId.ShouldBe(fixture.Scope.Context.Catalog.YearId);
		result.Assessment.SourceVersion.ShouldBe(fixture.Source.SourceVersion);
		result.Assessment.SchoolChargeDate.ShouldBe(new DateOnly(2026, 11, 17));
		result.Assessment.SchoolDueDate.ShouldBe(new DateOnly(2026, 11, 30));
		result.CanAmend.ShouldBeTrue();
		result.RequiresContextReview.ShouldBeFalse();
		await WithUnitOfWorkAsync(async () =>
		{
			var charge = await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(fixture.Source.SourceId);
			charge.BookIssueId.ShouldBe(fixture.Loan.Id);
			charge.AcademicYearId.ShouldBe(fixture.Scope.Context.Catalog.YearId);
			charge.GetAssessmentVersion().ShouldBe(fixture.Source.SourceVersion);
			charge.FinanceSourceLinkId.ShouldBeNull();
			charge.PaymentStatus.ShouldBe(ChargePaymentStatus.Pending);
		});
	}

	[Fact]
	public async Task Charge_Create_Should_Continue_To_Accept_An_Active_Current_Direct_Assessment_With_Explicit_Context()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedChargeEditorLookupAsync();
		_ = await ActualChargeEditorLookupsAsync(
			ShikshaMitraSubmodulePermissions.Library.Charges.View,
			ShikshaMitraSubmodulePermissions.Library.Charges.Add);
		await WithUnitOfWorkAsync(async () =>
		{
			// Scalar projection proves the persisted member flag without loading that
			// navigation into the later Create UOW or supplying an eligible tracked object.
			var queries = GetRequiredService<IAsyncQueryableExecuter>();
			var employee = await queries.SingleAsync((await GetRequiredService<IRepository<Employee, Guid>>().GetQueryableAsync())
				.Where(row => row.Id == fixture.Scope.EmployeeId)
				.Select(row => new { row.EmployeeStatusType, IsLibraryMember = row.AdditionalDetails != null && row.AdditionalDetails.LibraryMember }));
			employee.EmployeeStatusType.ShouldBe(EmployeeStatusType.Active);
			employee.IsLibraryMember.ShouldBeTrue();
			(await queries.AnyAsync((await GetRequiredService<ILibraryAccessService>().GetEmployeesAsync())
				.Where(row => row.Id == fixture.Scope.EmployeeId))).ShouldBeTrue();
		});
		var categoryId = await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(fixture.Source.SourceId)).ChargeCategoryId);
		var input = new CreateChargeRequest
		{
			MemberId = fixture.Scope.EmployeeId, ChargeCategoryId = categoryId, Amount = 100.75m,
			AcademicYearId = fixture.Scope.Context.Catalog.YearId,
			SchoolChargeDate = new(2026, 11, 17), SchoolDueDate = new(2027, 4, 2),
			Remarks = "Explicit direct assessment; due date may follow the assessment year."
		};

		var created = await GetRequiredService<ILibraryChargeAppService>().CreateAsync(input);

		await WithUnitOfWorkAsync(async () =>
		{
			var charge = await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(Guid.Parse(created.Id));
			charge.MemberId.ShouldBe(fixture.Scope.EmployeeId);
			charge.MemberRole.ShouldBe(1);
			charge.BookIssueId.ShouldBeNull();
			charge.AcademicYearId.ShouldBe(input.AcademicYearId);
			charge.Amount.ShouldBe(100.75m);
			charge.SchoolChargeDate.ShouldBe(input.SchoolChargeDate);
			charge.SchoolDueDate.ShouldBe(input.SchoolDueDate);
			charge.ChargeDate.ShouldBeNull();
			charge.DueDate.ShouldBeNull();
			charge.PaymentStatus.ShouldBe(ChargePaymentStatus.Pending);
			charge.FinanceSourceLinkId.ShouldBeNull();
		});
	}
}
