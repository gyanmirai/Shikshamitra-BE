using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class LateFeeCollectionRuntime_Tests
{
	[Theory]
	[InlineData("Flat", 1, 10)]
	[InlineData("Flat", 40, 10)]
	[InlineData("PerDay", 10, 100)]
	[InlineData("PerWeek", 8, 20)]
	[InlineData("PerMonth", 31, 20)]
	public async Task Selected_Heads_And_Months_Share_One_Late_Fee(string type, int days, int expected)
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (year, student, first, second) = await SeedCommonLateFeeAsync(type, days);
			var form = await _feeCollections.GetCollectFormAsync(student, year);
			var charges = form.Charges.Where(x => x.SourceType == "LateFee").ToList();
			charges.Count.ShouldBe(1);
			charges.Single().PendingAmount.ShouldBe(expected);
			charges.Single().Name.ShouldBe("Late Fee");
			var details = await _feeCollections.GetStudentFeeDetailsAsync(student, year);
			details.Charges!.Sum(x => x.PendingAmount).ShouldBe(expected);
			var summary = await GetSummaryRowAsync(year, student);
			summary.PendingCharges.ShouldBe(expected);

			// A caller requesting only a later row cannot generate a second common charge.
			var balances = await ReadCommonBalancesAsync([second], year, DateTime.UtcNow.Date);
			balances[second.Id].PendingAmount.ShouldBe(0m);
		}
	}

	[Theory]
	[InlineData(-1, 0)]
	[InlineData(0, 10)]
	[InlineData(1, 10)]
	public async Task Joining_Date_Excludes_PreAdmission_Dues_But_Includes_Same_Day(int dueOffset, int expected)
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (year, student, head, dueId, _) = await SeedStudentWithOverdueDueFeeAsync(10, true);
			var due = await _dueFeeRepo.GetAsync(dueId);
			var assignment = await _studentFeeStructureRepo.GetAsync(due.StudentFeeStructureId!.Value);
			assignment.CaptureAssignment(new string('a', 64), JsonSerializer.Serialize(new
			{
				RegistrationJoiningDate = due.DueDate.AddDays(-dueOffset).AddHours(12),
			}));
			await _studentFeeStructureRepo.UpdateAsync(assignment, autoSave: true);
			await ConfigureCommonPolicyAsync(assignment, head, null, "Flat");
			var form = await _feeCollections.GetCollectFormAsync(student, year);
			form.Charges.Where(x => x.SourceType == "LateFee").Sum(x => x.PendingAmount).ShouldBe(expected);
		}
	}

	[Fact]
	public async Task PreAdmission_Head_Does_Not_Set_The_Common_Accrual_Start()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (year, student, old, recent) = await SeedCommonLateFeeAsync("PerDay", 40);
			var assignment = await _studentFeeStructureRepo.GetAsync(old.StudentFeeStructureId!.Value);
			assignment.CaptureAssignment(new string('b', 64), JsonSerializer.Serialize(new
			{
				RegistrationJoiningDate = old.DueDate.AddDays(1),
			}));
			await _studentFeeStructureRepo.UpdateAsync(assignment, autoSave: true);
			var balances = await ReadCommonBalancesAsync([old, recent], year, DateTime.UtcNow.Date);
			balances[old.Id].PendingAmount.ShouldBe(0m);
			balances[recent.Id].PendingAmount.ShouldBe(100m);
		}
	}

	[Fact]
	public async Task Shared_Daily_Charge_Continues_After_The_Oldest_Principal_Is_Paid()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (year, student, first, second) = await SeedCommonLateFeeAsync("PerDay", 40);
			await _feeCollections.CollectFeeAsync(new CollectFeeInput
			{
				IdempotencyKey = Guid.NewGuid().ToString("N"), StudentId = student, AcademicYearId = year,
				DueFeePayments = [new() { DueFeeId = first.Id, FeeHeadId = first.FeeHeadId, Amount = (double)first.OutstandingAmount }],
				Payments = [new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = (double)first.OutstandingAmount }],
			});
			var balances = await ReadCommonBalancesAsync(
				[await _dueFeeRepo.GetAsync(first.Id), second], year, DateTime.UtcNow.Date.AddDays(1));
			balances.Values.Sum(x => x.Total).ShouldBe(410m);
		}
	}

	[Fact]
	public async Task Exempt_Student_Can_Collect_Principal_Without_Admission_Date_Evidence()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (year, student, _, dueId, structure) = await SeedStudentWithOverdueDueFeeAsync(10, true);
			var due = await _dueFeeRepo.GetAsync(dueId);
			var assignment = await _studentFeeStructureRepo.GetAsync(due.StudentFeeStructureId!.Value);
			assignment.CaptureAssignment(new string('c', 64), "invalid historical snapshot");
			await _studentFeeStructureRepo.UpdateAsync(assignment, autoSave: true);
			var exemption = new StudentFeePolicyOverride(Guid.NewGuid(), TestTenantId, student, structure);
			exemption.Update(true, null, false, null);
			await GetRequiredService<IRepository<StudentFeePolicyOverride, Guid>>().InsertAsync(exemption, autoSave: true);
			var form = await _feeCollections.GetCollectFormAsync(student, year);
			form.Charges.ShouldBeEmpty();
			form.Summary.PendingFee.ShouldBe(5000d);
		}
	}

	[Fact]
	public async Task Legacy_Assignment_Uses_Canonical_Admission_Joining_Date()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (year, student, _, dueId, _) = await SeedStudentWithOverdueDueFeeAsync(10, true);
			var due = await _dueFeeRepo.GetAsync(dueId);
			await WithUnitOfWorkAsync(async () =>
			{
				var studentRow = await _studentRepo.GetAsync(student);
				var standard = new Standard(Guid.NewGuid(), TestTenantId, "Fallback class", 10, studentRow.AcademicMediumStageId!.Value, null);
				await GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(standard, autoSave: true);
				var cycle = new AdmissionCycle(Guid.NewGuid(), TestTenantId, "Fallback", "FB-" + Guid.NewGuid().ToString("N")[..8], year,
					DateTime.UtcNow.Date.AddMonths(-3), DateTime.UtcNow.Date.AddMonths(3));
				await GetRequiredService<IRepository<AdmissionCycle, Guid>>().InsertAsync(cycle, autoSave: true);
				var offering = new AdmissionOffering(Guid.NewGuid(), TestTenantId, cycle.Id, null,
					studentRow.AcademicBoardId!.Value, studentRow.AcademicMediumId, studentRow.AcademicMediumStageId.Value,
					standard.Id, "Fallback", true, null, null);
				await GetRequiredService<IRepository<AdmissionOffering, Guid>>().InsertAsync(offering, autoSave: true);
				var application = new AdmissionApplication(Guid.NewGuid(), TestTenantId, $"APP-{Guid.NewGuid():N}", Guid.NewGuid(),
					null, cycle.Id, offering.Id, Guid.NewGuid(), Guid.NewGuid());
				application.ConfirmJoiningDate(due.DueDate.AddDays(1), locked: false);
				application.Submit(); application.Approve(FakeAdminUserId, null);
				application.IssueOffer(DateTime.UtcNow.AddDays(1), FakeAdminUserId, null);
				application.AcceptOffer(FakeAdminUserId, null);
				application.MarkEnrolled(student, Guid.NewGuid());
				await GetRequiredService<IRepository<AdmissionApplication, Guid>>().InsertAsync(application, autoSave: true);
			});
			(await _feeCollections.GetCollectFormAsync(student, year)).Charges.ShouldBeEmpty();
		}
	}

	[Theory]
	[InlineData(null)]
	[InlineData("SuspendLateFeeAccrual")]
	[InlineData("ExtendGracePeriod")]
	public async Task Closed_Legacy_Intervals_Are_Combined_Without_Charging_The_Gap(string? currentWindow)
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (year, student, first, second) = await SeedCommonLateFeeAsync("PerDay", 40);
			var assignment = await _studentFeeStructureRepo.GetAsync(first.StudentFeeStructureId!.Value);
			// This fixture represents an assignment pinned before either historical payment.
			assignment.ExtraProperties.Remove("AssignedPolicyHistory");
			await _studentFeeStructureRepo.UpdateAsync(assignment, autoSave: true);
			foreach (var due in new[] { first, second })
			{
				var before = due.OutstandingAmount;
				due.ApplyPayment(before);
				await _dueFeeRepo.UpdateAsync(due, autoSave: true);
				var tx = new FeeRecordMetadata(Guid.NewGuid(), TestTenantId, student, year, (double)before,
					$"LEG-{Guid.NewGuid():N}", FeeCollectionStatus.Paid);
				tx.SetCommandResult(JsonSerializer.Serialize(new
				{
					CollectedAt = due.DueDate.AddDays(5), AuditSummary = new
					{ AllocationLines = new[] { new { DueFeeId = due.Id, PendingBefore = before } } },
				}));
				await GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().InsertAsync(tx, autoSave: true);
				await GetRequiredService<IRepository<FeeCollectionDueFeeAllocation, Guid>>().InsertAsync(
					new(Guid.NewGuid(), TestTenantId, tx.Id, due.Id, (double)before), autoSave: true);
			}
			if (currentWindow is not null)
				await GetRequiredService<IRepository<CollectionPolicyExceptionWindow, Guid>>().InsertAsync(
					new(Guid.NewGuid(), TestTenantId, ExceptionWindowScope.AcademicYear, year, DateTime.UtcNow.Date,
						DateTime.UtcNow.Date.AddDays(2), Enum.Parse<ExceptionWindowEffect>(currentWindow),
						currentWindow == "ExtendGracePeriod" ? 3 : null, "CURRENT", "Prospective exception", FakeAdminUserId), autoSave: true);
			var balances = await ReadCommonBalancesAsync([first, second], year, DateTime.UtcNow.Date);
			balances.Values.Sum(x => x.Total).ShouldBe(100m); // two five-day intervals, with no accrual between them
		}
	}

	[Fact]
	public async Task Common_Daily_Charge_Stops_When_All_Principal_Is_Settled()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (year, student, first, second) = await SeedCommonLateFeeAsync("PerDay", 40);
			await _feeCollections.CollectFeeAsync(new CollectFeeInput
			{
				IdempotencyKey = Guid.NewGuid().ToString("N"), StudentId = student, AcademicYearId = year,
				DueFeePayments = new[] { first, second }.Select(d => new DueFeePaymentDto
					{ DueFeeId = d.Id, FeeHeadId = d.FeeHeadId, Amount = (double)d.OutstandingAmount }).ToList(),
				Payments = [new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = (double)(first.OutstandingAmount + second.OutstandingAmount) }],
			});
			var form = await _feeCollections.GetCollectFormAsync(student, year);
			form.Charges.Single().PendingAmount.ShouldBe(400d);
			var balances = await ReadCommonBalancesAsync([first, second], year, DateTime.UtcNow.Date.AddDays(30));
			balances.Values.Sum(x => x.Total).ShouldBe(400m);
		}
	}

	[Fact]
	public async Task Paid_Common_Flat_Charge_Does_Not_Reappear_On_Another_Head()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (year, student, _, second) = await SeedCommonLateFeeAsync("Flat", 40);
			var charge = (await _feeCollections.GetCollectFormAsync(student, year)).Charges.Single();
			await _feeCollections.CollectFeeAsync(new CollectFeeInput
			{
				IdempotencyKey = Guid.NewGuid().ToString("N"), StudentId = student, AcademicYearId = year,
				LateFeePayments = [new() { DueFeeId = charge.SourceId, Amount = 10d }],
				Payments = [new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = 10d }],
			});
			var form = await _feeCollections.GetCollectFormAsync(student, year);
			form.Charges.Count.ShouldBe(1);
			form.Charges.Single().PaidAmount.ShouldBe(10d);
			form.Charges.Single().PendingAmount.ShouldBe(0d);
			(await ReadCommonBalancesAsync([second], year, DateTime.UtcNow.Date.AddDays(30)))[second.Id].PendingAmount.ShouldBe(0m);
		}
	}

	[Fact]
	public async Task Preserved_Common_Charge_Is_Not_Reissued_On_Another_Head_Or_Month()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (year, _, first, second) = await SeedCommonLateFeeAsync("Flat", 40);
			var balances = await ReadCommonBalancesAsync([first, second], year, DateTime.UtcNow.Date, preserveAssessment: true);
			balances.Values.Sum(x => x.Total).ShouldBe(10m);
			first = await _dueFeeRepo.GetAsync(first.Id);
			first.ApplyPayment(first.OutstandingAmount);
			await _dueFeeRepo.UpdateAsync(first, autoSave: true);
			balances = await ReadCommonBalancesAsync([first, second], year, DateTime.UtcNow.Date.AddDays(30), preserveAssessment: true);
			balances.Values.Sum(x => x.Total).ShouldBe(10m);
			balances[second.Id].Total.ShouldBe(0m);
		}
	}

	private Task<Dictionary<Guid, LateFeeBalanceService.Balance>> ReadCommonBalancesAsync(
		IReadOnlyCollection<DueFee> dues, Guid year, DateTime date, bool preserveAssessment = false) =>
		WithUnitOfWorkAsync(() => GetRequiredService<LateFeeBalanceService>().GetBalancesAsync(dues, year, date, preserveAssessment));

	private async Task<(Guid Year, Guid Student, DueFee First, DueFee Second)> SeedCommonLateFeeAsync(string type, int days)
	{
		var (year, student, headId, firstId, _) = await SeedStudentWithOverdueDueFeeAsync(days, true);
		var first = await _dueFeeRepo.GetAsync(firstId);
		var assignment = await _studentFeeStructureRepo.GetAsync(first.StudentFeeStructureId!.Value);
		var head = await _feeHeadRepo.GetAsync(headId);
		var secondHead = new FeeHead(Guid.NewGuid(), TestTenantId, year, head.FeeCategoryId,
			"Transport", head.Module, false, "Transport", head.IncomeCategoryId);
		await _feeHeadRepo.InsertAsync(secondHead, autoSave: true);
		await ConfigureCommonPolicyAsync(assignment, headId, secondHead.Id, type);
		var date = first.DueDate.AddDays(Math.Min(30, Math.Max(1, days - 1)));
		var second = new DueFee(Guid.NewGuid(), TestTenantId, student, secondHead.Id,
			(MonthEnum)(date.Month - 1), date.Year, false, 1000m, date);
		second.BindAssignment(assignment.Id);
		await _dueFeeRepo.InsertAsync(second, autoSave: true);
		return (year, student, first, second);
	}

	private async Task ConfigureCommonPolicyAsync(StudentFeeStructure assignment, Guid firstHead, Guid? secondHead, string type)
	{
		var saved = await _collectionPolicies.GetAsync(assignment.CollectionPolicyId!.Value);
		var head = await _feeHeadRepo.GetAsync(firstHead);
		var updated = await _collectionPolicies.UpdateAsync(saved.Id, new CreateUpdateCollectionPolicyDto
		{
			AcademicYearId = assignment.AcademicYearId, PolicyName = saved.PolicyName, PolicyCode = saved.PolicyCode,
			Status = "Active", IsDefault = saved.IsDefault, EnableLateFee = true, LateFeeIncomeCategoryId = head.IncomeCategoryId,
			LateFeeApplyOnFeeHeads = [],
			LateFeeType = type, FlatAmount = 10m, AmountPerDay = 10m, AmountPerWeek = 10m, AmountPerMonth = 10m,
		});
		updated = await _collectionPolicies.GetAsync(updated.Id);
		assignment.ChangeAssignedPolicies(null, null, ConcessionFeeHeadScope.EntireFeeStructure, [],
			updated.Id, updated.CurrentVersionId!.Value, null, null, null, null, "Test common charge", FakeAdminUserId, DateTime.UtcNow);
		await _studentFeeStructureRepo.UpdateAsync(assignment, autoSave: true);
	}
}
