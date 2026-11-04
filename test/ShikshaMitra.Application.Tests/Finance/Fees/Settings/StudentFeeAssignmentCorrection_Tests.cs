using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureAppService_CollectionPolicy_Tests
{
	private async Task<(AssignStudentFeeStructureDto Input, AssignmentCommitResultDto Original)> ReadyCorrectionAsync()
	{
		var input = await ReadyAssignmentAsync(amount: 1000m);
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		input.PreviewToken = (await service.PreviewAsync(input)).PreviewToken;
		var original = await service.AssignAsync(input);
		var saved = await service.GetByStudentIdAsync(input.StudentId, input.AcademicYearId);
		// JSON makes the test express the additive public contract before implementation.
		var command = JsonNode.Parse(JsonSerializer.Serialize(input))!;
		command["EditAssignmentId"] = original.AssignmentId;
		command["AssignmentConcurrencyStamp"] = saved.ConcurrencyStamp;
		command["EditReason"] = "Correct the student's selected fees";
		command["PreviewToken"] = null;
		return (JsonSerializer.Deserialize<AssignStudentFeeStructureDto>(command.ToJsonString())!, original);
	}

	[Fact]
	public async Task Correction_Calculation_Is_Transient_And_Ordinary_Generation_Remains_Duplicate_Protected()
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var (input, original) = await ReadyCorrectionAsync();
		using var calculation = GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: false);
		var structures = GetRequiredService<IRepository<ShikshaMitra.Domain.Finance.Fees.Settings.FeeStructure, Guid>>();
		var structure = await (await structures.GetQueryableAsync()).Include(s => s.FeeStructureDetails)
			.SingleAsync(s => s.Id == input.StandardFeeStructureId);
		var year = await _academicYearRepo.GetAsync(input.AcademicYearId);
		var manager = GetRequiredService<DueFeeManager>();
		var calculated = await manager.CalculateForStudentAsync(input.StudentId, structure, year, input.SelectedOptionalFeeHeadIds);
		calculated.Sum(d => d.OriginalAmount).ShouldBe(original.Assignment.ProratedTotal);
		calculated.Select(d => (Guid?)d.Id).Intersect(original.Assignment.Lines.Select(l => l.DueFeeId)).ShouldBeEmpty();
		var persisted = await GetRequiredService<IRepository<DueFee, Guid>>().GetListAsync(d => d.StudentFeeStructureId == original.AssignmentId);
		persisted.Select(d => (Guid?)d.Id).Order().ShouldBe(original.Assignment.Lines.Select(l => l.DueFeeId).Order());
		(await Should.ThrowAsync<BusinessException>(() => manager.GenerateForStudentAsync(input.StudentId, structure, year,
			input.SelectedOptionalFeeHeadIds))).Code.ShouldBe(DueFeeErrorCodes.DueFeesAlreadyGenerated);
	}

	[Fact]
	public async Task Unpaid_Correction_Preserves_Evidence_And_Retry_Adds_No_Dues()
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var (input, original) = await ReadyCorrectionAsync();
		input.EditReason = new string('x', 501);
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		var master = await GetRequiredService<IRepository<ShikshaMitra.Domain.Finance.Fees.Settings.FeeStructure, Guid>>().GetAsync(input.StandardFeeStructureId);
		var revision = await _feeStructures.CreateRevisionAsync(master.Id, new() { ConcurrencyStamp = master.ConcurrencyStamp });
		await _feeStructures.PublishAsync(revision.Id, new() { ConcurrencyStamp = revision.ConcurrencyStamp });
		input.StandardFeeStructureId = revision.Id;
		var context = (await service.GetContextsAsync(new() { AcademicYearId = input.AcademicYearId, StudentIds = [input.StudentId] })).Single();
		JsonSerializer.SerializeToElement(context).GetProperty("CanEditStructure").GetBoolean().ShouldBeTrue();
		var preview = await service.PreviewAsync(input);
		input.PreviewToken = preview.PreviewToken;
		var corrected = await service.AssignAsync(input);
		corrected.AssignmentId.ShouldBe(original.AssignmentId);
		corrected.Outcome.ShouldBe("Assigned");
		corrected.Assignment.StructureId.ShouldBe(revision.Id);
		(await GetRequiredService<IRepository<ShikshaMitra.Students.Student, Guid>>().GetAsync(input.StudentId)).FeeStructureId.ShouldBe(revision.Id);
		var dues = GetRequiredService<IRepository<DueFee, Guid>>();
		var active = await dues.GetListAsync(d => d.StudentFeeStructureId == corrected.AssignmentId);
		active.Sum(d => d.OriginalAmount).ShouldBe(preview.ProratedTotal);
		active.Select(d => d.Id).Intersect(original.Assignment.Lines.Select(l => l.DueFeeId!.Value)).ShouldBeEmpty();
		var saved = await GetRequiredService<IRepository<StudentFeeStructure, Guid>>().GetAsync(corrected.AssignmentId);
		saved.ExtraProperties["AssignmentCorrectionHistory"].ShouldNotBeNull().ToString().ShouldNotBeNull().ShouldContain(input.EditReason!);
		saved.ExtraProperties["AssignmentCorrectionHistory"].ShouldNotBeNull().ToString().ShouldNotBeNull().ShouldContain(original.Assignment.Lines[0].DueFeeId!.Value.ToString());
		(await service.AssignAsync(input)).Outcome.ShouldBe("AlreadyAssigned");
		(await dues.GetListAsync(d => d.StudentFeeStructureId == corrected.AssignmentId)).Count.ShouldBe(active.Count);
		active[0].ApplyPayment(25m);
		await dues.UpdateAsync(active[0], true);
		var paidReplay = await service.AssignAsync(input);
		paidReplay.Outcome.ShouldBe("AlreadyAssigned");
		paidReplay.Assignment.Lines.Sum(l => l.PaidAmount).ShouldBe(25m);
		using (GetRequiredService<IDataFilter>().Disable<ISoftDelete>())
			(await dues.GetListAsync(d => d.StudentFeeStructureId == corrected.AssignmentId)).Count.ShouldBe(active.Count + original.Assignment.Lines.Count);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Correction_Bulk_Requires_The_Same_Original_Structure_And_Revision(bool mixed)
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var (first, original) = await ReadyCorrectionAsync();
		var students = GetRequiredService<IRepository<ShikshaMitra.Students.Student, Guid>>();
		var source = await students.GetAsync(first.StudentId);
		var secondStudent = new ShikshaMitra.Students.Student(Guid.NewGuid(), TestTenantId, "Second", null, "Correction", first.AcademicYearId,
			source.AcademicBoardId!.Value, source.AcademicMediumId, source.AcademicMediumStageId!.Value, source.StandardId,
			status: ShikshaMitra.Students.StudentLifeCycleStatusEnum.Enrolled);
		await students.InsertAsync(secondStudent, true);
		var second = new AssignStudentFeeStructureDto { StudentId = secondStudent.Id, AcademicYearId = first.AcademicYearId, StandardFeeStructureId = first.StandardFeeStructureId };
		if (mixed)
		{
			var master = await GetRequiredService<IRepository<ShikshaMitra.Domain.Finance.Fees.Settings.FeeStructure, Guid>>().GetAsync(first.StandardFeeStructureId);
			var revision = await _feeStructures.CreateRevisionAsync(master.Id, new() { ConcurrencyStamp = master.ConcurrencyStamp });
			await _feeStructures.PublishAsync(revision.Id, new() { ConcurrencyStamp = revision.ConcurrencyStamp });
			second.StandardFeeStructureId = revision.Id;
		}
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		second.PreviewToken = (await service.PreviewAsync(second)).PreviewToken;
		var assigned = await service.AssignAsync(second);
		var read = await service.GetByStudentIdAsync(second.StudentId, second.AcademicYearId);
		var command = JsonNode.Parse(JsonSerializer.Serialize(second))!;
		command["EditAssignmentId"] = assigned.AssignmentId;
		command["AssignmentConcurrencyStamp"] = read.ConcurrencyStamp;
		command["EditReason"] = "Correct batch";
		second = JsonSerializer.Deserialize<AssignStudentFeeStructureDto>(command.ToJsonString())!;
		var batch = new BulkAssignStudentFeeStructureDto { Items = [first, second] };
		if (mixed)
		{
			(await Should.ThrowAsync<BusinessException>(() => service.PreviewBulkAsync(batch))).Code.ShouldBe("Finance:Assignment:MixedOriginalStructures");
			return;
		}
		var commonMaster = await GetRequiredService<IRepository<ShikshaMitra.Domain.Finance.Fees.Settings.FeeStructure, Guid>>().GetAsync(first.StandardFeeStructureId);
		var replacement = await _feeStructures.CreateRevisionAsync(commonMaster.Id, new() { ConcurrencyStamp = commonMaster.ConcurrencyStamp });
		await _feeStructures.PublishAsync(replacement.Id, new() { ConcurrencyStamp = replacement.ConcurrencyStamp });
		first.StandardFeeStructureId = replacement.Id;
		second.StandardFeeStructureId = replacement.Id;
		var previews = await service.PreviewBulkAsync(batch);
		foreach (var item in batch.Items) item.PreviewToken = previews.Results.Single(p => p.StudentId == item.StudentId).Preview.ShouldNotBeNull().PreviewToken;
		(await service.BulkAssignAsync(batch)).SuccessCount.ShouldBe(2);
		(await service.BulkAssignAsync(batch)).AlreadyAssignedCount.ShouldBe(2);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Correction_Blocks_Any_Payment_Even_After_Reversal(bool reversed)
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var (input, original) = await ReadyCorrectionAsync();
		using (var change = GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
		{
			var dues = GetRequiredService<IRepository<DueFee, Guid>>();
			var due = await dues.GetAsync(original.Assignment.Lines[0].DueFeeId!.Value);
			due.ApplyPayment(50m);
			if (reversed) due.ReversePayment(50m);
			await dues.UpdateAsync(due, true);
			await change.CompleteAsync();
		}
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		(await Should.ThrowAsync<BusinessException>(() => service.PreviewAsync(input))).Code.ShouldBe("Finance:Assignment:StructureEditBlocked");
		var read = JsonSerializer.SerializeToElement(await service.GetByStudentIdAsync(input.StudentId, input.AcademicYearId));
		read.GetProperty("CanEditStructure").GetBoolean().ShouldBeFalse();
		read.GetProperty("StructureEditBlocker").GetString().ShouldNotBeNull().ShouldContain("payment", Case.Insensitive);
	}

	[Fact]
	public async Task Correction_Changed_Reason_Or_Assignment_Stamp_Rejects_Preview_Without_Replacing_Dues()
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var (input, original) = await ReadyCorrectionAsync();
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		input.PreviewToken = (await service.PreviewAsync(input)).PreviewToken;
		var command = JsonNode.Parse(JsonSerializer.Serialize(input))!;
		command["EditReason"] = "A different reason";
		var changed = JsonSerializer.Deserialize<AssignStudentFeeStructureDto>(command.ToJsonString())!;
		(await Should.ThrowAsync<BusinessException>(() => service.AssignAsync(changed))).Code.ShouldBe(AssignmentErrorCodes.StalePreview);
		command["AssignmentConcurrencyStamp"] = "stale";
		changed = JsonSerializer.Deserialize<AssignStudentFeeStructureDto>(command.ToJsonString())!;
		(await Should.ThrowAsync<BusinessException>(() => service.PreviewAsync(changed))).Code.ShouldBe(AssignmentErrorCodes.StalePreview);
		var dues = await GetRequiredService<IRepository<DueFee, Guid>>().GetListAsync(d => d.StudentFeeStructureId == original.AssignmentId);
		dues.Select(d => d.Id).Order().ShouldBe(original.Assignment.Lines.Select(l => l.DueFeeId!.Value).Order());
	}

	[Fact]
	public async Task Correction_Does_Not_Erase_Concession_Or_Withdrawal_Use()
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var (input, original) = await ReadyCorrectionAsync();
		using (var change = GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
		{
			var dues = GetRequiredService<IRepository<DueFee, Guid>>();
			var due = await dues.GetAsync(original.Assignment.Lines[0].DueFeeId!.Value);
			due.ApplyConcession(10m);
			await dues.UpdateAsync(due, true);
			await change.CompleteAsync();
		}
		(await Should.ThrowAsync<BusinessException>(() => GetRequiredService<IStudentFeeStructureAppService>().PreviewAsync(input)))
			.Code.ShouldBe("Finance:Assignment:StructureEditBlocked");
		(await GetRequiredService<IRepository<DueFee, Guid>>().GetAsync(original.Assignment.Lines[0].DueFeeId!.Value)).ConcessionAmount.ShouldBe(10m);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Correction_Blocks_Persisted_Bulk_Waiver_Applications_Including_Deleted_History(bool deleted)
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var (input, original) = await ReadyCorrectionAsync();
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		input.PreviewToken = (await service.PreviewAsync(input)).PreviewToken;
		var bulkRequestId = Guid.NewGuid();
		var waiverId = Guid.NewGuid();
		var waivers = GetRequiredService<IRepository<LateFeeWaiverApplication, Guid>>();
		using (var change = GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
		{
			await waivers.InsertAsync(new LateFeeWaiverApplication(waiverId, TestTenantId, input.StudentId,
				original.Assignment.Lines[0].DueFeeId!.Value, original.Assignment.Policies.Single(p => p.Family == "collection").PolicyId,
				input.AcademicYearId, 25m, 25m, 25m, "Persisted bulk waiver evidence", false, TestUserId, bulkRequestId), true);
			if (deleted) await waivers.DeleteAsync(waiverId, true);
			await change.CompleteAsync();
		}
		(await service.GetContextsAsync(new() { AcademicYearId = input.AcademicYearId, StudentIds = [input.StudentId] }))
			.Single().CanEditStructure.ShouldBeFalse();
		var read = await service.GetByStudentIdAsync(input.StudentId, input.AcademicYearId);
		read.CanEditStructure.ShouldBeFalse();
		read.Assignment!.Lines.Select(l => l.DueFeeId).ShouldBe(original.Assignment.Lines.Select(l => l.DueFeeId));
		(await Should.ThrowAsync<BusinessException>(() => service.PreviewAsync(input))).Code.ShouldBe(AssignmentErrorCodes.StructureEditBlocked);
		(await Should.ThrowAsync<BusinessException>(() => service.AssignAsync(input))).Code.ShouldBe(AssignmentErrorCodes.StructureEditBlocked);
		using var evidence = GetRequiredService<IDataFilter>().Disable<ISoftDelete>();
		(await waivers.GetAsync(waiverId)).BulkWaiverRequestId.ShouldBe(bulkRequestId);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Correction_Blocks_Persisted_Refund_Lines_Including_Deleted_Requests(bool deleted)
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var (input, original) = await ReadyCorrectionAsync();
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		input.PreviewToken = (await service.PreviewAsync(input)).PreviewToken;
		var requestId = Guid.NewGuid();
		var lineId = Guid.NewGuid();
		var dueId = original.Assignment.Lines[0].DueFeeId!.Value;
		var refunds = GetRequiredService<IRepository<FeeRefundRequest, Guid>>();
		using (var change = GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
		{
			// Legacy refund evidence must protect a due even if its old payment marker is absent.
			await refunds.InsertAsync(new FeeRefundRequest(requestId, TestTenantId, input.StudentId, input.AcademicYearId,
				$"CORR-REF-{requestId:N}", [new FeeRefundLine(lineId, requestId, dueId, 25d)], "cash", null, "Historical refund evidence"), true);
			if (deleted) await refunds.DeleteAsync(requestId, true);
			await change.CompleteAsync();
		}
		(await service.GetContextsAsync(new() { AcademicYearId = input.AcademicYearId, StudentIds = [input.StudentId] }))
			.Single().CanEditStructure.ShouldBeFalse();
		(await Should.ThrowAsync<BusinessException>(() => service.PreviewAsync(input))).Code.ShouldBe(AssignmentErrorCodes.StructureEditBlocked);
		(await Should.ThrowAsync<BusinessException>(() => service.AssignAsync(input))).Code.ShouldBe(AssignmentErrorCodes.StructureEditBlocked);
		(await service.GetByStudentIdAsync(input.StudentId, input.AcademicYearId)).Assignment!.Lines.Select(l => l.DueFeeId)
			.ShouldBe(original.Assignment.Lines.Select(l => l.DueFeeId));
		using var read = GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: false);
		using var evidence = GetRequiredService<IDataFilter>().Disable<ISoftDelete>();
		var saved = await (await refunds.GetQueryableAsync()).SelectMany(r => r.Lines).Where(l => l.Id == lineId)
			.Select(l => new { l.DueFeeId, l.RequestedAmount }).SingleAsync();
		saved.DueFeeId.ShouldBe(dueId);
		saved.RequestedAmount.ShouldBe(25m);
	}

	[Fact]
	public async Task Correction_Invalidates_A_Collection_That_Already_Loaded_The_Old_Due()
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var (input, original) = await ReadyCorrectionAsync();
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		input.PreviewToken = (await service.PreviewAsync(input)).PreviewToken;
		using var collection = GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: false);
		var dues = GetRequiredService<IRepository<DueFee, Guid>>();
		var oldDue = await dues.GetAsync(original.Assignment.Lines[0].DueFeeId!.Value);
		await service.AssignAsync(input);
		oldDue.ApplyPayment(25m);
		await Should.ThrowAsync<AbpDbConcurrencyException>(() => dues.UpdateAsync(oldDue, true));
	}

	[Fact]
	public async Task Correction_Cannot_Supersede_A_Stale_Due_After_Collection_Commits()
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var (input, original) = await ReadyCorrectionAsync();
		var manager = GetRequiredService<IUnitOfWorkManager>();
		using var correction = manager.Begin(requiresNew: true, isTransactional: false);
		var dues = GetRequiredService<IRepository<DueFee, Guid>>();
		var staleDue = await dues.GetAsync(original.Assignment.Lines[0].DueFeeId!.Value);
		using (var collection = manager.Begin(requiresNew: true, isTransactional: false))
		{
			var paid = await dues.GetAsync(staleDue.Id);
			paid.ApplyPayment(25m);
			await dues.UpdateAsync(paid, true);
			await collection.CompleteAsync();
		}
		staleDue.SupersedeUnusedAssignment(original.AssignmentId);
		await Should.ThrowAsync<AbpDbConcurrencyException>(() => dues.UpdateAsync(staleDue, true));
	}

	[Fact]
	public async Task Correction_Reprices_Variable_Amounts_And_Removes_Unselected_Optional_Dues()
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var (input, headId) = await VariableAssignmentAsync(true, true, addAdditionalHead: true);
		input.SelectedOptionalFeeHeadIds = [headId];
		input.VariableFeeHeads = [new() { FeeHeadId = headId, Amount = 1250m }];
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		input.PreviewToken = (await service.PreviewAsync(input)).PreviewToken;
		var original = await service.AssignAsync(input);
		input.EditAssignmentId = original.AssignmentId;
		input.AssignmentConcurrencyStamp = (await service.GetByStudentIdAsync(input.StudentId, input.AcademicYearId)).ConcurrencyStamp;
		input.EditReason = "Correct variable amount";
		input.VariableFeeHeads[0].Amount = 800m;
		var preview = await service.PreviewAsync(input);
		preview.ProratedTotal.ShouldBe(1550m);
		input.PreviewToken = preview.PreviewToken;
		(await service.AssignAsync(input)).Assignment.ProratedTotal.ShouldBe(1550m);
		input.AssignmentConcurrencyStamp = (await service.GetByStudentIdAsync(input.StudentId, input.AcademicYearId)).ConcurrencyStamp;
		input.EditReason = "Remove an inapplicable optional fee";
		input.SelectedOptionalFeeHeadIds = [];
		input.VariableFeeHeads = [];
		preview = await service.PreviewAsync(input);
		preview.ProratedTotal.ShouldBe(750m);
		input.PreviewToken = preview.PreviewToken;
		await service.AssignAsync(input);
		var dues = await GetRequiredService<IRepository<DueFee, Guid>>().GetListAsync(d => d.StudentFeeStructureId == original.AssignmentId);
		dues.Sum(d => d.OriginalAmount).ShouldBe(750m);
		dues.ShouldAllBe(d => d.FeeHeadId != headId);
	}

	[Fact]
	public async Task Correction_Blocks_Historical_Reversed_Allocations_Without_A_New_Payment_Marker()
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var (input, original) = await ReadyCorrectionAsync();
		using (var change = GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
		{
			var payment = new FeeRecordMetadata(Guid.NewGuid(), TestTenantId, input.StudentId, input.AcademicYearId,
				25d, "HISTORICAL-REVERSED", ShikshaMitra.Finance.Fees.Collections.FeeCollectionStatus.Reversed);
			await GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().InsertAsync(payment, true);
			await GetRequiredService<IRepository<FeeCollectionDueFeeAllocation, Guid>>().InsertAsync(new(Guid.NewGuid(), TestTenantId,
				payment.Id, original.Assignment.Lines[0].DueFeeId!.Value, 25d), true);
			await change.CompleteAsync();
		}
		(await Should.ThrowAsync<BusinessException>(() => GetRequiredService<IStudentFeeStructureAppService>().PreviewAsync(input)))
			.Code.ShouldBe("Finance:Assignment:StructureEditBlocked");
	}

	[Fact]
	public async Task Correction_Blocks_Payment_Arriving_After_Its_Preview()
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var (input, original) = await ReadyCorrectionAsync();
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		input.PreviewToken = (await service.PreviewAsync(input)).PreviewToken;
		using (var change = GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
		{
			var dues = GetRequiredService<IRepository<DueFee, Guid>>();
			var due = await dues.GetAsync(original.Assignment.Lines[0].DueFeeId!.Value);
			due.ApplyPayment(25m);
			await dues.UpdateAsync(due, true);
			await change.CompleteAsync();
		}
		(await Should.ThrowAsync<BusinessException>(() => service.AssignAsync(input))).Code.ShouldBe("Finance:Assignment:StructureEditBlocked");
		(await GetRequiredService<IRepository<DueFee, Guid>>().GetListAsync(d => d.StudentFeeStructureId == original.AssignmentId))
			.Select(d => d.Id).Order().ShouldBe(original.Assignment.Lines.Select(l => l.DueFeeId!.Value).Order());
	}
}
