using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class LateFeeCollectionRuntime_Tests
{
	[Theory]
	[InlineData(-1, -30, 0)]
	[InlineData(0, -30, 10)]
	[InlineData(1, -30, 10)]
	[InlineData(1, 1, 0)]
	public async Task Head_Service_Start_Excludes_Earlier_Due_Dates(int dueOffset, int joiningOffset, int expected)
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		var (year, student, head, dueId, _) = await SeedStudentWithOverdueDueFeeAsync(10, true);
		var due = await _dueFeeRepo.GetAsync(dueId);
		var assignment = await _studentFeeStructureRepo.GetAsync(due.StudentFeeStructureId!.Value);
		assignment.CaptureAssignment(new string('d', 64), JsonSerializer.Serialize(new
		{
			RegistrationJoiningDate = due.DueDate.AddDays(joiningOffset),
			ChargeStartDate = due.DueDate.AddMonths(-2),
			Heads = new[] { new { FeeHeadId = head, Included = true,
				ServicePeriod = new { Source = "ManualOverride", StartDate = due.DueDate.AddDays(-dueOffset).AddHours(12) } } },
		}));
		await _studentFeeStructureRepo.UpdateAsync(assignment, autoSave: true);
		await ConfigureCommonPolicyAsync(assignment, head, null, "Flat");
		var form = await _feeCollections.GetCollectFormAsync(student, year);
		form.Charges.Where(x => x.SourceType == "LateFee").Sum(x => x.PendingAmount).ShouldBe(expected);
		(await GetSummaryRowAsync(year, student)).PendingCharges.ShouldBe(expected);
	}

	[Fact]
	public async Task One_Heads_Manual_Start_Does_Not_Exempt_Another_Heads_Eligible_Dues()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		var (year, _, first, second) = await SeedCommonLateFeeAsync("PerDay", 40);
		var assignment = await _studentFeeStructureRepo.GetAsync(first.StudentFeeStructureId!.Value);
		assignment.CaptureAssignment(new string('e', 64), JsonSerializer.Serialize(new
		{
			Heads = new[] { new { FeeHeadId = first.FeeHeadId, Included = true,
				ServicePeriod = new { Source = "ManualOverride", StartDate = first.DueDate.AddDays(1) } } },
		}));
		await _studentFeeStructureRepo.UpdateAsync(assignment, autoSave: true);
		var balances = await ReadCommonBalancesAsync([first, second], year, DateTime.UtcNow.Date);
		balances[first.Id].PendingAmount.ShouldBe(0m);
		balances[second.Id].PendingAmount.ShouldBe(100m);
	}

	[Fact]
	public async Task Preserved_Common_Charge_On_A_PreService_Row_Is_Not_Issued_Again()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		var (year, _, first, second) = await SeedCommonLateFeeAsync("Flat", 40);
		var assignment = await _studentFeeStructureRepo.GetAsync(first.StudentFeeStructureId!.Value);
		first.PreserveLateFeeAssessment(10m, new DateTimeOffset(DateTime.UtcNow), assignment.CollectionPolicyVersionId!.Value,
			"Common late fee across selected fee heads and overdue installments. Historical assessment.");
		await _dueFeeRepo.UpdateAsync(first, autoSave: true);
		assignment.CaptureAssignment(new string('f', 64), JsonSerializer.Serialize(new
		{
			Heads = new[] { new { FeeHeadId = first.FeeHeadId, Included = true,
				ServicePeriod = new { Source = "ManualOverride", StartDate = first.DueDate.AddDays(1) } } },
		}));
		await _studentFeeStructureRepo.UpdateAsync(assignment, autoSave: true);
		var balances = await ReadCommonBalancesAsync([first, second], year, DateTime.UtcNow.Date);
		balances.Values.Sum(x => x.Total).ShouldBe(10m);
		balances[first.Id].Total.ShouldBe(10m);
		balances[second.Id].Total.ShouldBe(0m);
	}

	[Theory]
	[InlineData(14, 0, false)]
	[InlineData(-12, 23, false)]
	[InlineData(14, 0, true)]
	[InlineData(-12, 23, true)]
	public async Task Historical_Recovery_Uses_The_Payments_Recorded_Calendar_Date(int offsetHours, int hour, bool laterPolicyChange)
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		var (year, _, head, dueId, _) = await SeedStudentWithOverdueDueFeeAsync(40, true);
		var due = await _dueFeeRepo.GetAsync(dueId);
		var assignment = await _studentFeeStructureRepo.GetAsync(due.StudentFeeStructureId!.Value);
		await ConfigureCommonPolicyAsync(assignment, head, null, "PerDay");
		assignment.ExtraProperties.Remove("AssignedPolicyHistory");
		await _studentFeeStructureRepo.UpdateAsync(assignment, autoSave: true);
		var before = due.OutstandingAmount;
		due.ApplyPayment(before);
		await _dueFeeRepo.UpdateAsync(due, autoSave: true);
		var recorded = new DateTimeOffset(DateTime.SpecifyKind(due.DueDate.AddDays(5).AddHours(hour), DateTimeKind.Unspecified),
			TimeSpan.FromHours(offsetHours));
		if (laterPolicyChange)
		{
			assignment.ExtraProperties["AssignedPolicyHistory"] = JsonSerializer.Serialize(new[] { new
			{
				OccurredAt = recorded.AddHours(1), Before = new { CollectionPolicyVersionId = Guid.NewGuid() },
				After = new { CollectionPolicyVersionId = assignment.CollectionPolicyVersionId },
			} });
			await _studentFeeStructureRepo.UpdateAsync(assignment, autoSave: true);
		}
		var tx = new FeeRecordMetadata(Guid.NewGuid(), TestTenantId, due.StudentId!.Value, year, (double)before,
			$"OFFSET-{Guid.NewGuid():N}", FeeCollectionStatus.Paid);
		tx.SetCommandResult(JsonSerializer.Serialize(new { CollectedAt = recorded,
			AuditSummary = new { AllocationLines = new[] { new { DueFeeId = due.Id, PendingBefore = before } } } }));
		await GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().InsertAsync(tx, autoSave: true);
		await GetRequiredService<IRepository<FeeCollectionDueFeeAllocation, Guid>>().InsertAsync(
			new(Guid.NewGuid(), TestTenantId, tx.Id, due.Id, (double)before), autoSave: true);
		var balances = await ReadCommonBalancesAsync([due], year, DateTime.UtcNow.Date);
		balances[due.Id].Total.ShouldBe(laterPolicyChange ? 0m : 50m); // Five days, unless the historical rules cannot be recovered.
	}
}
