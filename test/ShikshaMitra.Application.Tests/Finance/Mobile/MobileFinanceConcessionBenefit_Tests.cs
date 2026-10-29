using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Mobile;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Academic.Mobile;

public partial class MobileAcademicAppService_Tests
{
	[Theory]
	[InlineData(ConcessionRequestStatus.Pending)]
	[InlineData(ConcessionRequestStatus.Approved)]
	[InlineData(ConcessionRequestStatus.PartiallyReversed)]
	public Task Family_concession_keeps_typed_contract_and_benefit_guards_with_canonical_cancel(ConcessionRequestStatus blockingStatus) =>
		WithUnitOfWorkAsync(new AbpUnitOfWorkOptions { IsTransactional = false }, async () =>
	{
		var tenantId = Guid.NewGuid();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, true);
		using var identity = FinanceIdentity(f.User.Id);
		foreach (var permission in new[] { MobileFinancePermissions.ViewConcessions, MobileFinancePermissions.RequestConcession,
			MobileFinancePermissions.EditConcession, MobileFinancePermissions.SubmitConcession, MobileFinancePermissions.CancelConcession })
			await GrantFinancePermissionAsync(f.User.Id, permission);
		var category = await Insert(new FeeCategory(Guid.NewGuid(), tenantId, f.Year.Id, "Benefit fees", null));
		var income = await Insert(new TransactionCategory(Guid.NewGuid(), tenantId, f.Year.Id, TransactionType.Income, "Benefit income", null));
		var head = await Insert(new FeeHead(Guid.NewGuid(), tenantId, f.Year.Id, category.Id, "Tuition", null, true, null, income.Id));
		var type = await Insert(new ConcessionType(Guid.NewGuid(), tenantId, f.Year.Id, "Hardship"));
		var otherType = await Insert(new ConcessionType(Guid.NewGuid(), tenantId, f.Year.Id, "Sibling"));
		var policyId = Guid.NewGuid();
		var policy = new ConcessionPolicySnapshot(policyId, f.Year.Id, "Family benefit policy", "FAMILY", ConcessionPolicyStatus.Active, false,
			ConcessionValueSource.RequestEnteredWithinLimits, ConcessionCalculationMethod.FixedAmount, null, null, null, 500m,
			ConcessionApplyOn.AllOutstanding, RoundingMethodType.RoundUp, 1, [type.Id, otherType.Id]);
		var version = await Insert(new FinancePolicyVersion(Guid.NewGuid(), tenantId, "concession", policyId, f.Year.Id, 1,
			policy.PolicyName, "Active", JsonSerializer.Serialize(policy), "create"));
		var assignment = await Insert(new StudentFeeStructure(Guid.NewGuid(), tenantId, f.Student.Id, Guid.NewGuid(), f.Year.Id,
			policyId, version.Id, ConcessionFeeHeadScope.EntireFeeStructure, []));
		var due = new DueFee(Guid.NewGuid(), tenantId, f.Student.Id, head.Id,
			(ShikshaMitra.Finance.Fees.Collections.MonthEnum)(DateTime.Today.Month - 1), DateTime.Today.Year, false, 500m, DateTime.Today);
		due.BindAssignment(assignment.Id);
		await Insert(due);
		var service = FamilyService<ConcessionRequestAppService>();
		var input = new MobileConcessionInput
		{
			StudentFeeStructureId = assignment.Id, ConcessionPolicyId = policyId, ConcessionPolicyVersionId = version.Id,
			ConcessionTypeId = type.Id, IdempotencyKey = Guid.NewGuid().ToString("N"),
			DueFeeConcessions = [new() { DueFeeId = due.Id, FeeHeadId = head.Id, Amount = "200.00" }]
		};
		var draft = await service.FamilySaveAsync(f.Student.Id, f.Year.Id, null, input);
		draft.TotalRequestedConcessionAmount.ShouldBe("200.00");
		draft.FeeHeadConcessions.ShouldHaveSingleItem().FeeHeadName.ShouldBe(head.Name);
		draft.AvailableActions.ShouldContain("edit");
		draft.AvailableActions.ShouldContain("submit");

		var blocking = new ConcessionRequest(Guid.NewGuid(), tenantId, f.Student.Id, assignment.Id, policyId, version.Id,
			type.Id, f.Year.Id, f.Student.AcademicBoardId ?? Guid.Empty, f.Student.AcademicMediumId,
			f.Student.StandardId!.Value, f.Student.DivisionId!.Value, "Existing annual benefit");
		if (blockingStatus != ConcessionRequestStatus.Pending)
		{
			var allocation = new ConcessionDueFeeAllocation(Guid.NewGuid(), tenantId, blocking.Id, due.Id, head.Id, 200m);
			blocking.Approve(Guid.NewGuid(), "Approved annual benefit", [allocation], DateTime.UtcNow);
			if (blockingStatus == ConcessionRequestStatus.PartiallyReversed)
				blocking.RecordReversals([new ConcessionReversal(Guid.NewGuid(), tenantId, blocking.Id, allocation.Id,
					due.Id, head.Id, 50m, "Partially reverse annual benefit", Guid.NewGuid(), DateTime.UtcNow)]);
		}
		await FinanceRequestAsync(() => Insert(blocking));
		var expectedCode = blockingStatus == ConcessionRequestStatus.Pending
			? FinanceConcessionErrorCodes.DuplicatePendingRequest : FinanceConcessionErrorCodes.BenefitAlreadyGranted;
		var context = await FinanceRequestAsync(() => service.FamilyEligibilityAsync(f.Student.Id, f.Year.Id, assignment.Id));
		context.StudentFeeStructureId.ShouldBe(assignment.Id);
		context.EligibleDueFees.ShouldHaveSingleItem().OriginalAmount.ShouldBe("500.00");
		context.ConcessionPolicies.ShouldHaveSingleItem().ConcessionPolicyVersionId.ShouldBe(version.Id);
		var contextJson = JsonSerializer.SerializeToElement(context);
		var option = contextJson.GetProperty("ConcessionTypes").EnumerateArray().Single(x => x.GetProperty("ConcessionTypeId").GetGuid() == type.Id);
		option.GetProperty("BenefitBlocker").GetProperty("Code").GetString().ShouldBe(expectedCode);
		option.GetProperty("BenefitBlocker").GetProperty("BlockingRequestId").GetGuid().ShouldBe(blocking.Id);
		contextJson.GetProperty("ConcessionTypes").EnumerateArray().Single(x => x.GetProperty("ConcessionTypeId").GetGuid() == otherType.Id)
			.GetProperty("BenefitBlocker").ValueKind.ShouldBe(JsonValueKind.Null);
		var detail = await FinanceRequestAsync(() => service.FamilyGetAsync(f.Student.Id, f.Year.Id, draft.ConcessionRequestId));
		detail.AvailableActions.ShouldNotContain("edit");
		detail.AvailableActions.ShouldNotContain("submit");
		detail.AvailableActions.ShouldContain("cancel");
		JsonSerializer.SerializeToElement(detail).GetProperty("BenefitBlocker").GetProperty("Code").GetString().ShouldBe(expectedCode);
		detail.TotalRequestedConcessionAmount.ShouldBe("200.00");
		detail.DueFeeConcessions.ShouldHaveSingleItem().DueFeeId.ShouldBe(due.Id);
		var previewError = await Should.ThrowAsync<BusinessException>(() => service.FamilyPreviewAsync(f.Student.Id, f.Year.Id, input));
		previewError.Code.ShouldBe(expectedCode);
		input.IdempotencyKey = Guid.NewGuid().ToString("N");
		var createError = await Should.ThrowAsync<BusinessException>(() => service.FamilySaveAsync(f.Student.Id, f.Year.Id, null, input));
		createError.Code.ShouldBe(expectedCode);
		input.ConcurrencyStamp = detail.ConcurrencyStamp;
		var editError = await Should.ThrowAsync<BusinessException>(() => service.FamilySaveAsync(f.Student.Id, f.Year.Id, draft.ConcessionRequestId, input));
		editError.Code.ShouldBe(expectedCode);
		var submitError = await Should.ThrowAsync<BusinessException>(() => service.FamilyActionAsync(f.Student.Id, f.Year.Id, draft.ConcessionRequestId,
			new() { IdempotencyKey = Guid.NewGuid().ToString("N"), ConcurrencyStamp = detail.ConcurrencyStamp }, true));
		submitError.Code.ShouldBe(expectedCode);

		// Cancellation uses the mobile/web canonical request stamp, even when a benefit blocks editing.
		await Should.ThrowAsync<BusinessException>(() => service.FamilyActionAsync(f.Student.Id, f.Year.Id, draft.ConcessionRequestId,
			new() { IdempotencyKey = Guid.NewGuid().ToString("N"), ConcurrencyStamp = detail.ConcurrencyStamp,
				RequestConcurrencyStamp = "stale", Reason = "Cancel duplicate request" }, false));
		var cancel = new MobileFinanceRequestAction { IdempotencyKey = Guid.NewGuid().ToString("N"),
			RequestConcurrencyStamp = detail.ConcurrencyStamp, Reason = "Cancel duplicate request" };
		var cancelled = await service.FamilyActionAsync(f.Student.Id, f.Year.Id, draft.ConcessionRequestId, cancel, false);
		cancelled.Status.ShouldBe("Cancelled");
		cancelled.CancellationReason.ShouldBe(cancel.Reason);
		cancelled.AvailableActions.ShouldBeEmpty();
		var replay = await service.FamilyActionAsync(f.Student.Id, f.Year.Id, draft.ConcessionRequestId, cancel, false);
		replay.ConcessionRequestId.ShouldBe(cancelled.ConcessionRequestId);
		replay.ConcurrencyStamp.ShouldBe(cancelled.ConcurrencyStamp);
		(await GetRequiredService<IRepository<DueFee, Guid>>().GetAsync(due.Id)).OutstandingAmount.ShouldBe(500m);
		(await GetRequiredService<IConcessionRequestRepository>().GetWithDetailsAsync(blocking.Id)).Status.ShouldBe(blockingStatus);
	});
}
