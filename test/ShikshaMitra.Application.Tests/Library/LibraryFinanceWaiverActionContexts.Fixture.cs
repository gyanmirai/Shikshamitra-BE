using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.ExternalReceivables.Operator;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	private async Task<WaiverReadFixture> SeedWaiverReadFixtureAsync(FinanceExternalWaiverAction action)
	{
		MovedCashSource? recorded = null;
		var settlement = await SeedTypedSettlementAsync(FinanceExternalPayerKind.Employee, requiresWaiverApproval: true,
			prepareSource: async owner => { recorded = await PrepareMovedCashLoanSourceAsync(owner); });
		using var tenant = GetRequiredService<ICurrentTenant>().Change(settlement.Owner.TenantId);
		await ConfigureAndApproveTypedAsync(settlement);
		FinanceExternalWaiverDto? waiver = null;
		if (action != FinanceExternalWaiverAction.Apply)
		{
			using (SignIn(settlement.Owner))
			{
				var form = await WithUnitOfWorkAsync(() => TypedService(settlement.Owner).GetCollectFormAsync(settlement.Post.Source));
				waiver = await WithUnitOfWorkAsync(() => TypedService(settlement.Owner).RequestWaiverAsync(new()
				{
					Source = settlement.Post.Source, Revision = form.Revision, Amount = 10.10m,
					Reason = "Requested school assistance.", IdempotencyKey = Guid.NewGuid().ToString("N")
				}));
				waiver.State.ShouldBe(FinanceExternalWaiverState.PendingApproval);
			}
			if (action == FinanceExternalWaiverAction.Reverse)
			{
				using var reviewer = SignIn(settlement.Reviewer);
				var form = await WithUnitOfWorkAsync(() => TypedService(settlement.Reviewer).GetCollectFormAsync(settlement.Post.Source));
				waiver = await WithUnitOfWorkAsync(() => TypedService(settlement.Reviewer).ApproveWaiverAsync(new()
				{
					Source = settlement.Post.Source, Revision = form.Revision, WaiverRequestId = waiver.RequestId,
					Reason = "Independently reviewed the assistance evidence.", IdempotencyKey = Guid.NewGuid().ToString("N")
				}));
				waiver.State.ShouldBe(FinanceExternalWaiverState.Applied);
			}
		}
		return new(settlement, waiver, recorded ?? throw new InvalidOperationException("A sealed historical source must precede posting."));
	}

	private async Task RestrictWaiverReadActionsAsync(WaiverReadFixture fixture, Fixture actor,
		FinanceExternalWaiverAction? action, string? ownReadAction = null)
	{
		var own = ownReadAction ?? WaiverReadAction(action ?? throw new ArgumentNullException(nameof(action)));
		var role = WaiverReadRole(fixture, actor);
		var actions = new[]
		{
			ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.View,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Add,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Update,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Approve,
			ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View,
			ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.Add,
			ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ApplyWaiver,
			ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ApproveWaiver,
			ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.RejectWaiver,
			ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ReverseWaiver,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Add,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Approve,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Reject,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Process,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Reverse,
			ShikshaMitraSubmodulePermissions.Finance.Fees_InvoiceReceipt.View,
			ShikshaMitraSubmodulePermissions.Finance.Auditing.View,
			ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.View,
			ShikshaMitraSubmodulePermissions.Finance.Policies_RefundPolicy.View,
		};
		await WithUnitOfWorkAsync(async () =>
		{
			var permissions = GetRequiredService<IPermissionManager>();
			foreach (var permission in actions) await permissions.SetAsync(permission, "R", role, permission == own);
			await permissions.SetAsync(own, "R", role, true);
		});
		(await OperatorGrantedAsync(actor, own)).ShouldBeTrue();
		(await OperatorGrantedAsync(actor, ShikshaMitraSubmodulePermissions.Library.Charges.View)).ShouldBeTrue();
		foreach (var permission in actions.Where(permission => permission != own))
			(await OperatorGrantedAsync(actor, permission)).ShouldBeFalse();
	}

	private async Task SetWaiverReadPlacementAsync(WaiverReadFixture fixture, Fixture actor, bool currentPlacement)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var source = fixture.RecordedSource!;
			var owner = fixture.Settlement.Owner;
			var employment = GetRequiredService<IRepository<EmployeeOrganizationAssignment, Guid>>();
			await employment.DeleteAsync(row => row.TenantId == owner.TenantId && row.EmployeeId == owner.PersonId, true);
			await employment.InsertAsync(new(Guid.NewGuid(), owner.TenantId, owner.PersonId, source.Current.StageId), true);
			var placement = currentPlacement ? source.Current : source.Original;
			var scopes = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
			await scopes.DeleteAsync(row => row.TenantId == owner.TenantId && row.UserId == actor.ActorId, true);
			await scopes.InsertAsync(new(Guid.NewGuid(), owner.TenantId, userId: actor.ActorId,
				academicYearId: source.LoanYearId, educationBoardId: placement.BoardId,
				educationMediumId: placement.MediumId, educationMediumStageId: placement.StageId,
				description: "Real scoped waiver reader for captured staff obligation"), true);
			await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(actor.ActorId);
		});
	}

	private Task AssertWaiverReadPlacementAsync(WaiverReadFixture fixture, bool currentPlacement) => WithUnitOfWorkAsync(async () =>
	{
		var scope = await GetRequiredService<IUserScopeAccessService>().GetCurrentUserScopeAsync();
		scope.HasTenantWideScope().ShouldBeFalse();
		(await GetRequiredService<LibraryStoredLoanScope>().CanReadAsync(fixture.RecordedSource!.LoanId, scope)).ShouldBe(!currentPlacement);
		(await GetRequiredService<IAsyncQueryableExecuter>().AnyAsync((await GetRequiredService<ILibraryAccessService>()
			.GetEmployeesAsync(scope)).Where(row => row.Id == fixture.Settlement.Owner.PersonId))).ShouldBe(currentPlacement);
	});

	private Task GiveWaiverReaderAnotherYearAsync(Fixture actor) => WithUnitOfWorkAsync(async () =>
	{
		var year = new AcademicYear(Guid.NewGuid(), actor.TenantId, "Other authorized refund year",
			new DateTime(2025, 4, 1), new DateTime(2026, 3, 31));
		await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(year, true);
		var scopes = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		await scopes.DeleteAsync(row => row.TenantId == actor.TenantId && row.UserId == actor.ActorId, true);
		await scopes.InsertAsync(new(Guid.NewGuid(), actor.TenantId, userId: actor.ActorId,
			academicYearId: year.Id, description: "Another real authorized year; no original billing year access"), true);
		await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(actor.ActorId);
	});

	private async Task ExecuteOwnWaiverContextAsync(WaiverReadFixture fixture, Fixture actor,
		FinanceExternalWaiverAction action, FinanceExternalWaiverContextDto context)
	{
		FinanceExternalWaiverDto result;
		if (action == FinanceExternalWaiverAction.Apply)
			result = await WithUnitOfWorkAsync(() => TypedService(actor).RequestWaiverAsync(new()
			{
				Source = context.Source, Revision = context.Revision, Amount = 5.05m,
				Reason = "Requested an explicitly bounded partial assistance amount.", IdempotencyKey = Guid.NewGuid().ToString("N")
			}));
		else if (action == FinanceExternalWaiverAction.Reverse)
			result = await WithUnitOfWorkAsync(() => TypedService(actor).ReverseWaiverAsync(new()
			{
				Source = context.Source, Revision = context.Revision, WaiverApplicationId = context.Requests.Items.Single().ApplicationId!.Value,
				Amount = 4.05m, Reason = "Reversed only the evidenced correction amount.", IdempotencyKey = Guid.NewGuid().ToString("N")
			}));
		else
		{
			var input = new FinanceExternalWaiverDecisionInput
			{
				Source = context.Source, Revision = context.Revision, WaiverRequestId = context.Requests.Items.Single().RequestId,
				Reason = "Independently decided the actual pending assistance request.", IdempotencyKey = Guid.NewGuid().ToString("N")
			};
			result = action == FinanceExternalWaiverAction.Approve
				? await WithUnitOfWorkAsync(() => TypedService(actor).ApproveWaiverAsync(input))
				: await WithUnitOfWorkAsync(() => TypedService(actor).RejectWaiverAsync(input));
		}
		result.Receivable.Source.SourceVersion.ShouldBe(fixture.Settlement.Owner.Version);
		if (action == FinanceExternalWaiverAction.Apply)
		{
			result.State.ShouldBe(FinanceExternalWaiverState.PendingApproval);
			result.RequestedAmount.ShouldBe(5.05m);
			result.Receivable.WaivedAmount.ShouldBe(0m);
		}
		else if (action == FinanceExternalWaiverAction.Approve)
		{
			result.State.ShouldBe(FinanceExternalWaiverState.Applied);
			result.AppliedAmount.ShouldBe(10.10m);
			result.Receivable.OutstandingAmount.ShouldBe(90.65m);
		}
		else if (action == FinanceExternalWaiverAction.Reject)
		{
			result.State.ShouldBe(FinanceExternalWaiverState.Rejected);
			result.AppliedAmount.ShouldBe(0m);
			result.Receivable.OutstandingAmount.ShouldBe(100.75m);
		}
		else
		{
			result.ReversedAmount.ShouldBe(4.05m);
			result.Receivable.WaivedAmount.ShouldBe(6.05m);
			result.Receivable.OutstandingAmount.ShouldBe(94.70m);
		}
	}

	private Task AssertWaiverStillPendingAsync(WaiverReadFixture fixture) => WithUnitOfWorkAsync(async () =>
	{
		var request = await GetRequiredService<IRepository<FinanceExternalWaiverRequest, Guid>>().GetAsync(fixture.Waiver!.RequestId);
		request.State.ShouldBe(FinanceExternalWaiverState.PendingApproval);
		request.Amount.ShouldBe(10.10m);
		request.AppliedWaiverId.ShouldBeNull();
	});

	private static string WaiverReadRole(WaiverReadFixture fixture, Fixture actor) => actor.ActorId == fixture.Settlement.Owner.ActorId
		? $"external-finance-{actor.ActorId:N}" : $"source-reviewer-{actor.ActorId:N}";
	private static string WaiverReadAction(FinanceExternalWaiverAction action) => action switch
	{
		FinanceExternalWaiverAction.Apply => ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ApplyWaiver,
		FinanceExternalWaiverAction.Approve => ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ApproveWaiver,
		FinanceExternalWaiverAction.Reject => ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.RejectWaiver,
		FinanceExternalWaiverAction.Reverse => ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ReverseWaiver,
		_ => throw new ArgumentOutOfRangeException(nameof(action))
	};
	private static void AssertWaiverContextDenied(Exception? error)
	{
		error.ShouldNotBeNull();
		(error is AbpAuthorizationException || error is BusinessException business &&
			(business.Code == UserScopeErrorCodes.AccessDenied || business.Code == LibraryErrorCodes.LibraryChargeNotFound ||
			 business.Code == LibraryFinanceErrorCodes.InvalidSource)).ShouldBeTrue(
			"Only an actual action/tenant/source/year denial satisfies this boundary; missing-feature/fixture exceptions do not.");
	}

	private static RefundPolicyUpdateModel RefundMethodDriftModel(RefundPolicySnapshot snapshot, string description) => new(
		snapshot.PolicyName, snapshot.PolicyCode, description, snapshot.Status, snapshot.IsDefault, snapshot.FeeHeadScope,
		snapshot.ApplicableFeeHeadIds, snapshot.IncludePaidCharges, snapshot.IncludePaidLateFee, snapshot.AllowPartialRefund,
		snapshot.AmountMode, snapshot.PercentageValue, snapshot.FixedAmount, snapshot.MaximumPerRequest, snapshot.MaximumPerStudentYear,
		snapshot.RoundingMethod, snapshot.DeductionType, snapshot.DeductionValue, snapshot.MaximumDeduction,
		snapshot.DeductionExplanation, [RefundPayoutMethod.BankTransfer], snapshot.ProcessingTargetDays,
		snapshot.ExpenseCategoryId, snapshot.DefaultPayingBankAccountId);

	private sealed record WaiverReadFixture(TypedSettlement Settlement, FinanceExternalWaiverDto? Waiver, MovedCashSource? RecordedSource);
}
