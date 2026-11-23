using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureAppService_CollectionPolicy_Tests
{
	[Theory]
	[InlineData(nameof(StudentFeeStructureAppService.GetOptionalServiceRestartContextAsync))]
	[InlineData(nameof(StudentFeeStructureAppService.PreviewOptionalServiceRestartAsync))]
	[InlineData(nameof(StudentFeeStructureAppService.RestartOptionalServiceAsync))]
	public void Optional_Service_Restart_Requires_Assignment_And_Cancellation_Permissions(string method)
	{
		var policies = typeof(StudentFeeStructureAppService).GetMethod(method)!.GetCustomAttributes<AuthorizeAttribute>()
			.Select(attribute => attribute.Policy).OrderBy(policy => policy);
		policies.ShouldBe(new[]
		{
			ShikshaMitraSubmodulePermissions.Finance.Fees_StructureAssign.Add,
			ShikshaMitraSubmodulePermissions.Finance.Fees_StructureAssign.StopOptionalService,
			ShikshaMitraSubmodulePermissions.Finance.Fees_StructureAssign.View,
		});
	}

	[Fact]
	public async Task Optional_Service_Restart_Also_Adds_Previously_Unselected_Transport_And_Records_History()
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var clock = (FeeStructurePolicyTestModule.FixtureClock)GetRequiredService<IClock>();
		clock.Current = new(2025, 9, 30);
		var (input, headId) = await VariableAssignmentAsync(variable: false, optional: true, addAdditionalHead: true,
			monthlyTerm: true, configuredMonthlyRate: 1200m);
		input.SelectedOptionalFeeHeadIds = [];
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		input.PreviewToken = (await service.PreviewAsync(input)).PreviewToken;
		await service.AssignAsync(input);
		clock.Current = new(2025, 10, 1);
		var dues = GetRequiredService<IRepository<DueFee, Guid>>();
		var required = (await dues.GetListAsync(row => row.StudentId == input.StudentId && row.FeeHeadId != headId))
			.First(row => row.OutstandingAmount > 0);
		required.ApplyPayment(1m);
		await dues.UpdateAsync(required, autoSave: true);
		var policies = GetRequiredService<IProrationPolicyAppService>();
		var policy = await policies.CreateAsync(new()
		{
			AcademicYearId = input.AcademicYearId, PolicyName = "Optional service proration", Status = "Active", IsDefault = true,
			FeeHeadRules = [new() { FeeHeadId = headId, RuleType = "Daily", ApplyOnAdmission = true }],
		});
		var context = await service.GetOptionalServiceRestartContextAsync(input.StudentId, input.AcademicYearId, headId);
		var request = new OptionalServiceRestartInput(input.AcademicYearId, headId, new(2026, 1, 1), new(2026, 1, 31),
			1200m, (await policies.GetAsync(policy.Id)).CurrentVersionId!.Value, context.DefaultPaymentTermVersionId!.Value, Guid.NewGuid());
		var preview = await service.PreviewOptionalServiceRestartAsync(input.StudentId, request);
		preview.AssignedTotal.ShouldBe(1200m);
		await service.RestartOptionalServiceAsync(input.StudentId, request with { PreviewToken = preview.PreviewToken });
		var saved = await service.GetByStudentIdAsync(input.StudentId, input.AcademicYearId);
		saved.CanEditStructure.ShouldBeFalse();
		saved.Assignment!.Heads.Single(head => head.FeeHeadId == headId).Included.ShouldBeTrue();
		(await dues.GetAsync(required.Id)).PaidAmount.ShouldBe(1m);
		(await dues.GetListAsync(row => row.StudentId == input.StudentId && row.FeeHeadId == headId)).Count.ShouldBe(1);

		var history = await service.GetHistoryAsync(input.StudentId, new() { AcademicYearId = input.AcademicYearId, MaxResultCount = 1 });
		history.TotalCount.ShouldBe(2);
		var entry = history.Items.Single();
		entry.Action.ShouldBe("Optional service added");
		entry.Changes.Single(change => change.Field == "New assigned amount").After.ShouldBe(1200m);
		entry.Changes.Single(change => change.Field == "Service start").After.ShouldBe("2026-01-01");
		var older = await service.GetHistoryAsync(input.StudentId,
			new() { AcademicYearId = input.AcademicYearId, MaxResultCount = 1, SkipCount = 1 });
		older.Items.Single().Action.ShouldBe("Fee structure assigned");
		using var outsideTenant = _currentTenant.Change(Guid.NewGuid());
		(await Should.ThrowAsync<BusinessException>(() => service.GetHistoryAsync(input.StudentId,
			new() { AcademicYearId = input.AcademicYearId }))).Code.ShouldBe(AssignmentErrorCodes.EnrollmentRequired);
	}

	[Fact]
	public async Task Optional_Service_Restart_Previews_Only_The_Selected_Service_Days()
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var (studentId, request, _, _) = await StoppedOptionalServiceAsync();
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		var dues = GetRequiredService<IRepository<DueFee, Guid>>();
		var before = (await dues.GetListAsync(row => row.StudentId == studentId)).Count;

		var context = await service.GetOptionalServiceRestartContextAsync(studentId, request.AcademicYearId, request.FeeHeadId);
		context.EarliestStartDate.ShouldBe(new DateOnly(2025, 10, 1));
		context.LatestEndDate.ShouldBe(new DateOnly(2026, 3, 31));
		context.ProrationPolicies.Select(option => option.VersionId).ShouldContain(request.ProrationPolicyVersionId);
		context.PaymentTerms.Select(option => option.VersionId).ShouldContain(request.PaymentTermVersionId);
		context.PrepaidCredit.ShouldBe(125m);
		var quote = await service.PreviewOptionalServiceRestartAsync(studentId, request);

		quote.PreviewToken.ShouldNotBeNullOrWhiteSpace();
		quote.ScheduledTotal.ShouldBe(2400m);
		quote.AssignedTotal.ShouldBe(1181m);
		quote.ProrationReduction.ShouldBe(1219m);
		quote.Lines.Select(line => (line.CoverageStartDate, line.CoverageEndDate, line.ProratedAmount)).ShouldBe([
			((DateTime?)new DateTime(2025, 11, 16), (DateTime?)new DateTime(2025, 11, 30), 600m),
			((DateTime?)new DateTime(2025, 12, 1), (DateTime?)new DateTime(2025, 12, 15), 581m),
		]);
		quote.Lines.ShouldAllBe(line => line.PaymentTermVersionId == request.PaymentTermVersionId && line.DueFeeId == null);
		(await dues.GetListAsync(row => row.StudentId == studentId)).Count.ShouldBe(before);
	}

	[Fact]
	public async Task Optional_Service_Restart_Appends_Collectible_Dues_And_Preserves_Cancellation_Credit()
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var (studentId, request, _, prepaidDueId) = await StoppedOptionalServiceAsync();
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		var dues = GetRequiredService<IRepository<DueFee, Guid>>();
		var original = await dues.GetListAsync(row => row.StudentId == studentId && row.FeeHeadId == request.FeeHeadId);
		var originalIds = original.Select(row => row.Id).ToArray();
		var cancelled = original.Single(row => row.Id == prepaidDueId);
		var cancellationId = cancelled.GetOptionalServiceCancellationId();
		cancellationId.ShouldNotBeNull();
		var quote = await service.PreviewOptionalServiceRestartAsync(studentId, request);
		quote.PrepaidCredit.ShouldBe(125m);
		request = request with { PreviewToken = quote.PreviewToken };

		var committed = await service.RestartOptionalServiceAsync(studentId, request);
		var replay = await service.RestartOptionalServiceAsync(studentId, request);
		replay.AssignedTotal.ShouldBe(committed.AssignedTotal);
		replay.Lines.Select(line => line.DueFeeId).ShouldBe(committed.Lines.Select(line => line.DueFeeId));
		committed.AssignedTotal.ShouldBe(quote.AssignedTotal);
		committed.PrepaidCredit.ShouldBe(125m);
		var audit = await GetRequiredService<IRepository<FinanceAuditEvent, Guid>>().GetAsync(request.RequestId);
		audit.StudentId.ShouldBe(studentId);
		audit.AcademicYearId.ShouldBe(request.AcademicYearId);

		var after = await dues.GetListAsync(row => row.StudentId == studentId && row.FeeHeadId == request.FeeHeadId);
		after.Count.ShouldBe(original.Count + 2);
		after.Select(row => row.Id).ToHashSet().IsSupersetOf(originalIds).ShouldBeTrue();
		cancelled = after.Single(row => row.Id == prepaidDueId);
		cancelled.GetOptionalServiceCancellationId().ShouldBe(cancellationId);
		cancelled.OriginalAmount.ShouldBe(1200m);
		cancelled.PaidAmount.ShouldBe(125m);
		cancelled.AdjustableBalance.ShouldBe(125m);
		cancelled.OutstandingAmount.ShouldBe(0m);
		var newRows = after.Where(row => !originalIds.Contains(row.Id)).OrderBy(row => row.CoverageStartDate).ToArray();
		newRows.Select(row => row.OutstandingAmount).ShouldBe([600m, 581m]);
		newRows.ShouldAllBe(row => row.ProrationPolicyVersionId == request.ProrationPolicyVersionId &&
			row.PaymentTermVersionId == request.PaymentTermVersionId && row.PaidAmount == 0m &&
			row.GetOptionalServiceCancellationId() == null);
		var saved = (await service.GetByStudentIdAsync(studentId, request.AcademicYearId)).Assignment!;
		var period = saved.Heads.Single(head => head.FeeHeadId == request.FeeHeadId).OptionalServicePeriods!
			.Single(item => item.StartDate == request.StartDate);
		period.EndDate.ShouldBe(request.EndDate);
		period.CancellationEffectiveDate.ShouldBeNull();
		period.MonthlyRate.ShouldBe(request.MonthlyRate);
		period.AssignedAmount.ShouldBe(1181m);
		saved.Lines.Where(line => line.DueFeeId.HasValue && newRows.Any(row => row.Id == line.DueFeeId.Value))
			.Sum(line => line.ProratedAmount).ShouldBe(1181m);

		var collection = GetRequiredService<IFeeCollectionAppService>();
		var form = await collection.GetCollectFormAsync(studentId, request.AcademicYearId);
		form.Summary.OptionalServiceCredit.ShouldBe(125m);
		var ledger = form.Ledger.SelectMany(month => month.Items).ToArray();
		ledger.Single(line => line.DueFeeId == prepaidDueId).Status.ShouldBe(FeeCollectionStatus.Cancelled);
		foreach (var row in newRows)
			ledger.Single(line => line.DueFeeId == row.Id).Due.ShouldBe((double)row.OutstandingAmount);
	}

	[Fact]
	public async Task Optional_Service_Restart_Rejects_An_Overlapping_Or_Out_Of_Year_Period()
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var (studentId, request, _, _) = await StoppedOptionalServiceAsync();
		var service = GetRequiredService<IStudentFeeStructureAppService>();

		var overlap = request with { StartDate = new DateOnly(2025, 9, 30) };
		(await Should.ThrowAsync<BusinessException>(() => service.PreviewOptionalServiceRestartAsync(studentId, overlap)))
			.Code.ShouldBe(AssignmentErrorCodes.OptionalServiceRestartDates);
		var afterYearEnd = request with { EndDate = new DateOnly(2026, 4, 1) };
		(await Should.ThrowAsync<BusinessException>(() => service.PreviewOptionalServiceRestartAsync(studentId, afterYearEnd)))
			.Code.ShouldBe(AssignmentErrorCodes.OptionalServiceRestartDates);
	}

	[Fact]
	public async Task Optional_Service_Restart_Keeps_Existing_Billing_And_Only_Adds_Uncovered_Months()
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var (studentId, request, _, decemberDueId) = await StoppedOptionalServiceAsync(coverDecemberWithConcession: true);
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		var december = await GetRequiredService<IRepository<DueFee, Guid>>().GetAsync(decemberDueId);
		december.OriginalAmount.ShouldBe(1200m);
		december.NetPayableAmount.ShouldBe(0m);
		december.GetOptionalServiceCancellationId().ShouldBeNull();

		var context = await service.GetOptionalServiceRestartContextAsync(studentId, request.AcademicYearId, request.FeeHeadId);
		context.EarliestStartDate.ShouldBe(new DateOnly(2025, 10, 1));
		var dues = GetRequiredService<IRepository<DueFee, Guid>>();
		var original = await dues.GetListAsync(row => row.StudentId == studentId && row.FeeHeadId == request.FeeHeadId);
		var quote = await service.PreviewOptionalServiceRestartAsync(studentId, request);
		quote.AssignedTotal.ShouldBe(600m);
		quote.Lines.Single().CoverageStartDate.ShouldBe(new DateTime(2025, 11, 16));
		quote.Lines.Single().AllocationPercentage.ShouldBe(100m);
		quote.ExistingInstallments.Single().DueFeeId.ShouldBe(decemberDueId);
		var committed = await service.RestartOptionalServiceAsync(studentId, request with { PreviewToken = quote.PreviewToken });
		committed.AssignedTotal.ShouldBe(600m);

		var after = await dues.GetListAsync(row => row.StudentId == studentId && row.FeeHeadId == request.FeeHeadId);
		after.Count.ShouldBe(original.Count + 1);
		after.Single(row => row.Id == decemberDueId).ConcessionAmount.ShouldBe(1200m);
		after.Count(row => row.CoverageStartDate == new DateTime(2025, 12, 1)).ShouldBe(1);
	}

	[Fact]
	public async Task Optional_Service_Restart_Saves_Covered_Dates_Without_Adding_A_Charge()
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var (studentId, request, _, _) = await StoppedOptionalServiceAsync(legacyCancellation: true);
		request = request with { StartDate = new(2026, 1, 1), EndDate = new(2026, 1, 31) };
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		var dues = GetRequiredService<IRepository<DueFee, Guid>>();
		var before = await dues.GetListAsync(row => row.StudentId == studentId && row.FeeHeadId == request.FeeHeadId);
		var january = before.Single(row => row.CoverageStartDate == new DateTime(2026, 1, 1));

		var context = await service.GetOptionalServiceRestartContextAsync(studentId, request.AcademicYearId, request.FeeHeadId);
		context.EarliestStartDate.ShouldBe(new DateOnly(2025, 12, 31));
		context.LatestEndDate.ShouldBe(new DateOnly(2026, 3, 31));
		var preview = await service.PreviewOptionalServiceRestartAsync(studentId, request);
		preview.Lines.ShouldBeEmpty();
		preview.AssignedTotal.ShouldBe(0m);
		var installment = preview.ExistingInstallments.Single();
		installment.DueFeeId.ShouldBe(january.Id);
		installment.BilledAmount.ShouldBe(1200m);
		installment.OutstandingAmount.ShouldBe(1075m);
		request = request with { PreviewToken = preview.PreviewToken };
		await service.RestartOptionalServiceAsync(studentId, request);
		(await service.RestartOptionalServiceAsync(studentId, request)).ExistingInstallments.Single().ShouldBe(installment);

		var saved = (await service.GetByStudentIdAsync(studentId, request.AcademicYearId)).Assignment!;
		var period = saved.Heads.Single(head => head.FeeHeadId == request.FeeHeadId).OptionalServicePeriods!.Single();
		period.StartDate.ShouldBe(request.StartDate);
		period.EndDate.ShouldBe(request.EndDate);
		period.CancellationEffectiveDate.ShouldBeNull();
		var form = await GetRequiredService<IFeeCollectionAppService>().GetCollectFormAsync(studentId, request.AcademicYearId);
		var ledger = form.Ledger.SelectMany(month => month.Items).Single(line => line.DueFeeId == january.Id);
		ledger.Due.ShouldBe(1075d);
		(await dues.GetListAsync(row => row.StudentId == studentId && row.FeeHeadId == request.FeeHeadId)).Count.ShouldBe(before.Count);
	}

	[Fact]
	public async Task Optional_Service_Restart_Rejects_A_Required_Head()
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var (studentId, request, requiredHeadId, _) = await StoppedOptionalServiceAsync();
		var service = GetRequiredService<IStudentFeeStructureAppService>();

		(await Should.ThrowAsync<BusinessException>(() => service.GetOptionalServiceRestartContextAsync(
			studentId, request.AcademicYearId, requiredHeadId))).Code.ShouldBe(AssignmentErrorCodes.OptionalServiceRestartUnavailable);
	}

	[Fact]
	public async Task Optional_Service_Restart_Rejects_A_Student_Outside_The_Tenant()
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var (studentId, request, _, _) = await StoppedOptionalServiceAsync();
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		var preview = await service.PreviewOptionalServiceRestartAsync(studentId, request);
		using var otherTenant = _currentTenant.Change(Guid.NewGuid());

		(await Should.ThrowAsync<BusinessException>(() => service.RestartOptionalServiceAsync(
			studentId, request with { PreviewToken = preview.PreviewToken }))).Code.ShouldBe(AssignmentErrorCodes.EnrollmentRequired);
	}

	[Fact]
	public async Task Optional_Service_Restart_Rejects_A_Request_Changed_After_Preview()
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var (studentId, request, _, _) = await StoppedOptionalServiceAsync();
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		var dues = GetRequiredService<IRepository<DueFee, Guid>>();
		var before = await dues.GetListAsync(row => row.StudentId == studentId);
		var quote = await service.PreviewOptionalServiceRestartAsync(studentId, request);
		var changed = request with { MonthlyRate = 1400m, PreviewToken = quote.PreviewToken };

		(await Should.ThrowAsync<BusinessException>(() => service.RestartOptionalServiceAsync(studentId, changed)))
			.Code.ShouldBe(AssignmentErrorCodes.StalePreview);
		(await dues.GetListAsync(row => row.StudentId == studentId)).Select(row => row.Id).OrderBy(id => id)
			.ShouldBe(before.Select(row => row.Id).OrderBy(id => id));
		var audit = GetRequiredService<IRepository<FinanceAuditEvent, Guid>>();
		(await audit.FindAsync(request.RequestId)).ShouldBeNull();
	}

	[Fact]
	public async Task Optional_Service_Restart_Requires_A_New_Preview_After_Prepaid_Credit_Changes()
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var (studentId, request, _, prepaidDueId) = await StoppedOptionalServiceAsync();
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		var dues = GetRequiredService<IRepository<DueFee, Guid>>();
		var quote = await service.PreviewOptionalServiceRestartAsync(studentId, request);
		quote.PrepaidCredit.ShouldBe(125m);
		var cancelled = await dues.GetAsync(prepaidDueId);
		cancelled.ApplyRefund(25m);
		await dues.UpdateAsync(cancelled, autoSave: true);

		(await Should.ThrowAsync<BusinessException>(() => service.RestartOptionalServiceAsync(
			studentId, request with { PreviewToken = quote.PreviewToken }))).Code.ShouldBe(AssignmentErrorCodes.StalePreview);
		var refreshed = await service.PreviewOptionalServiceRestartAsync(studentId, request);
		refreshed.PrepaidCredit.ShouldBe(100m);
		var committed = await service.RestartOptionalServiceAsync(studentId, request with { PreviewToken = refreshed.PreviewToken });
		committed.PrepaidCredit.ShouldBe(100m);
		committed.AssignedTotal.ShouldBe(quote.AssignedTotal);
	}

	[Fact]
	public async Task Optional_Service_Restart_Can_Be_Cancelled_Before_Its_Midmonth_Start()
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var (studentId, request, _, prepaidDueId) = await StoppedOptionalServiceAsync();
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		var preview = await service.PreviewOptionalServiceRestartAsync(studentId, request);
		var restarted = await service.RestartOptionalServiceAsync(studentId, request with { PreviewToken = preview.PreviewToken });
		var stop = new OptionalServiceStopInput(request.AcademicYearId, request.FeeHeadId, request.StartDate.AddDays(-1),
			"Transport restart deferred", Guid.NewGuid());

		var cancellation = await service.PreviewOptionalServiceStopAsync(studentId, stop);

		cancellation.InstallmentCount.ShouldBe(2);
		cancellation.CancelledAmount.ShouldBe(1181m);
		await service.StopOptionalServiceAsync(studentId, stop with { PreviewToken = cancellation.PreviewToken });
		var dues = GetRequiredService<IRepository<DueFee, Guid>>();
		foreach (var line in restarted.Lines)
			(await dues.GetAsync(line.DueFeeId!.Value)).GetOptionalServiceCancellationId().ShouldBe(stop.RequestId);
		(await dues.GetAsync(prepaidDueId)).GetOptionalServiceCancellationId().ShouldNotBe(stop.RequestId);
		var context = await service.GetOptionalServiceRestartContextAsync(studentId, request.AcademicYearId, request.FeeHeadId);
		context.EarliestStartDate.ShouldBe(request.StartDate);
		var next = request with { RequestId = Guid.NewGuid() };
		var nextPreview = await service.PreviewOptionalServiceRestartAsync(studentId, next);
		nextPreview.AssignedTotal.ShouldBe(restarted.AssignedTotal);
	}

	[Fact]
	public async Task Optional_Service_Restart_Uses_Its_Saved_Policy_For_Withdrawal_Credit()
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var (studentId, request, _, cancelledDecemberId) = await StoppedOptionalServiceAsync();
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		var preview = await service.PreviewOptionalServiceRestartAsync(studentId, request);
		var restarted = await service.RestartOptionalServiceAsync(studentId, request with { PreviewToken = preview.PreviewToken });
		var restartedNovember = restarted.Lines.Single(line => line.CoverageStartDate?.Month == 11);
		var restartedDecember = restarted.Lines.Single(line => line.CoverageStartDate?.Month == 12);
		var dues = GetRequiredService<IRepository<DueFee, Guid>>();
		var cancellationId = (await dues.GetAsync(cancelledDecemberId)).GetOptionalServiceCancellationId();
		cancellationId.ShouldNotBeNull();
		var withdrawalService = GetRequiredService<WithdrawalProrationService>();
		var leavingDate = new DateTime(2025, 11, 30);

		var withdrawal = await WithUnitOfWorkAsync(() => withdrawalService.PreviewAsync(studentId, request.AcademicYearId, leavingDate));

		var credit = withdrawal.Lines.Single();
		credit.DueFeeId.ShouldBe(restartedDecember.DueFeeId!.Value);
		credit.Credit.ShouldBe(581m);
		withdrawal.Result!.WithdrawalCredit.ShouldBe(581m);
		var settledDecember = await dues.GetAsync(restartedDecember.DueFeeId.Value);
		settledDecember.OutstandingAmount.ShouldBe(581m);
		settledDecember.ApplyPayment(581m);
		await dues.UpdateAsync(settledDecember, autoSave: true);

		var paidWithdrawal = await WithUnitOfWorkAsync(() => withdrawalService.PreviewAsync(studentId, request.AcademicYearId, leavingDate));

		var unpaidCredit = paidWithdrawal.Lines.Single();
		unpaidCredit.DueFeeId.ShouldBe(restartedNovember.DueFeeId!.Value);
		unpaidCredit.Credit.ShouldBe(581m);
		var unpaidNovember = await dues.GetAsync(restartedNovember.DueFeeId.Value);
		(unpaidNovember.OutstandingAmount - unpaidCredit.Credit).ShouldBe(19m);
		var cancelled = await dues.GetAsync(cancelledDecemberId);
		cancelled.GetOptionalServiceCancellationId().ShouldBe(cancellationId);
		cancelled.AdjustableBalance.ShouldBe(125m);
	}

	private async Task<(Guid StudentId, OptionalServiceRestartInput Request, Guid RequiredHeadId, Guid DecemberDueId)> StoppedOptionalServiceAsync(
		bool coverDecemberWithConcession = false, bool legacyCancellation = false)
	{
		var clock = (FeeStructurePolicyTestModule.FixtureClock)GetRequiredService<IClock>();
		clock.Current = new(2025, 9, 30);
		var (input, headId) = await VariableAssignmentAsync(variable: false, optional: true, addAdditionalHead: true,
			monthlyTerm: true, configuredMonthlyRate: 1200m);
		input.SelectedOptionalFeeHeadIds = [headId];
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		input.PreviewToken = (await service.PreviewAsync(input)).PreviewToken;
		var assignment = await service.AssignAsync(input);
		var dues = GetRequiredService<IRepository<DueFee, Guid>>();
		var december = await dues.GetAsync(row => row.StudentId == input.StudentId && row.FeeHeadId == headId &&
			row.CoverageStartDate == new DateTime(2025, 12, 1));
		if (coverDecemberWithConcession) december.ApplyConcession(december.OriginalAmount);
		else december.ApplyPayment(125m);
		await dues.UpdateAsync(december, autoSave: true);
		if (legacyCancellation)
		{
			var assignments = GetRequiredService<IRepository<StudentFeeStructure, Guid>>();
			var saved = await assignments.GetAsync(item => item.StudentId == input.StudentId && item.AcademicYearId == input.AcademicYearId);
			saved.RecordOptionalServiceEndDate(headId, new(2025, 12, 31));
			await assignments.UpdateAsync(saved, autoSave: true);
			var january = await dues.GetAsync(row => row.StudentId == input.StudentId && row.FeeHeadId == headId &&
				row.CoverageStartDate == new DateTime(2026, 1, 1));
			january.ApplyPayment(125m);
			await dues.UpdateAsync(january, autoSave: true);
		}
		else
		{
			var stop = new OptionalServiceStopInput(input.AcademicYearId, headId, new DateOnly(2025, 9, 30),
				"Transport paused", Guid.NewGuid());
			var stopQuote = await service.PreviewOptionalServiceStopAsync(input.StudentId, stop);
			await service.StopOptionalServiceAsync(input.StudentId, stop with { PreviewToken = stopQuote.PreviewToken });
		}

		var policies = GetRequiredService<IProrationPolicyAppService>();
		var policy = await policies.CreateAsync(new()
		{
			AcademicYearId = input.AcademicYearId, PolicyName = "Transport service dates", Status = "Active", IsDefault = true,
			FeeHeadRules = [new() { FeeHeadId = headId, RuleType = "Daily", ApplyOnAdmission = true,
				ApplyOnEarlyWithdrawal = true, EarlyWithdrawalRuleType = "Daily" }],
		});
		var versionId = (await policies.GetAsync(policy.Id)).CurrentVersionId!.Value;
		var termVersionId = assignment.Assignment.Lines.First(line => line.FeeHeadId == headId).PaymentTermVersionId!.Value;
		var request = new OptionalServiceRestartInput(input.AcademicYearId, headId, new(2025, 11, 16), new(2025, 12, 15),
			1200m, versionId, termVersionId, Guid.NewGuid());
		return (input.StudentId, request, assignment.Assignment.Heads.Single(head => !head.IsOptional).FeeHeadId, december.Id);
	}
}
