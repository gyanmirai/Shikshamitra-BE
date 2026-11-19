using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class LateFeeCollectionRuntime_Tests
{
	[Theory]
	[InlineData(0, null, 40, 400)]
	[InlineData(3, null, 37, 370)]
	[InlineData(3, 5, 5, 50)]
	public async Task Collect_Form_Late_Fee_Period_Uses_Canonical_Grace_And_Charging_Day_Cap(
		int grace, int? cutoff, int expectedDays, int expectedAmount)
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (year, student, first, _) = await SeedCommonLateFeeAsync("PerDay", 40);
			var assignment = await _studentFeeStructureRepo.GetAsync(first.StudentFeeStructureId!.Value);
			var saved = await _collectionPolicies.GetAsync(assignment.CollectionPolicyId!.Value);
			var head = await _feeHeadRepo.GetAsync(first.FeeHeadId);
			var policy = await _collectionPolicies.UpdateAsync(saved.Id, new CreateUpdateCollectionPolicyDto
			{
				AcademicYearId = year, PolicyName = saved.PolicyName, PolicyCode = saved.PolicyCode,
				Status = "Active", IsDefault = saved.IsDefault, EnableLateFee = true, LateFeeIncomeCategoryId = head.IncomeCategoryId,
				LateFeeApplyOnFeeHeads = [], LateFeeType = "PerDay", AmountPerDay = 10m,
				GracePeriodDays = grace, MaximumChargingDays = cutoff,
			});
			policy = await _collectionPolicies.GetAsync(policy.Id);
			assignment.ChangeAssignedPolicies(null, null, ConcessionFeeHeadScope.EntireFeeStructure, [],
				policy.Id, policy.CurrentVersionId!.Value, null, null, null, null, "Period regression", FakeAdminUserId, DateTime.UtcNow);
			await _studentFeeStructureRepo.UpdateAsync(assignment, autoSave: true);

			var charge = (await _feeCollections.GetCollectFormAsync(student, year)).Charges.Single();
			charge.PendingAmount.ShouldBe(expectedAmount);
			var json = JsonSerializer.SerializeToElement(charge, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
			json.TryGetProperty("dueDate", out var dueDate).ShouldBeTrue("The collection response must carry the source obligation's due date.");
			dueDate.GetString().ShouldBe(first.DueDate.ToString("yyyy-MM-dd"));
			AssertAppliedPeriod(json, first.DueDate.AddDays(grace + 1), first.DueDate.AddDays(grace + expectedDays), expectedDays, 1);
		}
	}

	[Theory]
	[InlineData("PerDay", null, 100)]
	[InlineData("PerDay", "SuspendLateFeeAccrual", 100)]
	[InlineData("PerDay", "ExtendGracePeriod", 100)]
	[InlineData("Flat", null, 10)]
	[InlineData("Flat", "SuspendLateFeeAccrual", 10)]
	[InlineData("Flat", "ExtendGracePeriod", 10)]
	public async Task Collect_Form_Late_Fee_Period_Preserves_Disjoint_Historical_Intervals(string type, string? currentWindow, int expectedAmount)
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (year, student, first, second) = await SeedCommonLateFeeAsync(type, 40);
			var assignment = await _studentFeeStructureRepo.GetAsync(first.StudentFeeStructureId!.Value);
			assignment.ExtraProperties.Remove("AssignedPolicyHistory");
			await _studentFeeStructureRepo.UpdateAsync(assignment, autoSave: true);
			foreach (var due in new[] { first, second })
			{
				var before = due.OutstandingAmount;
				due.ApplyPayment(before);
				await _dueFeeRepo.UpdateAsync(due, autoSave: true);
				var transaction = new FeeRecordMetadata(Guid.NewGuid(), TestTenantId, student, year, (double)before,
					$"PERIOD-{Guid.NewGuid():N}", FeeCollectionStatus.Paid);
				transaction.SetCommandResult(JsonSerializer.Serialize(new
				{
					CollectedAt = due.DueDate.AddDays(5), AuditSummary = new
					{ AllocationLines = new[] { new { DueFeeId = due.Id, PendingBefore = before } } },
				}));
				await GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().InsertAsync(transaction, autoSave: true);
				await GetRequiredService<IRepository<FeeCollectionDueFeeAllocation, Guid>>().InsertAsync(
					new(Guid.NewGuid(), TestTenantId, transaction.Id, due.Id, (double)before), autoSave: true);
			}
			if (currentWindow != null)
				await GetRequiredService<IRepository<CollectionPolicyExceptionWindow, Guid>>().InsertAsync(
					new(Guid.NewGuid(), TestTenantId, ExceptionWindowScope.AcademicYear, year, DateTime.UtcNow.Date,
						DateTime.UtcNow.Date.AddDays(2), Enum.Parse<ExceptionWindowEffect>(currentWindow),
						currentWindow == "ExtendGracePeriod" ? 3 : null, "PERIOD", "Prospective period exception", FakeAdminUserId), autoSave: true);
			var charge = (await _feeCollections.GetCollectFormAsync(student, year)).Charges.Single();
			charge.PendingAmount.ShouldBe(expectedAmount);
			var json = JsonSerializer.SerializeToElement(charge, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
			AssertAppliedPeriod(json, first.DueDate.AddDays(1), second.DueDate.AddDays(5), 10, 2);
			var intervals = json.GetProperty("lateFeeAppliedIntervals").EnumerateArray().ToList();
			intervals[0].GetProperty("throughDate").GetString().ShouldBe(first.DueDate.AddDays(5).ToString("yyyy-MM-dd"));
			intervals[1].GetProperty("fromDate").GetString().ShouldBe(second.DueDate.AddDays(1).ToString("yyyy-MM-dd"));
		}
	}

	[Fact]
	public async Task Collect_Form_Late_Fee_Period_Uses_Current_Exception_Grace_Extension()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (year, student, first, _) = await SeedCommonLateFeeAsync("PerDay", 40);
			await GetRequiredService<IRepository<CollectionPolicyExceptionWindow, Guid>>().InsertAsync(
				new(Guid.NewGuid(), TestTenantId, ExceptionWindowScope.AcademicYear, year, DateTime.UtcNow.Date,
					DateTime.UtcNow.Date.AddDays(2), ExceptionWindowEffect.ExtendGracePeriod, 3,
					"PERIOD", "Period grace extension", FakeAdminUserId), autoSave: true);
			var charge = (await _feeCollections.GetCollectFormAsync(student, year)).Charges.Single();
			charge.PendingAmount.ShouldBe(370d);
			var json = JsonSerializer.SerializeToElement(charge, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
			AssertAppliedPeriod(json, first.DueDate.AddDays(4), DateTime.UtcNow.Date, 37, 1);
		}
	}

	[Fact]
	public async Task Collect_Form_Late_Fee_Period_Does_Not_Invent_Missing_Legacy_Assessment_Dates()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (year, student, first, second) = await SeedCommonLateFeeAsync("Flat", 40);
			var assignment = await _studentFeeStructureRepo.GetAsync(first.StudentFeeStructureId!.Value);
			first.PreserveLateFeeAssessment(10m, DateTimeOffset.UtcNow, assignment.CollectionPolicyVersionId!.Value,
				"Common late fee across selected fee heads and overdue installments. Historical assessment.");
			foreach (var due in new[] { first, second })
			{
				due.ApplyPayment(due.OutstandingAmount);
				await _dueFeeRepo.UpdateAsync(due, autoSave: true);
			}
			var charge = (await _feeCollections.GetCollectFormAsync(student, year)).Charges.Single();
			charge.PendingAmount.ShouldBe(10d);
			var json = JsonSerializer.SerializeToElement(charge, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
			json.GetProperty("dueDate").GetString().ShouldBe(first.DueDate.ToString("yyyy-MM-dd"));
			json.GetProperty("lateFeeAppliedFromDate").ValueKind.ShouldBe(JsonValueKind.Null);
			json.GetProperty("lateFeeAppliedThroughDate").ValueKind.ShouldBe(JsonValueKind.Null);
			json.GetProperty("lateFeeAppliedDays").ValueKind.ShouldBe(JsonValueKind.Null);
			json.GetProperty("lateFeeAppliedIntervals").ValueKind.ShouldBe(JsonValueKind.Null);
		}
	}

	[Fact]
	public async Task Collect_Form_Reconstructs_Legacy_Individual_Period_From_Pinned_Assessment_When_No_Exception_Could_Change_It()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			const int daysOverdue = 10;
			var (yearId, student, _, dueId, _) = await SeedStudentWithOverdueDueFeeAsync(daysOverdue, attachPolicy: true);
			var due = await _dueFeeRepo.GetAsync(dueId);
			var assignment = await _studentFeeStructureRepo.GetAsync(due.StudentFeeStructureId!.Value);
			var assessmentAt = DateTimeOffset.UtcNow;
			due.PreserveLateFeeAssessment(daysOverdue * 20m, assessmentAt,
				assignment.CollectionPolicyVersionId!.Value, "Historical individual late-fee assessment.");
			await _dueFeeRepo.UpdateAsync(due, autoSave: true);

			var charge = (await _feeCollections.GetCollectFormAsync(student, yearId)).Charges.Single();
			var json = JsonSerializer.SerializeToElement(charge, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
			AssertAppliedPeriod(json, due.DueDate.AddDays(1), assessmentAt.DateTime.Date, daysOverdue, 1);
		}
	}

	[Fact]
	public async Task Collect_Form_Leaves_Legacy_Period_Unknown_When_An_Exception_Window_Overlaps_It()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			const int daysOverdue = 10;
			var (yearId, student, _, dueId, _) = await SeedStudentWithOverdueDueFeeAsync(daysOverdue, attachPolicy: true);
			var due = await _dueFeeRepo.GetAsync(dueId);
			var assignment = await _studentFeeStructureRepo.GetAsync(due.StudentFeeStructureId!.Value);
			var windowRepo = GetRequiredService<IRepository<CollectionPolicyExceptionWindow, Guid>>();
			await windowRepo.InsertAsync(new(Guid.NewGuid(), TestTenantId, ExceptionWindowScope.AcademicYear, yearId,
				due.DueDate.AddDays(2), due.DueDate.AddDays(4), ExceptionWindowEffect.SuspendLateFeeAccrual, null,
				"PERIOD", "Historical period verification window", FakeAdminUserId), autoSave: true);
			var assessmentAt = DateTimeOffset.UtcNow;
			due.PreserveLateFeeAssessment(daysOverdue * 20m, assessmentAt,
				assignment.CollectionPolicyVersionId!.Value, "Historical individual late-fee assessment.");
			await _dueFeeRepo.UpdateAsync(due, autoSave: true);

			var charge = (await _feeCollections.GetCollectFormAsync(student, yearId)).Charges.Single();
			var json = JsonSerializer.SerializeToElement(charge, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
			json.GetProperty("lateFeeAppliedIntervals").ValueKind.ShouldBe(JsonValueKind.Null);
		}
	}

	[Fact]
	public async Task Collect_Form_Leaves_Legacy_Period_Unknown_When_Exception_History_Changed_After_Assessment()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			const int daysOverdue = 10;
			var (yearId, student, _, dueId, _) = await SeedStudentWithOverdueDueFeeAsync(daysOverdue, attachPolicy: true);
			var due = await _dueFeeRepo.GetAsync(dueId);
			var assignment = await _studentFeeStructureRepo.GetAsync(due.StudentFeeStructureId!.Value);
			var assessmentAt = DateTimeOffset.UtcNow.AddMinutes(-1);
			due.PreserveLateFeeAssessment(daysOverdue * 20m, assessmentAt,
				assignment.CollectionPolicyVersionId!.Value, "Historical individual late-fee assessment.");
			await _dueFeeRepo.UpdateAsync(due, autoSave: true);
			await GetRequiredService<IRepository<CollectionPolicyExceptionWindow, Guid>>().InsertAsync(
				new(Guid.NewGuid(), TestTenantId, ExceptionWindowScope.AcademicYear, yearId,
					DateTime.UtcNow.Date.AddDays(30), DateTime.UtcNow.Date.AddDays(31), ExceptionWindowEffect.SuspendLateFeeAccrual, null,
					"PERIOD", "Window created after historical assessment", FakeAdminUserId), autoSave: true);

			var charge = (await _feeCollections.GetCollectFormAsync(student, yearId)).Charges.Single();
			var json = JsonSerializer.SerializeToElement(charge, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
			json.GetProperty("lateFeeAppliedIntervals").ValueKind.ShouldBe(JsonValueKind.Null);
		}
	}

	[Fact]
	public async Task Collect_Form_Late_Fee_Period_Retains_Assessment_Dates_After_Principal_Settlement()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (year, student, first, second) = await SeedCommonLateFeeAsync("PerDay", 40);
			await ReadCommonBalancesAsync([first, second], year, DateTime.UtcNow.Date, preserveAssessment: true);
			foreach (var row in new[] { first, second })
			{
				var due = await _dueFeeRepo.GetAsync(row.Id);
				due.ApplyPayment(due.OutstandingAmount);
				await _dueFeeRepo.UpdateAsync(due, autoSave: true);
			}
			var charge = (await _feeCollections.GetCollectFormAsync(student, year)).Charges.Single();
			charge.PendingAmount.ShouldBe(400d);
			var json = JsonSerializer.SerializeToElement(charge, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
			AssertAppliedPeriod(json, first.DueDate.AddDays(1), DateTime.UtcNow.Date, 40, 1);
		}
	}

	private static void AssertAppliedPeriod(JsonElement charge, DateTime from, DateTime through, int days, int intervalCount)
	{
		charge.TryGetProperty("lateFeeAppliedFromDate", out var actualFrom).ShouldBeTrue("The server must expose the actual first charged date.");
		actualFrom.GetString().ShouldBe(from.ToString("yyyy-MM-dd"));
		charge.GetProperty("lateFeeAppliedThroughDate").GetString().ShouldBe(through.ToString("yyyy-MM-dd"));
		charge.GetProperty("lateFeeAppliedDays").GetInt32().ShouldBe(days);
		charge.GetProperty("lateFeeAppliedIntervals").GetArrayLength().ShouldBe(intervalCount);
	}
}
