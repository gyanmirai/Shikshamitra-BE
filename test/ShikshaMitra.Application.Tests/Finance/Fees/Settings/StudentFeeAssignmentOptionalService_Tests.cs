using System;
using System.Linq;
using System.Threading.Tasks;
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
	[Fact]
	public async Task Board_Date_Default_Uses_Verified_Bounds_And_Rejects_Different_Dates()
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var input = await ReadyAssignmentAsync();
		var board = (await _boardRepo.GetListAsync(x => x.AcademicYearId == input.AcademicYearId)).Single();
		board.SetDates(new(2025, 6, 1), new(2026, 2, 28));
		await _boardRepo.UpdateAsync(board, autoSave: true);

		var service = GetRequiredService<IStudentFeeStructureAppService>();
		var candidate = (await service.GetCandidatesAsync(input.StudentId, input.AcademicYearId))
			.Single(item => item.Id == input.StandardFeeStructureId);
		candidate.BoardStartDate.ShouldBe(board.StartDate.Date);
		candidate.BoardEndDate.ShouldBe(board.EndDate.Date);

		input.ChargePeriodSource = "BoardAcademicYear";
		var period = (await service.PreviewAsync(input)).Heads.Single().ServicePeriod!;
		period.StartDate.ShouldBe(board.StartDate.Date);
		period.EndDate.ShouldBe(board.EndDate.Date);
		period.SourceReferenceId.ShouldBe(board.Id);

		input.ChargeStartDate = new(2025, 5, 1);
		(await Should.ThrowAsync<BusinessException>(() => service.PreviewAsync(input))).Code.ShouldBe(AssignmentErrorCodes.InvalidInput);
	}

	[Fact]
	public async Task Unpaid_Correction_Locks_Fixed_Amounts_And_Required_Service_Dates()
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var (input, headId) = await VariableAssignmentAsync(false, true, addAdditionalHead: true, fixedAmount: 1200m);
		input.SelectedOptionalFeeHeadIds = [headId];
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		input.FeeHeadDetails = [new() { FeeHeadId = headId, Amount = 450.50m }];
		(await Should.ThrowAsync<BusinessException>(() => service.PreviewAsync(input))).Code.ShouldBe(AssignmentErrorCodes.InvalidInput);
		input.FeeHeadDetails.Clear();
		input.PreviewToken = (await service.PreviewAsync(input)).PreviewToken;
		var original = await service.AssignAsync(input);

		input.EditAssignmentId = original.AssignmentId;
		input.AssignmentConcurrencyStamp = (await service.GetByStudentIdAsync(input.StudentId, input.AcademicYearId)).ConcurrencyStamp;
		input.EditReason = "Correct the optional contracted fee";
		input.FeeHeadDetails = [new() { FeeHeadId = headId, Amount = 450.50m }];
		(await Should.ThrowAsync<BusinessException>(() => service.PreviewAsync(input))).Code.ShouldBe(AssignmentErrorCodes.InvalidInput);
		input.FeeHeadDetails.Clear();

		var required = original.Assignment.Heads.Single(head => !head.IsOptional);
		var requiredPeriod = required.ServicePeriod!;
		input.ServicePeriods = [new() { FeeHeadId = required.FeeHeadId, Source = "ManualOverride",
			StartDate = requiredPeriod.StartDate!.Value.AddDays(1), EndDate = requiredPeriod.EndDate }];
		(await Should.ThrowAsync<BusinessException>(() => service.PreviewAsync(input))).Code.ShouldBe(AssignmentErrorCodes.InvalidInput);

		var optionalPeriod = original.Assignment.Heads.Single(head => head.FeeHeadId == headId).ServicePeriod!;
		var newStart = optionalPeriod.StartDate!.Value.AddDays(1);
		input.ServicePeriods = [new() { FeeHeadId = headId, Source = "ManualOverride",
			StartDate = newStart, EndDate = optionalPeriod.EndDate }];
		var preview = await service.PreviewAsync(input);
		var optional = preview.Heads.Single(head => head.FeeHeadId == headId);
		optional.EffectiveAmount.ShouldBe(1200m);
		optional.ServicePeriod!.StartDate.ShouldBe(newStart);
		preview.Heads.Single(head => !head.IsOptional).ServicePeriod!.StartDate.ShouldBe(requiredPeriod.StartDate);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Optional_Service_Stop_Respects_Payments_And_Revalidates_Preview(bool paymentAfterPreview)
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var clock = (FeeStructurePolicyTestModule.FixtureClock)GetRequiredService<IClock>();
		clock.Current = new(2025, 9, 30);
		var (input, headId) = await VariableAssignmentAsync(false, true, addAdditionalHead: true, fixedAmount: 1200m);
		input.SelectedOptionalFeeHeadIds = [headId];
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		input.PreviewToken = (await service.PreviewAsync(input)).PreviewToken;
		var original = await service.AssignAsync(input);
		var dues = GetRequiredService<IRepository<DueFee, Guid>>();
		var rows = (await dues.GetListAsync(x => x.StudentFeeStructureId == original.AssignmentId && x.FeeHeadId == headId))
			.OrderBy(x => x.CoverageStartDate).ToList();
		var past = rows.First();
		var future = rows.Last();
		var pastAmount = past.OriginalAmount;
		var futureAmount = future.OriginalAmount;
		var lastServiceDate = DateOnly.FromDateTime(future.CoverageStartDate!.Value.AddDays(-1));
		past.ApplyPayment(pastAmount);
		await dues.UpdateAsync(past, autoSave: true);
		var request = new OptionalServiceStopInput(input.AcademicYearId, headId, lastServiceDate, "Service ended", Guid.NewGuid());

		var quote = await service.PreviewOptionalServiceStopAsync(input.StudentId, request);
		quote.InstallmentCount.ShouldBe(1);
		quote.CancelledAmount.ShouldBe(futureAmount);
		request = request with { PreviewToken = quote.PreviewToken };
		if (paymentAfterPreview)
		{
			future.ApplyPayment(1m);
			await dues.UpdateAsync(future, autoSave: true);
			await Should.ThrowAsync<BusinessException>(() => service.StopOptionalServiceAsync(input.StudentId, request));
			(await dues.GetAsync(future.Id)).AdjustmentCreditAmount.ShouldBe(0m);
			return;
		}

		var committed = await service.StopOptionalServiceAsync(input.StudentId, request);
		committed.CancelledAmount.ShouldBe(futureAmount);
		(await service.StopOptionalServiceAsync(input.StudentId, request)).ShouldBe(committed);
		var secondStop = await Should.ThrowAsync<BusinessException>(() =>
			service.PreviewOptionalServiceStopAsync(input.StudentId, request with { RequestId = Guid.NewGuid(), PreviewToken = null }));
		secondStop.Code.ShouldBe(AssignmentErrorCodes.OptionalServiceAlreadyStopped);
		(await dues.GetAsync(past.Id)).PaidAmount.ShouldBe(pastAmount);
		var cancelled = await dues.GetAsync(future.Id);
		cancelled.PaidAmount.ShouldBe(0m);
		cancelled.OriginalAmount.ShouldBe(futureAmount);
		cancelled.OutstandingAmount.ShouldBe(0m);
		cancelled.AdjustmentCreditAmount.ShouldBe(futureAmount);
		var saved = await service.GetByStudentIdAsync(input.StudentId, input.AcademicYearId);
		saved.Assignment!.Heads.Single(x => x.FeeHeadId == headId).OptionalServiceEndDate.ShouldBe(lastServiceDate.AddDays(1));
		saved.Assignment!.Lines.Single(x => x.DueFeeId == future.Id).OptionalServiceCancellationAmount.ShouldBe(futureAmount);
		var audit = await GetRequiredService<IRepository<FinanceAuditEvent, Guid>>().GetAsync(request.RequestId);
		audit.Reason.ShouldBe(request.Reason);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Optional_Service_Cancellation_Deducts_Paid_Or_Partial_Installments(bool alreadyRecorded)
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var clock = (FeeStructurePolicyTestModule.FixtureClock)GetRequiredService<IClock>();
		clock.Current = new(2025, 9, 30);
		var (input, headId) = await VariableAssignmentAsync(false, true, addAdditionalHead: true, fixedAmount: 1200m);
		input.SelectedOptionalFeeHeadIds = [headId];
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		input.PreviewToken = (await service.PreviewAsync(input)).PreviewToken;
		var original = await service.AssignAsync(input);
		var dues = GetRequiredService<IRepository<DueFee, Guid>>();
		var rows = (await dues.GetListAsync(x => x.StudentFeeStructureId == original.AssignmentId && x.FeeHeadId == headId))
			.OrderBy(row => row.CoverageStartDate).ToList();
		var past = rows.First();
		var unused = rows.Last();
		var prepaid = alreadyRecorded ? unused.OriginalAmount / 2 : unused.OriginalAmount;
		unused.ApplyPayment(prepaid);
		await dues.UpdateAsync(unused, autoSave: true);

		var effectiveDate = DateOnly.FromDateTime(unused.CoverageStartDate!.Value);
		var lastServiceDate = effectiveDate.AddDays(-1);
		if (alreadyRecorded)
		{
			var assignments = GetRequiredService<IRepository<StudentFeeStructure, Guid>>();
			var assignment = await assignments.GetAsync(original.AssignmentId);
			assignment.RecordOptionalServiceEndDate(headId, effectiveDate);
			await assignments.UpdateAsync(assignment, autoSave: true);
			clock.Current = effectiveDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
			var before = await service.GetByStudentIdAsync(input.StudentId, input.AcademicYearId);
			before.Assignment!.Heads.Single(x => x.FeeHeadId == headId).CanReviewOptionalServiceCancellation.ShouldBeTrue();
		}
		var collection = GetRequiredService<IFeeCollectionAppService>();
		var originalForm = await collection.GetCollectFormAsync(input.StudentId, input.AcademicYearId);
		var request = new OptionalServiceStopInput(input.AcademicYearId, headId, lastServiceDate, "Transport ended", Guid.NewGuid());
		var quote = await service.PreviewOptionalServiceStopAsync(input.StudentId, request);
		quote.InstallmentCount.ShouldBe(1);
		quote.CancelledAmount.ShouldBe(unused.OriginalAmount);
		quote.OutstandingReduction.ShouldBe(unused.OriginalAmount - prepaid);
		quote.PrepaidCredit.ShouldBe(prepaid);
		request = request with { PreviewToken = quote.PreviewToken };
		var committed = await service.StopOptionalServiceAsync(input.StudentId, request);
		committed.CancelledAmount.ShouldBe(unused.OriginalAmount);
		(await service.StopOptionalServiceAsync(input.StudentId, request)).ShouldBe(committed);

		var saved = await service.GetByStudentIdAsync(input.StudentId, input.AcademicYearId);
		saved.Assignment!.Heads.Single(x => x.FeeHeadId == headId).OptionalServiceEndDate.ShouldBe(lastServiceDate.AddDays(1));
		saved.Assignment!.Heads.Single(x => x.FeeHeadId == headId).CanReviewOptionalServiceCancellation.ShouldBeFalse();
		(await dues.GetAsync(past.Id)).AdjustmentCreditAmount.ShouldBe(0m);
		var cancelled = await dues.GetAsync(unused.Id);
		cancelled.PaidAmount.ShouldBe(prepaid);
		cancelled.OutstandingAmount.ShouldBe(0m);
		cancelled.AdjustableBalance.ShouldBe(prepaid);
		var form = await collection.GetCollectFormAsync(input.StudentId, input.AcademicYearId);
		form.Summary.TotalFee.ShouldBe(originalForm.Summary.TotalFee - (double)unused.OriginalAmount);
		form.Summary.OptionalServiceCredit.ShouldBe(prepaid);
		var line = form.Ledger.SelectMany(month => month.Items).Single(x => x.DueFeeId == unused.Id);
		line.Status.ShouldBe(FeeCollectionStatus.Cancelled);
		line.Total.ShouldBe(0d);
		line.Due.ShouldBe(0d);
	}
}
