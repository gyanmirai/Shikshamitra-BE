using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using ShikshaMitra.Finance.Fees.FeeRefund.Repositories;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Theory]
	[InlineData(FinanceExternalPayerKind.Student)]
	[InlineData(FinanceExternalPayerKind.Employee)]
	public async Task Typed_Source_Refund_Detail_Should_Use_The_Actual_Payer_And_Source_Without_A_Fee_Assignment(FinanceExternalPayerKind kind)
	{
		var fixture = await SeedSubmittedTypedRefundAsync(kind);
		var owner = fixture.Source.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);

		var detail = await WithUnitOfWorkAsync(() => DownstreamRefundService(owner).GetAsync(fixture.Submitted.Id));

		detail.StudentId.ShouldBe(kind == FinanceExternalPayerKind.Student ? owner.PersonId : null);
		detail.EmployeeId.ShouldBe(kind == FinanceExternalPayerKind.Employee ? owner.PersonId : null);
		detail.PayerName.ShouldNotBeNullOrWhiteSpace();
		detail.FinanceExternalSourceLinkId.ShouldBe(fixture.Submitted.FinanceExternalSourceLinkId);
		detail.StudentFeeStructureId.ShouldBeNull();
		detail.FinanceFeeAccountId.ShouldBeNull();
		detail.RefundPolicyVersionId.ShouldBe(fixture.Source.PolicyVersionId);
		detail.OriginalReceiptId.ShouldBe(fixture.Source.Collection.ReceiptId);
		detail.Status.ShouldBe(FeeRefundStatus.Submitted);
		detail.TotalAmount.ShouldBe(15.25m);
		detail.CalculatedAmount.ShouldBe(15m);
		detail.ProcessedAmount.ShouldBe(0m);
		detail.RefundSubledgerNetAmount.ShouldBe(0m);
		detail.IsRefundSubledgerBalanced.ShouldBe(true);
		detail.Lines.Single().SourceAllocationId.ShouldBe(fixture.Source.AllocationId);
	}

	[Fact]
	public async Task Typed_Employee_Refund_List_Should_Include_The_Source_Before_Counting_And_Paging()
	{
		var fixture = await SeedSubmittedTypedRefundAsync(FinanceExternalPayerKind.Employee);
		var owner = fixture.Source.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);

		var list = await WithUnitOfWorkAsync(() => DownstreamRefundService(owner).GetListAsync(new()
		{
			AcademicYearId = owner.YearId, WorkflowVersion = 1, MaxResultCount = 1, SkipCount = 0
		}));

		list.TotalCount.ShouldBe(1);
		list.Items.Single().Id.ShouldBe(fixture.Submitted.Id);
		list.Items.Single().EmployeeId.ShouldBe(owner.PersonId);
		list.Items.Single().StudentId.ShouldBeNull();
		list.Items.Single().FinanceExternalSourceLinkId.ShouldBe(fixture.Submitted.FinanceExternalSourceLinkId);
	}

	[Fact]
	public async Task Typed_Source_Refund_Approval_Should_Reject_The_Actual_Submitter_Even_With_The_Decision_Grant()
	{
		var fixture = await SeedSubmittedTypedRefundAsync(FinanceExternalPayerKind.Employee);
		var owner = fixture.Source.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		await WithUnitOfWorkAsync(() => GetRequiredService<IPermissionManager>().SetAsync(
			ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Approve, "R", $"external-finance-{owner.ActorId:N}", true));
		(await WithUnitOfWorkAsync(() => new FinanceServices(ServiceProvider, owner, useStoredSource: true)
			.GetRequiredService<IPermissionChecker>().IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Approve))).ShouldBeTrue();

		var error = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
			DownstreamRefundService(owner).ApproveAsync(fixture.Submitted.Id, new() { Notes = "Maker attempted own approval." })));

		error.Code.ShouldBe(FeeRefundErrorCodes.MakerCheckerViolation);
		await AssertTypedRefundStillSubmittedAsync(fixture);
	}

	[Fact]
	public async Task Typed_Employee_Refund_Approval_Should_Reserve_Calculated_Money_Without_Recording_A_Payout()
	{
		var fixture = await SeedSubmittedTypedRefundAsync(FinanceExternalPayerKind.Employee);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(fixture.Administrator.TenantId);
		using var actor = SignIn(fixture.Administrator);
		fixture.Administrator.ActorId.ShouldNotBe(fixture.Source.Settlement.Owner.ActorId);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IUserScopeService>().IsAdminUserAsync(fixture.Administrator.ActorId))).ShouldBeTrue();

		var approved = await WithUnitOfWorkAsync(() => DownstreamRefundService(fixture.Administrator)
			.ApproveAsync(fixture.Submitted.Id, new() { Notes = "Independent school administrator reviewed original payment and policy." }));

		approved.Status.ShouldBe(FeeRefundStatus.Approved);
		approved.TotalAmount.ShouldBe(15.25m);
		approved.CalculatedAmount.ShouldBe(15m);
		approved.ApprovedAmount.ShouldBe(15m);
		approved.ProcessedAmount.ShouldBe(0m);
		await WithUnitOfWorkAsync(async () =>
		{
			var stored = await GetRequiredService<IFeeRefundRepository>().FindWithLinesAsync(approved.Id);
			stored.ShouldNotBeNull();
			stored.EmployeeId.ShouldBe(fixture.Source.Settlement.Owner.PersonId);
			stored.Reservations.Single().Amount.ShouldBe(15m);
			stored.Reservations.Single().Status.ShouldBe(FeeRefundReservationStatus.Active);
			stored.Settlements.ShouldBeEmpty();
			(await GetRequiredService<IRepository<FeeRefundAccountingEntry, Guid>>().CountAsync()).ShouldBe(0);
		});
		var remaining = await WithUnitOfWorkAsync(() => TypedRefundService(fixture.Administrator).GetFormAsync(fixture.Source.Settlement.Post.Source));
		remaining.Sources.Single().CommittedAmount.ShouldBe(15m);
		remaining.Sources.Single().AvailableAmount.ShouldBe(25.25m);
		remaining.Receivable.RefundedAmount.ShouldBe(0m);
	}

	[Fact]
	public async Task Typed_Employee_Refund_Detail_Should_Deny_A_User_Whose_Allowed_Year_Was_Revoked()
	{
		var fixture = await SeedSubmittedTypedRefundAsync(FinanceExternalPayerKind.Employee);
		var owner = fixture.Source.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		await WithUnitOfWorkAsync(async () =>
		{
			var assignments = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
			await assignments.DeleteAsync(row => row.TenantId == owner.TenantId && row.UserId == owner.ActorId, true);
			var year = new AcademicYear(Guid.NewGuid(), owner.TenantId, "Unrelated refund year",
				new DateTime(2024, 4, 1), new DateTime(2025, 3, 31));
			await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(year, true);
			await assignments.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), owner.TenantId,
				userId: owner.ActorId, academicYearId: year.Id, description: "Current authority excludes the source year"), true);
			await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(owner.ActorId);
			var scope = await GetRequiredService<IUserScopeService>().GetUserScopeAsync(owner.ActorId);
			scope.IsAdmin.ShouldBeFalse();
			scope.HasWildcardAcademicYear.ShouldBeFalse();
			scope.AcademicYearIds.ShouldNotContain(owner.YearId);
		});

		await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(() =>
			DownstreamRefundService(owner).GetAsync(fixture.Submitted.Id)));

		await AssertTypedRefundStillSubmittedAsync(fixture);
	}

	private Task AssertTypedRefundStillSubmittedAsync(DownstreamRefundFixture fixture) => WithUnitOfWorkAsync(async () =>
	{
		var stored = await GetRequiredService<IFeeRefundRepository>().FindWithLinesAsync(fixture.Submitted.Id);
		stored.ShouldNotBeNull();
		stored.Status.ShouldBe(FeeRefundStatus.Submitted);
		stored.TotalAmount.ShouldBe(15.25m);
		stored.ApprovedAmount.ShouldBe(0m);
		stored.Reservations.ShouldBeEmpty();
		stored.Settlements.ShouldBeEmpty();
		(await GetRequiredService<IRepository<FeeRefundAccountingEntry, Guid>>().CountAsync()).ShouldBe(0);
	});
}
