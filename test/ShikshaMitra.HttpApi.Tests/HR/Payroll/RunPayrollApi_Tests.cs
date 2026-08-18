using System.Collections.Generic;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Employees.Status;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.SalaryStructure;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class RunPayrollApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/payroll/run";
	private const string ReviewUrl = "/api/payroll/run/reviews";
	private const string PayrollMonth = "APRIL-2026";

	public RunPayrollApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Payroll_Preview_Without_500()
	{
		var response = await Client.GetAsync($"{BaseUrl}?month=2026-05&page=0&pageSize=100");
		var body = await response.Content.ReadAsStringAsync();

		body.ShouldNotContain("HR:Payroll:030011");
		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.OK,
			HttpStatusCode.BadRequest,
			HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Register_Should_Recognize_Saved_Components_Without_Payroll_Metadata()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"SavedStructureOnly{Guid.NewGuid():N}",
			dateOfJoining: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));
		var earning = await PayrollTestHelper.CreateEarningComponentAsync(
			Client,
			$"SavedStructureEarn{Guid.NewGuid():N}");
		var deduction = await PayrollTestHelper.CreateDeductionComponentAsync(
			Client,
			$"SavedStructureDed{Guid.NewGuid():N}");

		var structureResponse = await Client.PutAsJsonAsync(
			$"/api/payroll/employees/{employee.Id}/salary-structure",
			new UpdateSalaryStructureDto
			{
				EarningComponents =
				[
					new SalaryComponentDto
					{
						ComponentId = earning.Id,
						Name = earning.Name,
						Amount = 25000m,
						IsFixed = true,
						IsTemp = false,
						ApplicableMonth = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)
					}
				],
				DeductionComponents =
				[
					new SalaryComponentDto
					{
						ComponentId = deduction.Id,
						Name = deduction.Name,
						Amount = 1000m,
						IsFixed = true,
						IsTemp = false,
						ApplicableMonth = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)
					}
				]
			},
			JsonOptions);
		var structureBody = await structureResponse.Content.ReadAsStringAsync();
		structureResponse.StatusCode.ShouldBe(HttpStatusCode.OK, structureBody);

		var preview = await GetAndReadAsync<RunPayrollResponse>(
			$"{BaseUrl}?month=2026-05&page=0&pageSize=100&forceRefresh=true");
		var item = preview.Items.Single(x => x.EmployeeId == employee.Id);

		item.EarningComponents.ShouldNotBeEmpty();
		item.IssueCodes.ShouldNotContain(IssueCodes.MissingSalaryStructure);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Register_Should_Ignore_Holiday_Attendance_For_Selected_Academic_Year()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"AkshayPayrollFlow{Guid.NewGuid():N}",
			dateOfJoining: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

		await AssignSalaryStructureAsync(employee.Id, "AkshayPayrollFlow");
		await SeedAttendanceOnDateAsync(employee.Id, new DateOnly(2026, 5, 10));

		var preview = await GetAndReadAsync<RunPayrollResponse>(
			$"{BaseUrl}?month=2026-05&academicYearId={Factory.SeededAcademicYearId}&page=0&pageSize=100&forceRefresh=true");
		var item = preview.Items.Single(x => x.EmployeeId == employee.Id);

		item.BasisDays.ShouldBe(0);
		item.PayableDays.ShouldBe(0);
		item.IssueCodes.ShouldContain("NO_WORKING_DAYS");
		item.IssueCodes.ShouldNotContain("NO_ATTENDANCE");
		item.IssueCodes.ShouldNotContain("PAYROLL_POLICY_CONFIGURATION");
		item.IssueCodes.ShouldNotContain("MISSING_SALARY_STRUCTURE");
		item.FullGrossSalary.ShouldBeGreaterThan(0);
		item.GrossSalary.ShouldBe(0);
		item.NetPay.ShouldBe(0);

		preview.Readiness.CanLock.ShouldBeFalse();
		preview.Readiness.BlockingIssues.ShouldContain("NO_WORKING_DAYS");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Register_Should_Reject_Academic_Year_When_Month_Is_Outside_Range()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var response = await Client.GetAsync(
			$"{BaseUrl}?month=2040-05&academicYearId={Factory.SeededAcademicYearId}&page=0&pageSize=100");

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var body = await response.Content.ReadAsStringAsync();
		body.ShouldContain("HR:Payroll:030031");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Register_Should_Recompute_Unlocked_Zero_Basis_Snapshot()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"PayrollZeroBasis{Guid.NewGuid():N}",
			dateOfJoining: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

		await AssignSalaryStructureAsync(employee.Id, "PayrollZeroBasis");
		await SeedAttendanceOnDateAsync(employee.Id, new DateOnly(2026, 5, 10));

		var firstPreview = await GetAndReadAsync<RunPayrollResponse>(
			$"{BaseUrl}?month=2026-05&academicYearId={Factory.SeededAcademicYearId}&page=0&pageSize=100&forceRefresh=true");
		firstPreview.Items.Single(x => x.EmployeeId == employee.Id).BasisDays.ShouldBe(0);

		await CorruptPayrollSnapshotBasisAsync(employee.Id, "2026-05");

		var refreshedPreview = await GetAndReadAsync<RunPayrollResponse>(
			$"{BaseUrl}?month=2026-05&academicYearId={Factory.SeededAcademicYearId}&page=0&pageSize=100&forceRefresh=false");
		var refreshedItem = refreshedPreview.Items.Single(x => x.EmployeeId == employee.Id);

		refreshedItem.BasisDays.ShouldBe(0);
		refreshedItem.PayableDays.ShouldBe(0);
		refreshedItem.IssueCodes.ShouldContain("NO_WORKING_DAYS");
		refreshedItem.IssueCodes.ShouldNotContain("NO_ATTENDANCE");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Register_Should_Exclude_Employee_Before_Joining_Month()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			"PayrollMayJoin",
			dateOfJoining: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

		var aprilPreview = await GetAndReadAsync<RunPayrollResponse>(
			$"{BaseUrl}?month=2026-04&page=0&pageSize=100&forceRefresh=true");
		aprilPreview.Items.Any(x => x.EmployeeId == employee.Id).ShouldBeFalse();

		var mayPreview = await GetAndReadAsync<RunPayrollResponse>(
			$"{BaseUrl}?month=2026-05&page=0&pageSize=100&forceRefresh=true");
		mayPreview.Items.Any(x => x.EmployeeId == employee.Id).ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Readiness_Should_Not_Count_Employee_Before_Joining_Month()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var before = await GetAndReadAsync<RunPayrollReadinessSummary>(
			$"{BaseUrl}/readiness?month=2026-04");

		await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			"PayrollReadinessMayJoin",
			dateOfJoining: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

		var after = await GetAndReadAsync<RunPayrollReadinessSummary>(
			$"{BaseUrl}/readiness?month=2026-04");
		after.EmployeeCount.ShouldBe(before.EmployeeCount);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Register_Should_Include_Inactive_Employee_For_Effective_Month_Only()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"PayrollInactiveFinal{Guid.NewGuid():N}",
			dateOfJoining: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));

		await SetEmployeeStatusAsync(employee.Id, 1, new DateOnly(2026, 5, 31));

		var mayPreview = await GetAndReadAsync<RunPayrollResponse>(
			$"{BaseUrl}?month=2026-05&page=0&pageSize=100&forceRefresh=true");
		mayPreview.Items.Any(x => x.EmployeeId == employee.Id).ShouldBeTrue();

		var junePreview = await GetAndReadAsync<RunPayrollResponse>(
			$"{BaseUrl}?month=2026-06&page=0&pageSize=100&forceRefresh=true");
		junePreview.Items.Any(x => x.EmployeeId == employee.Id).ShouldBeFalse();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Register_Should_Report_No_Working_Days_For_Final_Month_Inactive_Employee_During_Holiday()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"PayrollInactiveNoAttendance{Guid.NewGuid():N}",
			dateOfJoining: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

		await SetEmployeeStatusAsync(employee.Id, 1, new DateOnly(2026, 5, 31));

		var mayPreview = await GetAndReadAsync<RunPayrollResponse>(
			$"{BaseUrl}?month=2026-05&page=0&pageSize=100&forceRefresh=true");
		var item = mayPreview.Items.Single(x => x.EmployeeId == employee.Id);
		item.BasisDays.ShouldBe(0);
		item.PayableDays.ShouldBe(0);
		item.IssueCodes.ShouldContain("NO_WORKING_DAYS");
		item.IssueCodes.ShouldNotContain("NO_ATTENDANCE");
		item.IssueCodes.ShouldContain("MISSING_SALARY_STRUCTURE");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Register_Should_Include_Left_Employee_For_Final_Month_And_Lock()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var before = await GetAndReadAsync<RunPayrollReadinessSummary>(
			$"{BaseUrl}/readiness?month=2026-03");
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"PayrollLeftFinal{Guid.NewGuid():N}",
			dateOfJoining: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));

		await AssignSalaryStructureAsync(employee.Id, "PayrollLeftFinal");
		await MarkAttendanceAsync(employee.Id, "11-03-2026");
		await SetEmployeeStatusAsync(employee.Id, 2, new DateOnly(2026, 3, 20));

		var marchPreview = await GetAndReadAsync<RunPayrollResponse>(
			$"{BaseUrl}?month=2026-03&page=0&pageSize=100&forceRefresh=true");
		var previewItem = marchPreview.Items.Single(x => x.EmployeeId == employee.Id);
		previewItem.IssueCodes.ShouldNotContain("NO_ATTENDANCE");
		previewItem.IssueCodes.ShouldNotContain("MISSING_SALARY_STRUCTURE");
		previewItem.IssueCodes.ShouldNotContain("NEGATIVE_NET_PAY");
		previewItem.IssueCodes.ShouldNotContain("PAYROLL_POLICY_CONFIGURATION");

		var marchReadiness = await GetAndReadAsync<RunPayrollReadinessSummary>(
			$"{BaseUrl}/readiness?month=2026-03");
		marchReadiness.EmployeeCount.ShouldBeGreaterThanOrEqualTo(before.EmployeeCount + 1);

		var reviewResponse = await Client.PostAsJsonAsync(
			ReviewUrl,
			new PayrollRunReviewRequest
			{
				Month = "2026-03",
				EmployeeIds = new List<Guid> { employee.Id }
			},
			JsonOptions);
		var reviewBody = await reviewResponse.Content.ReadAsStringAsync();
		reviewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, reviewBody);

		var lockResponse = await Client.PostAsJsonAsync(
			BaseUrl,
			new RunPayrollRequest
			{
				Month = "2026-03",
				EmployeeIds = new List<Guid> { employee.Id }
			},
			JsonOptions);
		var lockBody = await lockResponse.Content.ReadAsStringAsync();
		lockResponse.StatusCode.ShouldBe(HttpStatusCode.OK, lockBody);

		var junePreview = await GetAndReadAsync<RunPayrollResponse>(
			$"{BaseUrl}?month=2026-06&page=0&pageSize=100&forceRefresh=true");
		junePreview.Items.Any(x => x.EmployeeId == employee.Id).ShouldBeFalse();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Register_Should_Use_Status_Change_Date_When_Effective_Date_Is_Missing()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"PayrollLeftNoEffectiveDate{Guid.NewGuid():N}",
			dateOfJoining: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

		await SetEmployeeStatusAsync(employee.Id, 2, new DateOnly(2026, 5, 20));
		await ClearStatusEffectiveDateAsync(employee.Id, new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero));

		var mayPreview = await GetAndReadAsync<RunPayrollResponse>(
			$"{BaseUrl}?month=2026-05&page=0&pageSize=100&forceRefresh=true");
		mayPreview.Items.Any(x => x.EmployeeId == employee.Id).ShouldBeTrue();

		var junePreview = await GetAndReadAsync<RunPayrollResponse>(
			$"{BaseUrl}?month=2026-06&page=0&pageSize=100&forceRefresh=true");
		junePreview.Items.Any(x => x.EmployeeId == employee.Id).ShouldBeFalse();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Readiness_Should_Refresh_After_Mark_Attendance_Save()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"PayrollAttendanceRefresh{Guid.NewGuid():N}",
			dateOfJoining: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));

		var before = await GetAndReadAsync<RunPayrollReadinessSummary>(
			$"{BaseUrl}/readiness?month=2026-03");

		var attendanceResponse = await Client.PostAsJsonAsync(
			"/api/hr/attendance/take-attendance/day/save",
			new TakeAttendanceSaveInput
			{
				AttendanceDate = "11-03-2026",
				Entries =
				[
					new TakeAttendanceSaveEntryDto
					{
						EmployeeId = employee.Id,
						StatusWire = "PRESENT",
						InTime = "09:00",
						OutTime = "17:00",
						Remarks = "Payroll readiness refresh regression"
					}
				]
			},
			JsonOptions);
		attendanceResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		await AssertAttendanceStagePersistedAsync(employee.Id, new DateOnly(2026, 3, 11));

		var after = await GetAndReadAsync<RunPayrollReadinessSummary>(
			$"{BaseUrl}/readiness?month=2026-03");

		after.EmployeeCount.ShouldBe(before.EmployeeCount);
		after.AttendanceFoundCount.ShouldBeGreaterThanOrEqualTo(before.AttendanceFoundCount + 1);
		after.MissingAttendanceCount.ShouldBeLessThanOrEqualTo(Math.Max(0, before.MissingAttendanceCount - 1));
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Register_Should_Use_Manual_Attendance_For_Payable_Days()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"PayrollManualAttendance{Guid.NewGuid():N}",
			dateOfJoining: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));

		var attendanceResponse = await Client.PostAsJsonAsync(
			"/api/hr/attendance/take-attendance/day/save",
			new TakeAttendanceSaveInput
			{
				AttendanceDate = "14-04-2026",
				Entries =
				[
					new TakeAttendanceSaveEntryDto
					{
						EmployeeId = employee.Id,
						StatusWire = "PRESENT",
						InTime = "09:00",
						OutTime = "17:00",
						Remarks = "Payroll payable days regression"
					}
				]
			},
			JsonOptions);
		attendanceResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		await AssertAttendanceStagePersistedAsync(employee.Id, new DateOnly(2026, 4, 14));

		var preview = await GetAndReadAsync<RunPayrollResponse>(
			$"{BaseUrl}?month=2026-04&page=0&pageSize=100&forceRefresh=true");
		var item = preview.Items.Single(x => x.EmployeeId == employee.Id);

		item.PresentDays.ShouldBeGreaterThanOrEqualTo(1);
		item.PayableDays.ShouldBeGreaterThan(0);
		item.IssueCodes.ShouldNotContain("NO_ATTENDANCE");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Register_Should_Reduce_Payable_Days_For_Approved_Unpaid_Leave()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		const string month = "2026-06";
		const decimal monthlyGross = 30000m;
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"PayrollUnpaidLeave{Guid.NewGuid():N}",
			dateOfJoining: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
		await AssignSalaryStructureAsync(
			employee.Id,
			"PayrollUnpaidLeave",
			earningAmount: monthlyGross,
			deductionAmount: 0m,
			useMasterComponents: false);
		await MarkAttendanceAsync(employee.Id, "04-06-2026");
		await MarkAttendanceAsync(employee.Id, "05-06-2026");
		var leavePolicyId = await EnsureActiveDefaultLeavePolicyAsync(new DateTime(2026, 6, 1));
		await SeedApprovedLeaveAsync(
			employee.Id,
			leavePolicyId,
			new DateTime(2026, 6, 5),
			LeavePaidType.Unpaid,
			approvedUnits: 1m);

		var preview = await GetPayrollPreviewAsync(month, forceRefresh: true);
		var item = preview.Items.Single(x => x.EmployeeId == employee.Id);
		item.BasisDays.ShouldBeGreaterThan(0);
		var expectedGross = Math.Round(
			monthlyGross / (decimal)item.BasisDays,
			2,
			MidpointRounding.AwayFromZero);

		item.PresentDays.ShouldBe(2);
		item.PayableDays.ShouldBe(1d);
		item.FullGrossSalary.ShouldBe((double)monthlyGross);
		item.GrossSalary.ShouldBe((double)expectedGross);
		item.NetPay.ShouldBe((double)expectedGross);
		item.IssueCodes.ShouldNotContain(IssueCodes.NoAttendance);
		item.IssueCodes.ShouldNotContain(IssueCodes.MissingSalaryStructure);
		item.IssueCodes.ShouldNotContain(IssueCodes.PayrollPolicyConfiguration);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Register_And_Payslip_Should_Include_Overtime_Earning()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		const string month = "2026-06";
		var attendanceDate = new DateOnly(2026, 6, 10);
		var shiftStart = new TimeOnly(7, 0);
		var shiftEnd = new TimeOnly(17, 30);
		const decimal monthlyGross = 25000m;
		var policySnapshot = await ConfigureDefaultAttendancePolicyOvertimeAsync(new DateTime(2026, 6, 1));
		await EnsureActiveDefaultLeavePolicyAsync(new DateTime(2026, 6, 1));

		try
		{
			var employee = await EmployeeTestHelper.CreateEmployeeAsync(
				Client,
				$"PayrollOvertime{Guid.NewGuid():N}",
				dateOfJoining: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
			var shiftId = await InsertShiftAsync(
				$"Payroll overtime shift {Guid.NewGuid():N}",
				shiftStart,
				shiftEnd);

			await AssignEmployeeShiftAsync(employee.Id, shiftId);
			await AssignSalaryStructureAsync(
				employee.Id,
				"PayrollOvertime",
				earningAmount: monthlyGross,
				deductionAmount: 0m,
				useMasterComponents: false);

			var attendanceResponse = await Client.PostAsJsonAsync(
				"/api/hr/attendance/take-attendance/day/save",
				new TakeAttendanceSaveInput
				{
					AttendanceDate = attendanceDate.ToString("dd-MM-yyyy"),
					Entries =
					[
						new TakeAttendanceSaveEntryDto
						{
							EmployeeId = employee.Id,
							StatusWire = "PRESENT",
							InTime = "07:00",
							OutTime = "19:00",
							Remarks = "Payroll overtime regression"
						}
					]
				},
				JsonOptions);
			var attendanceBody = await attendanceResponse.Content.ReadAsStringAsync();
			attendanceResponse.StatusCode.ShouldBe(HttpStatusCode.OK, attendanceBody);

			var preview = await GetPayrollPreviewAsync(month, forceRefresh: true);
			var item = preview.Items.Single(x => x.EmployeeId == employee.Id);
			var expectedOvertime = CalculateExpectedOvertime(
				monthlyGross,
				(decimal)item.BasisDays,
				shiftStart,
				shiftEnd,
				overtimeMinutes: 90m,
				multiplier: 2m);
			var expectedRegularGross = Math.Round(
				monthlyGross / (decimal)item.BasisDays,
				2,
				MidpointRounding.AwayFromZero);
			var expectedGross = expectedRegularGross + expectedOvertime;
			var overtimeLine = item.EarningComponents.Single(x => x.Name == "Overtime Pay");

			item.PresentDays.ShouldBeGreaterThanOrEqualTo(1);
			item.PayableDays.ShouldBeGreaterThan(0);
			overtimeLine.Amount.ShouldBe((double)expectedOvertime);
			overtimeLine.Description.ShouldNotBeNull();
			overtimeLine.Description.ShouldContain("90");
			item.GrossSalary.ShouldBe((double)expectedGross);
			item.NetPay.ShouldBe((double)expectedGross);

			var reviewResponse = await Client.PostAsJsonAsync(
				ReviewUrl,
				new PayrollRunReviewRequest
				{
					Month = month,
					EmployeeIds = new List<Guid> { employee.Id }
				},
				JsonOptions);
			var reviewBody = await reviewResponse.Content.ReadAsStringAsync();
			reviewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, reviewBody);
			await MarkPayrollSnapshotReviewedAsync(employee.Id, month);

			var lockResponse = await Client.PostAsJsonAsync(
				BaseUrl,
				new RunPayrollRequest
				{
					Month = month,
					EmployeeIds = new List<Guid> { employee.Id }
				},
				JsonOptions);
			var lockBody = await lockResponse.Content.ReadAsStringAsync();
			lockResponse.StatusCode.ShouldBe(HttpStatusCode.OK, lockBody);

			var salarySlip = await GetSalarySlipResponseAsync(employee.Id, "JUNE-2026");
			salarySlip.Earnings.ShouldContain(x => x.Name == "Overtime Pay" && x.Amount == (double)expectedOvertime);
			salarySlip.TotalEarning.ShouldBe((double)expectedGross);
			salarySlip.NetSalary.ShouldBe((double)expectedGross);
			salarySlip.NetPay.ShouldBe((double)expectedGross);
		}
		finally
		{
			await RestoreAttendancePolicyOvertimeAsync(policySnapshot);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Register_And_Payslip_Should_Create_Late_Arrival_Salary_Deduction()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		const string month = "2026-06";
		var attendanceDate = new DateOnly(2026, 6, 11);
		var policySnapshot = await ConfigureDefaultAttendancePolicySalaryDeductionAsync(
			new DateTime(2026, 6, 1),
			250m);
		await EnsureActiveDefaultLeavePolicyAsync(new DateTime(2026, 6, 1));

		try
		{
			var employee = await EmployeeTestHelper.CreateEmployeeAsync(
				Client,
				$"PayrollLateDeduction{Guid.NewGuid():N}",
				dateOfJoining: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
			var shiftId = await InsertShiftAsync(
				$"Payroll late deduction shift {Guid.NewGuid():N}",
				new TimeOnly(9, 0),
				new TimeOnly(17, 0));

			await AssignEmployeeShiftAsync(employee.Id, shiftId);
			await AssignSalaryStructureAsync(
				employee.Id,
				"PayrollLateDeduction",
				earningAmount: 30000m,
				deductionAmount: 0m,
				useMasterComponents: false);

			var attendanceResponse = await Client.PostAsJsonAsync(
				"/api/hr/attendance/take-attendance/day/save",
				new TakeAttendanceSaveInput
				{
					AttendanceDate = attendanceDate.ToString("dd-MM-yyyy"),
					Entries =
					[
						new TakeAttendanceSaveEntryDto
						{
							EmployeeId = employee.Id,
							StatusWire = "PRESENT",
							InTime = "09:10",
							OutTime = "17:00",
							Remarks = "Payroll late deduction regression"
						}
					]
				},
				JsonOptions);
			var attendanceBody = await attendanceResponse.Content.ReadAsStringAsync();
			attendanceResponse.StatusCode.ShouldBe(HttpStatusCode.OK, attendanceBody);

			var preview = await GetPayrollPreviewAsync(month, forceRefresh: true);
			var item = preview.Items.Single(x => x.EmployeeId == employee.Id);
			var adjustment = item.PayrollAdjustments.Single(x => x.Name == "Late arrival deduction");
			var deduction = item.DeductionComponents.Single(x => x.Name == "Late arrival deduction");

			adjustment.Amount.ShouldBe(250d);
			adjustment.IsEarning.ShouldBeFalse();
			adjustment.IsSystemGenerated.ShouldBeTrue();
			adjustment.SourceType.ShouldBe(PayrollAdjustmentSourceTypes.LateArrivalSalaryDeduction);
			deduction.Amount.ShouldBe(250d);
			item.IssueCodes.ShouldNotContain(IssueCodes.AttendanceSalaryDeductionConfiguration);
			item.AttendancePolicyWarnings.ShouldBeEmpty();
			item.PayableDays.ShouldBeGreaterThan(0);

			await ReviewAndLockPayrollAsync(employee.Id, month);
			var salarySlip = await GetSalarySlipResponseAsync(employee.Id, "JUNE-2026");
			salarySlip.Deductions.ShouldContain(x => x.Name == "Late arrival deduction" && x.Amount == 250d);
			salarySlip.TotalDeduction.ShouldBe(item.TotalDeduction);
			salarySlip.NetSalary.ShouldBe(item.NetPay);
			salarySlip.NetPay.ShouldBe(item.NetPay);
		}
		finally
		{
			await RestoreAttendancePolicyLateDeductionAsync(policySnapshot);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Register_And_Payslip_Should_Apply_Assigned_Pf_Rule()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		const string month = "2026-06";
		const string slipMonth = "JUNE-2026";
		const decimal monthlyGross = 30000m;
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"PayrollPfRule{Guid.NewGuid():N}",
			dateOfJoining: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
		var pfRule = await PayrollTestHelper.CreatePfRuleAsync(Client, "PayrollPFRule");
		var savedRule = await Client.GetFromJsonAsync<ProvidentFundDto>(
			$"/api/payroll/pf-rules/{pfRule.Id}",
			JsonOptions);
		savedRule.ShouldNotBeNull();
		savedRule.IsActive.ShouldBeTrue();
		savedRule.EffectiveFrom.ShouldBeLessThanOrEqualTo(new DateTime(2026, 6, 1));
		await EnsureActiveDefaultLeavePolicyAsync(new DateTime(2026, 6, 1));
		await AssignSalaryStructureAsync(
			employee.Id,
			"PayrollPfRule",
			earningAmount: monthlyGross,
			deductionAmount: 0m,
			useMasterComponents: false,
			pfRuleId: pfRule.Id);
		var savedPayroll = await Client.GetFromJsonAsync<EmployeePayrollResponse>(
			$"/api/employees/{employee.Id}/payroll",
			JsonOptions);
		savedPayroll.ShouldNotBeNull();
		savedPayroll.SalaryDetails.ShouldNotBeNull();
		savedPayroll.SalaryDetails.PfRuleId.ShouldBe(pfRule.Id);
		await MarkAttendanceAsync(employee.Id, "10-06-2026");

		var preview = await GetPayrollPreviewAsync(month, forceRefresh: true);
		var item = preview.Items.Single(x => x.EmployeeId == employee.Id);
		var pfDeduction = item.DeductionComponents.Single(x =>
			x.Name.StartsWith("Provident Fund -", StringComparison.OrdinalIgnoreCase));

		pfDeduction.Amount.ShouldBeGreaterThan(0d);
		pfDeduction.Description.ShouldNotBeNull();
		pfDeduction.Description.ShouldContain("12");
		item.TotalDeduction.ShouldBe(pfDeduction.Amount);
		item.NetPay.ShouldBe(item.GrossSalary - pfDeduction.Amount);

		await ReviewAndLockPayrollAsync(employee.Id, month);

		var lockedRegister = await GetPayrollPreviewAsync(month, forceRefresh: false);
		var lockedItem = lockedRegister.Items.Single(x => x.EmployeeId == employee.Id);
		lockedItem.IsLocked.ShouldBeTrue();
		lockedItem.DeductionComponents.ShouldContain(x =>
			x.Name == pfDeduction.Name &&
			x.Amount == pfDeduction.Amount);

		var salarySlip = await GetSalarySlipResponseAsync(employee.Id, slipMonth);
		salarySlip.Deductions.ShouldContain(x =>
			x.Name == pfDeduction.Name &&
			x.Amount == pfDeduction.Amount);
		salarySlip.TotalDeduction.ShouldBe(pfDeduction.Amount);
		salarySlip.NetSalary.ShouldBe(item.NetPay);
		salarySlip.NetPay.ShouldBe(item.NetPay);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Register_Should_Block_Late_Arrival_Salary_Deduction_When_Amount_Is_Missing()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		const string month = "2026-06";
		var attendanceDate = new DateOnly(2026, 6, 12);
		var policySnapshot = await ConfigureDefaultAttendancePolicySalaryDeductionAsync(
			new DateTime(2026, 6, 1),
			250m,
			graceTime: 30);
		await ForceDefaultAttendancePolicyPenaltyDeductionAmountAsync(new DateTime(2026, 6, 1), null);
		await EnsureActiveDefaultLeavePolicyAsync(new DateTime(2026, 6, 1));

		try
		{
			var employee = await EmployeeTestHelper.CreateEmployeeAsync(
				Client,
				$"PayrollLateMissingAmount{Guid.NewGuid():N}",
				dateOfJoining: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
			var shiftId = await InsertShiftAsync(
				$"Payroll late missing amount shift {Guid.NewGuid():N}",
				new TimeOnly(9, 0),
				new TimeOnly(17, 0));

			await AssignEmployeeShiftAsync(employee.Id, shiftId);
			await AssignSalaryStructureAsync(
				employee.Id,
				"PayrollLateMissingAmount",
				earningAmount: 30000m,
				deductionAmount: 0m,
				useMasterComponents: false);
			await MarkTimedAttendanceAsync(employee.Id, attendanceDate, "09:31");

			var preview = await GetPayrollPreviewAsync(month, forceRefresh: true);
			var item = preview.Items.Single(x => x.EmployeeId == employee.Id);
			item.IssueCodes.ShouldContain(IssueCodes.AttendanceSalaryDeductionConfiguration);
			item.AttendancePolicyWarnings.ShouldContain(x => x.Contains("deduction amount", StringComparison.OrdinalIgnoreCase));
			item.PayrollAdjustments.Any(x => x.Name == "Late arrival deduction").ShouldBeFalse();
			item.DeductionComponents.Any(x => x.Name == "Late arrival deduction").ShouldBeFalse();
			preview.Readiness.CanLock.ShouldBeFalse();
			preview.Readiness.BlockingIssues.ShouldContain(IssueCodes.AttendanceSalaryDeductionConfiguration);
			preview.Readiness.PolicyConfigurationIssueCount.ShouldBeGreaterThan(0);

			var reviewResponse = await Client.PostAsJsonAsync(
				ReviewUrl,
				new PayrollRunReviewRequest
				{
					Month = month,
					EmployeeIds = new List<Guid> { employee.Id }
				},
				JsonOptions);
			var reviewBody = await reviewResponse.Content.ReadAsStringAsync();
			reviewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, reviewBody);

			var lockResponse = await Client.PostAsJsonAsync(
				BaseUrl,
				new RunPayrollRequest
				{
					Month = month,
					EmployeeIds = new List<Guid> { employee.Id }
				},
				JsonOptions);
			var lockBody = await lockResponse.Content.ReadAsStringAsync();
			lockResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest, lockBody);
			lockBody.ShouldContain(HRErrorCodes.PayrollBlockingIssuesBeforeLock);
		}
		finally
		{
			await RestoreAttendancePolicyLateDeductionAsync(policySnapshot);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Register_Should_Block_Late_Arrival_Salary_Deduction_When_Shift_Is_Missing()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		const string month = "2026-06";
		var attendanceDate = new DateOnly(2026, 6, 12);
		var policySnapshot = await ConfigureDefaultAttendancePolicySalaryDeductionAsync(
			new DateTime(2026, 6, 1),
			250m,
			graceTime: 30);
		await EnsureActiveDefaultLeavePolicyAsync(new DateTime(2026, 6, 1));

		try
		{
			var employee = await EmployeeTestHelper.CreateEmployeeAsync(
				Client,
				$"PayrollLateMissingShift{Guid.NewGuid():N}",
				dateOfJoining: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

			await ClearEmployeeShiftAsync(employee.Id);
			await AssignSalaryStructureAsync(
				employee.Id,
				"PayrollLateMissingShift",
				earningAmount: 30000m,
				deductionAmount: 0m,
				useMasterComponents: false);
			await SeedTimedAttendanceOnDateAsync(employee.Id, attendanceDate, "09:31");

			var preview = await GetPayrollPreviewAsync(month, forceRefresh: true);
			var item = preview.Items.Single(x => x.EmployeeId == employee.Id);
			item.IssueCodes.ShouldContain(IssueCodes.AttendanceSalaryDeductionConfiguration);
			item.AttendancePolicyWarnings.ShouldContain(x => x.Contains("shift", StringComparison.OrdinalIgnoreCase));
			item.PayrollAdjustments.Any(x => x.Name == "Late arrival deduction").ShouldBeFalse();
			preview.Readiness.BlockingIssues.ShouldContain(IssueCodes.AttendanceSalaryDeductionConfiguration);
		}
		finally
		{
			await RestoreAttendancePolicyLateDeductionAsync(policySnapshot);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Register_Should_Block_Late_Arrival_Salary_Deduction_When_Attendance_Has_No_InTime()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		const string month = "2026-06";
		var attendanceDate = new DateOnly(2026, 6, 12);
		var policySnapshot = await ConfigureDefaultAttendancePolicySalaryDeductionAsync(
			new DateTime(2026, 6, 1),
			250m,
			graceTime: 30);
		await EnsureActiveDefaultLeavePolicyAsync(new DateTime(2026, 6, 1));

		try
		{
			var employee = await EmployeeTestHelper.CreateEmployeeAsync(
				Client,
				$"PayrollLateNoInTime{Guid.NewGuid():N}",
				dateOfJoining: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
			var shiftId = await InsertShiftAsync(
				$"Payroll late no in time shift {Guid.NewGuid():N}",
				new TimeOnly(9, 0),
				new TimeOnly(17, 0));

			await AssignEmployeeShiftAsync(employee.Id, shiftId);
			await AssignSalaryStructureAsync(
				employee.Id,
				"PayrollLateNoInTime",
				earningAmount: 30000m,
				deductionAmount: 0m,
				useMasterComponents: false);
			await SeedAttendanceOnDateAsync(employee.Id, attendanceDate);

			var preview = await GetPayrollPreviewAsync(month, forceRefresh: true);
			var item = preview.Items.Single(x => x.EmployeeId == employee.Id);
			item.IssueCodes.ShouldContain(IssueCodes.AttendanceSalaryDeductionConfiguration);
			item.AttendancePolicyWarnings.ShouldContain(x => x.Contains("in-time", StringComparison.OrdinalIgnoreCase));
			item.PayrollAdjustments.Any(x => x.Name == "Late arrival deduction").ShouldBeFalse();
			preview.Readiness.BlockingIssues.ShouldContain(IssueCodes.AttendanceSalaryDeductionConfiguration);
		}
		finally
		{
			await RestoreAttendancePolicyLateDeductionAsync(policySnapshot);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Register_Should_Apply_Late_Arrival_Deduction_Only_After_Grace_Boundary()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		const string month = "2026-06";
		var exactGraceDate = new DateOnly(2026, 6, 12);
		var overGraceDate = new DateOnly(2026, 6, 13);
		var policySnapshot = await ConfigureDefaultAttendancePolicySalaryDeductionAsync(
			new DateTime(2026, 6, 1),
			250m,
			graceTime: 30);
		await EnsureActiveDefaultLeavePolicyAsync(new DateTime(2026, 6, 1));

		try
		{
			var employee = await EmployeeTestHelper.CreateEmployeeAsync(
				Client,
				$"PayrollLateBoundary{Guid.NewGuid():N}",
				dateOfJoining: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
			var shiftId = await InsertShiftAsync(
				$"Payroll late boundary shift {Guid.NewGuid():N}",
				new TimeOnly(9, 0),
				new TimeOnly(17, 0));

			await AssignEmployeeShiftAsync(employee.Id, shiftId);
			await AssignSalaryStructureAsync(
				employee.Id,
				"PayrollLateBoundary",
				earningAmount: 30000m,
				deductionAmount: 0m,
				useMasterComponents: false);
			await MarkTimedAttendanceAsync(employee.Id, exactGraceDate, "09:30");
			await MarkTimedAttendanceAsync(employee.Id, overGraceDate, "09:31");

			var preview = await GetPayrollPreviewAsync(month, forceRefresh: true);
			var item = preview.Items.Single(x => x.EmployeeId == employee.Id);
			var adjustment = item.PayrollAdjustments.Single(x => x.Name == "Late arrival deduction");
			var deduction = item.DeductionComponents.Single(x => x.Name == "Late arrival deduction");

			adjustment.Amount.ShouldBe(250d);
			adjustment.Description.ShouldNotBeNull();
			adjustment.Description!.ShouldNotContain(exactGraceDate.ToString("yyyy-MM-dd"));
			adjustment.Description.ShouldContain(overGraceDate.ToString("yyyy-MM-dd"));
			deduction.Amount.ShouldBe(250d);
			item.IssueCodes.ShouldNotContain(IssueCodes.AttendanceSalaryDeductionConfiguration);
		}
		finally
		{
			await RestoreAttendancePolicyLateDeductionAsync(policySnapshot);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Register_Should_Refresh_Late_Arrival_Deduction_After_Amount_Changes()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		const string month = "2026-06";
		var attendanceDate = new DateOnly(2026, 6, 12);
		var policySnapshot = await ConfigureDefaultAttendancePolicySalaryDeductionAsync(
			new DateTime(2026, 6, 1),
			250m,
			graceTime: 30);
		await EnsureActiveDefaultLeavePolicyAsync(new DateTime(2026, 6, 1));

		try
		{
			var employee = await EmployeeTestHelper.CreateEmployeeAsync(
				Client,
				$"PayrollLateAmountRefresh{Guid.NewGuid():N}",
				dateOfJoining: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
			var shiftId = await InsertShiftAsync(
				$"Payroll late amount refresh shift {Guid.NewGuid():N}",
				new TimeOnly(9, 0),
				new TimeOnly(17, 0));

			await AssignEmployeeShiftAsync(employee.Id, shiftId);
			await AssignSalaryStructureAsync(
				employee.Id,
				"PayrollLateAmountRefresh",
				earningAmount: 30000m,
				deductionAmount: 0m,
				useMasterComponents: false);
			await MarkTimedAttendanceAsync(employee.Id, attendanceDate, "09:31");

			var firstPreview = await GetPayrollPreviewAsync(month, forceRefresh: true);
			var firstItem = firstPreview.Items.Single(x => x.EmployeeId == employee.Id);
			firstItem.DeductionComponents.Single(x => x.Name == "Late arrival deduction").Amount.ShouldBe(250d);

			await UpdateDefaultAttendancePolicySalaryDeductionAmountAsync(new DateTime(2026, 6, 1), 300m);

			var refreshedPreview = await GetPayrollPreviewAsync(month, forceRefresh: false);
			var refreshedItem = refreshedPreview.Items.Single(x => x.EmployeeId == employee.Id);
			refreshedItem.DeductionComponents.Single(x => x.Name == "Late arrival deduction").Amount.ShouldBe(300d);
			refreshedItem.PayrollAdjustments.Single(x => x.Name == "Late arrival deduction").Amount.ShouldBe(300d);
			refreshedItem.ReviewStatus.ShouldBe(ReviewStatuses.Pending);
		}
		finally
		{
			await RestoreAttendancePolicyLateDeductionAsync(policySnapshot);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Register_Should_Refresh_Stale_Snapshot_When_Late_Arrival_Deduction_Is_Missing()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		const string month = "2026-06";
		var attendanceDate = new DateOnly(2026, 6, 12);
		var policySnapshot = await ConfigureDefaultAttendancePolicySalaryDeductionAsync(
			new DateTime(2026, 6, 1),
			250m,
			graceTime: 30);
		await EnsureActiveDefaultLeavePolicyAsync(new DateTime(2026, 6, 1));

		try
		{
			var employee = await EmployeeTestHelper.CreateEmployeeAsync(
				Client,
				$"PayrollLateStaleSnapshot{Guid.NewGuid():N}",
				dateOfJoining: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
			var shiftId = await InsertShiftAsync(
				$"Payroll late stale snapshot shift {Guid.NewGuid():N}",
				new TimeOnly(9, 0),
				new TimeOnly(17, 0));

			await AssignEmployeeShiftAsync(employee.Id, shiftId);
			await AssignSalaryStructureAsync(
				employee.Id,
				"PayrollLateStaleSnapshot",
				earningAmount: 30000m,
				deductionAmount: 0m,
				useMasterComponents: false);
			await MarkTimedAttendanceAsync(employee.Id, attendanceDate, "09:31");

			var firstPreview = await GetPayrollPreviewAsync(month, forceRefresh: true);
			var firstItem = firstPreview.Items.Single(x => x.EmployeeId == employee.Id);
			firstItem.DeductionComponents.Single(x => x.Name == "Late arrival deduction").Amount.ShouldBe(250d);

			await RemoveLateArrivalDeductionFromSnapshotAndAdjustmentsAsync(employee.Id, month, 250m);

			var refreshedPreview = await GetPayrollPreviewAsync(month, forceRefresh: false);
			var refreshedItem = refreshedPreview.Items.Single(x => x.EmployeeId == employee.Id);
			refreshedItem.DeductionComponents.Single(x => x.Name == "Late arrival deduction").Amount.ShouldBe(250d);
			refreshedItem.PayrollAdjustments.Single(x => x.Name == "Late arrival deduction").Amount.ShouldBe(250d);
			refreshedItem.IssueCodes.ShouldNotContain(IssueCodes.AttendanceSalaryDeductionConfiguration);
			await AssertSnapshotAdjustmentsMatchAsync(employee.Id, month);
		}
		finally
		{
			await RestoreAttendancePolicyLateDeductionAsync(policySnapshot);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Register_Should_Refresh_Unlocked_Snapshot_After_Policy_Day_Calculation_Changes()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"PayrollPolicyRefresh{Guid.NewGuid():N}",
			dateOfJoining: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

		await AssignSalaryStructureAsync(employee.Id, "PayrollPolicyRefresh");
		await SeedAttendanceOnDateAsync(employee.Id, new DateOnly(2026, 6, 4));

		var firstPreview = await GetAndReadAsync<RunPayrollResponse>(
			$"{BaseUrl}?month=2026-06&page=0&pageSize=100&forceRefresh=true");
		var firstItem = firstPreview.Items.Single(x => x.EmployeeId == employee.Id);
		firstItem.BasisDays.ShouldNotBe(30);
		firstItem.PayrollPolicyId.ShouldNotBeNull();

		var reviewResponse = await Client.PostAsJsonAsync(
			ReviewUrl,
			new PayrollRunReviewRequest
			{
				Month = "2026-06",
				EmployeeIds = new List<Guid> { employee.Id }
			},
			JsonOptions);
		var reviewBody = await reviewResponse.Content.ReadAsStringAsync();
		reviewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, reviewBody);

		await SetPayrollPolicyDayCalculationAsync(firstItem.PayrollPolicyId!.Value, DayCalculationType.TotalMonthDays);

		var refreshedPreview = await GetAndReadAsync<RunPayrollResponse>(
			$"{BaseUrl}?month=2026-06&page=0&pageSize=100&forceRefresh=false");
		var refreshedItem = refreshedPreview.Items.Single(x => x.EmployeeId == employee.Id);

		refreshedItem.BasisDays.ShouldBe(30);
		refreshedItem.TotalWorkingDays.ShouldBe(30);
		refreshedItem.DayCalculationMode.ShouldBe("total-month-days");
		refreshedItem.ReviewStatus.ShouldBe(ReviewStatuses.Pending);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Lock_Should_Reject_Reviewed_Snapshot_When_Adjustment_Changed()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"PayrollSalaryFreshness{Guid.NewGuid():N}",
			dateOfJoining: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

		await AssignSalaryStructureAsync(employee.Id, "SalaryFreshness", earningAmount: 25000m, useMasterComponents: false);
		await SeedAttendanceOnDateAsync(employee.Id, new DateOnly(2026, 6, 4));

		var firstPreview = await GetAndReadAsync<RunPayrollResponse>(
			$"{BaseUrl}?month=2026-06&page=0&pageSize=100&forceRefresh=true");
		var firstItem = firstPreview.Items.Single(x => x.EmployeeId == employee.Id);
		firstItem.ReviewStatus.ShouldBe(ReviewStatuses.Pending);

		var reviewResponse = await Client.PostAsJsonAsync(
			ReviewUrl,
			new PayrollRunReviewRequest
			{
				Month = "2026-06",
				EmployeeIds = new List<Guid> { employee.Id }
			},
			JsonOptions);
		var reviewBody = await reviewResponse.Content.ReadAsStringAsync();
		reviewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, reviewBody);
		await MarkPayrollSnapshotReviewedAsync(employee.Id, "2026-06");

		await CreatePayrollAdjustmentAsync(employee.Id, "2026-06", 500m);

		var lockResponse = await Client.PostAsJsonAsync(
			BaseUrl,
			new RunPayrollRequest
			{
				Month = "2026-06",
				EmployeeIds = new List<Guid> { employee.Id }
			},
			JsonOptions);
		var lockBody = await lockResponse.Content.ReadAsStringAsync();

		lockResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest, lockBody);
		lockBody.ShouldContain(HRErrorCodes.PayrollReviewRequiredBeforeLock);

		var refreshedPreview = await GetPayrollPreviewAsync("2026-06", forceRefresh: false);
		var refreshedItem = refreshedPreview.Items.Single(x => x.EmployeeId == employee.Id);
		refreshedItem.ReviewStatus.ShouldBe(
			ReviewStatuses.Pending,
			$"Lock body: {lockBody}; firstNetPay={firstItem.NetPay}; refreshedNetPay={refreshedItem.NetPay}");
		refreshedItem.NetPay.ShouldBe(firstItem.NetPay + 500d);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Adjustment_Create_Should_Reset_Reviewed_Snapshot_To_Pending()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"PayrollAdjustmentReviewReset{Guid.NewGuid():N}",
			dateOfJoining: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

		await AssignSalaryStructureAsync(employee.Id, "AdjustmentReviewReset", earningAmount: 25000m, useMasterComponents: false);
		await SeedAttendanceOnDateAsync(employee.Id, new DateOnly(2026, 6, 4));

		var firstPreview = await GetPayrollPreviewAsync("2026-06", forceRefresh: true);
		var firstItem = firstPreview.Items.Single(x => x.EmployeeId == employee.Id);

		var reviewResponse = await Client.PostAsJsonAsync(
			ReviewUrl,
			new PayrollRunReviewRequest
			{
				Month = "2026-06",
				EmployeeIds = new List<Guid> { employee.Id }
			},
			JsonOptions);
		var reviewBody = await reviewResponse.Content.ReadAsStringAsync();
		reviewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, reviewBody);
		await MarkPayrollSnapshotReviewedAsync(employee.Id, "2026-06");

		var adjustmentResponse = await Client.PostAsJsonAsync(
			$"{BaseUrl}/adjustments",
			new PayrollAdjustmentRequest
			{
				Month = "2026-06",
				EmployeeId = employee.Id,
				Name = "Review reset allowance",
				Amount = 750d,
				IsEarning = true,
				Reason = "Regression coverage"
			},
			JsonOptions);
		var adjustmentBody = await adjustmentResponse.Content.ReadAsStringAsync();
		adjustmentResponse.StatusCode.ShouldBe(HttpStatusCode.OK, adjustmentBody);

		await AssertPayrollReviewInvalidatedAsync(employee.Id, "2026-06");

		var lockResponse = await Client.PostAsJsonAsync(
			BaseUrl,
			new RunPayrollRequest
			{
				Month = "2026-06",
				EmployeeIds = new List<Guid> { employee.Id }
			},
			JsonOptions);
		var lockBody = await lockResponse.Content.ReadAsStringAsync();
		lockResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest, lockBody);
		lockBody.ShouldContain(HRErrorCodes.PayrollReviewRequiredBeforeLock);

		var refreshedPreview = await GetPayrollPreviewAsync("2026-06", forceRefresh: false);
		var refreshedItem = refreshedPreview.Items.Single(x => x.EmployeeId == employee.Id);
		refreshedItem.ReviewStatus.ShouldBe(ReviewStatuses.Pending);
		refreshedItem.NetPay.ShouldBe(firstItem.NetPay + 750d);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Register_Should_Use_Employee_Override_Effective_During_Month()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"PayrollMidMonthOverride{Guid.NewGuid():N}",
			dateOfJoining: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

		await AssignSalaryStructureAsync(employee.Id, "MidOverride");
		await SeedAttendanceOnDateAsync(employee.Id, new DateOnly(2026, 6, 4));
		var overridePayrollPolicyId = await AssignTotalMonthEmployeeOverrideAsync(
			employee.Id,
			new DateTime(2026, 6, 4));

		var preview = await GetAndReadAsync<RunPayrollResponse>(
			$"{BaseUrl}?month=2026-06&page=0&pageSize=100&forceRefresh=true");
		var item = preview.Items.Single(x => x.EmployeeId == employee.Id);

		item.PayrollPolicyId.ShouldBe(overridePayrollPolicyId);
		item.BasisDays.ShouldBe(30);
		item.TotalWorkingDays.ShouldBe(30);
		item.DayCalculationMode.ShouldBe("total-month-days");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Readiness_Should_Count_Absent_Attendance_As_Found()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"PayrollAbsentAttendance{Guid.NewGuid():N}",
			dateOfJoining: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));

		var attendanceResponse = await Client.PostAsJsonAsync(
			"/api/hr/attendance/take-attendance/day/save",
			new TakeAttendanceSaveInput
			{
				AttendanceDate = "15-04-2026",
				Entries =
				[
					new TakeAttendanceSaveEntryDto
					{
						EmployeeId = employee.Id,
						StatusWire = "ABSENT",
						Remarks = "Payroll absent attendance is still marked"
					}
				]
			},
			JsonOptions);
		attendanceResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		await AssertAttendanceStagePersistedAsync(employee.Id, new DateOnly(2026, 4, 15));

		var preview = await GetAndReadAsync<RunPayrollResponse>(
			$"{BaseUrl}?month=2026-04&page=0&pageSize=100&forceRefresh=true");
		var item = preview.Items.Single(x => x.EmployeeId == employee.Id);

		item.PayableDays.ShouldBe(0);
		item.IssueCodes.ShouldNotContain("NO_ATTENDANCE");

		var readiness = await GetAndReadAsync<RunPayrollReadinessSummary>(
			$"{BaseUrl}/readiness?month=2026-04");
		readiness.AttendanceFoundCount.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Lock_Should_Reject_Employee_Before_Joining_Month()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			"PayrollLockMayJoin",
			dateOfJoining: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

		var response = await Client.PostAsJsonAsync(
			BaseUrl,
			new RunPayrollRequest
			{
				Month = "2026-04",
				EmployeeIds = new List<Guid> { employee.Id }
			},
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Register_Should_Remove_Unlocked_Stale_Rows_For_Ineligible_Employees()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			"PayrollStaleMayJoin",
			dateOfJoining: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));
		await SeedStalePayrollRowsAsync(employee.Id, "2026-04");

		var aprilPreview = await GetAndReadAsync<RunPayrollResponse>(
			$"{BaseUrl}?month=2026-04&page=0&pageSize=100&forceRefresh=true");

		aprilPreview.Items.Any(x => x.EmployeeId == employee.Id).ShouldBeFalse();
		await AssertNoStalePayrollRowsAsync(employee.Id, "2026-04");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Payroll_Readiness_Without_Generating_Preview()
	{
		var response = await Client.GetAsync($"{BaseUrl}/readiness?month=2026-05");

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.OK,
			HttpStatusCode.BadRequest,
			HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Preview_Should_Report_Policy_Setup_Issue_When_Month_Has_No_Academic_Year()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"PayrollNoAcademicYear{Guid.NewGuid():N}",
			dateOfJoining: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

		var previewResponse = await Client.GetAsync($"{BaseUrl}?month=2040-05&page=0&pageSize=100&forceRefresh=true");
		var previewBody = await previewResponse.Content.ReadAsStringAsync();
		previewBody.ShouldNotContain("HR:Payroll:030011");
		previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, previewBody);

		var preview = JsonSerializer.Deserialize<RunPayrollResponse>(previewBody, JsonOptions)!;
		preview.Readiness.CanLock.ShouldBeFalse();
		preview.Readiness.PolicyConfigurationIssueCount.ShouldBeGreaterThan(0);
		preview.Readiness.BlockingIssues.ShouldContain("PAYROLL_POLICY_CONFIGURATION");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Readiness_Should_Report_Policy_Setup_Issue_When_Default_Payroll_Policy_Is_Missing()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"PayrollMissingDefaultPolicy{Guid.NewGuid():N}",
			dateOfJoining: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

		var initialReadiness = await GetAndReadAsync<RunPayrollReadinessSummary>(
			$"{BaseUrl}/readiness?month=2026-05");
		initialReadiness.SnapshotCount.ShouldBeGreaterThan(0);

		var effectiveDate = new DateTime(2026, 5, 1);
		var disabledAssignmentIds = await DisablePolicyAssignmentsForMonthAsync(effectiveDate);
		var defaultPayrollPolicyId = await ClearDefaultPayrollPolicyForMonthAsync(effectiveDate);
		try
		{
			var response = await Client.GetAsync($"{BaseUrl}/readiness?month=2026-05");
			var body = await response.Content.ReadAsStringAsync();
			response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

			var readiness = JsonSerializer.Deserialize<RunPayrollReadinessSummary>(body, JsonOptions)!;
			readiness.CanLock.ShouldBeFalse();
			readiness.PolicyConfigurationIssueCount.ShouldBeGreaterThan(0);
			readiness.BlockingIssues.ShouldContain("PAYROLL_POLICY_CONFIGURATION");
			readiness.RecommendedActions.ShouldContain("fix-payroll-policy");

			var previewResponse = await Client.GetAsync($"{BaseUrl}?month=2026-05&page=0&pageSize=100&forceRefresh=false");
			var previewBody = await previewResponse.Content.ReadAsStringAsync();
			previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, previewBody);

			var preview = JsonSerializer.Deserialize<RunPayrollResponse>(previewBody, JsonOptions)!;
			preview.Readiness.CanLock.ShouldBeFalse();
			preview.Readiness.PolicyConfigurationIssueCount.ShouldBeGreaterThan(0);
			preview.Readiness.BlockingIssues.ShouldContain("PAYROLL_POLICY_CONFIGURATION");
		}
		finally
		{
			await RestoreDefaultPayrollPolicyAsync(defaultPayrollPolicyId);
			await RestorePolicyAssignmentsAsync(disabledAssignmentIds);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Register_Should_Report_Setup_Issue_When_Approved_Leave_Employee_Lacks_Payroll_Policy()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		const string month = "2026-05";
		var effectiveDate = new DateTime(2026, 5, 1);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"PayrollSetupLeave{Guid.NewGuid():N}",
			dateOfJoining: effectiveDate);
		var leavePolicyId = await EnsureActiveDefaultLeavePolicyAsync(effectiveDate);
		await SeedApprovedPaidLeaveAsync(
			employee.Id,
			leavePolicyId,
			new DateTime(2026, 5, 11),
			approvedUnits: 1m);

		var disabledAssignmentIds = await DisablePolicyAssignmentsForMonthAsync(effectiveDate);
		var defaultPayrollPolicyId = await ClearDefaultPayrollPolicyForMonthAsync(effectiveDate);
		try
		{
			var response = await Client.GetAsync($"{BaseUrl}?month={month}&page=0&pageSize=100&forceRefresh=true");
			var body = await response.Content.ReadAsStringAsync();
			response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

			var preview = JsonSerializer.Deserialize<RunPayrollResponse>(body, JsonOptions)!;
			var item = preview.Items.Single(x => x.EmployeeId == employee.Id);
			item.IssueCodes.ShouldContain("PAYROLL_POLICY_CONFIGURATION");
			preview.Readiness.PolicyConfigurationIssueCount.ShouldBeGreaterThan(0);
		}
		finally
		{
			await RestoreDefaultPayrollPolicyAsync(defaultPayrollPolicyId);
			await RestorePolicyAssignmentsAsync(disabledAssignmentIds);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Lock_Payroll_Without_500()
	{
		var response = await Client.PostAsJsonAsync(
			BaseUrl,
			new RunPayrollRequest { Month = PayrollMonth },
			JsonOptions);

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.OK,
			HttpStatusCode.BadRequest,
			HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Mark_Review_Without_500()
	{
		var response = await Client.PostAsJsonAsync(
			ReviewUrl,
			new PayrollRunReviewRequest
			{
				Month = PayrollMonth,
				EmployeeIds = new List<System.Guid> { System.Guid.NewGuid() }
			},
			JsonOptions);

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.OK,
			HttpStatusCode.BadRequest,
			HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Unmark_Review_Without_500()
	{
		var response = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, ReviewUrl)
		{
			Content = JsonContent.Create(
				new PayrollRunReviewRequest
				{
					Month = PayrollMonth,
					EmployeeIds = new List<System.Guid> { System.Guid.NewGuid() }
				},
				options: JsonOptions)
		});

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.OK,
			HttpStatusCode.BadRequest,
			HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Error_For_Invalid_Month_Format()
	{
		var response = await Client.GetAsync($"{BaseUrl}?month=invalid-format&page=0&pageSize=50");
		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.BadRequest,
			HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task HrSwagger_Should_Contain_RunPayroll_And_AutomatedRunLock_Endpoints()
	{
		var response = await Client.GetAsync("/swagger/v1-hr/swagger.json");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var json = await response.Content.ReadAsStringAsync();
		using var document = JsonDocument.Parse(json);
		document.RootElement.GetProperty("paths").TryGetProperty("/api/payroll/run", out _).ShouldBeTrue();
		document.RootElement.GetProperty("paths").TryGetProperty("/api/payroll/run/readiness", out _).ShouldBeTrue();
		document.RootElement.GetProperty("paths").TryGetProperty("/api/payroll/run/reviews", out _).ShouldBeTrue();
		document.RootElement.GetProperty("paths").TryGetProperty("/api/hr/attendance/automated-runs/{batchId}/lock", out _).ShouldBeTrue();
	}

	private async Task SeedStalePayrollRowsAsync(Guid employeeId, string monthYear)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var snapshotRepository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>();
		var reviewRepository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollRunReview, Guid>>();
		var adjustmentRepository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollAdjustment, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var reviewerId = Guid.Parse(TestAuthHandler.TestUserId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var snapshot = new PayrollRunItemSnapshot(Guid.NewGuid(), tenantId, monthYear, employeeId)
			{
				EmployeeName = "Stale Payroll Employee",
				EmployeeIdentifier = "STALE",
				ReviewStatus = "REVIEWED"
			};
			await snapshotRepository.InsertAsync(snapshot, autoSave: false);
			await reviewRepository.InsertAsync(
				new PayrollRunReview(Guid.NewGuid(), tenantId, monthYear, employeeId, reviewerId, DateTime.UtcNow),
				autoSave: false);
			await adjustmentRepository.InsertAsync(
				new PayrollAdjustment(Guid.NewGuid(), tenantId, monthYear, employeeId, "Stale adjustment", 100m, true, "Regression setup"),
				autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private async Task CorruptPayrollSnapshotBasisAsync(Guid employeeId, string monthYear)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var snapshotRepository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var query = await snapshotRepository.GetQueryableAsync();
			var snapshot = query.Single(x => x.MonthYear == monthYear && x.EmployeeId == employeeId);
			snapshot.BasisDays = 0m;
			snapshot.TotalWorkingDays = 0m;
			snapshot.PayableDays = 0m;
			snapshot.PayableUnits = 0m;
			await snapshotRepository.UpdateAsync(snapshot, autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private async Task CreatePayrollAdjustmentAsync(Guid employeeId, string monthYear, decimal amount)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var adjustmentRepository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollAdjustment, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			await adjustmentRepository.InsertAsync(
				new PayrollAdjustment(Guid.NewGuid(), tenantId, monthYear, employeeId, "Freshness adjustment", amount, true, "Regression setup"),
				autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private async Task<RunPayrollResponse> GetPayrollPreviewAsync(string month, bool forceRefresh)
	{
		var response = await Client.GetAsync(
			$"{BaseUrl}?month={month}&page=0&pageSize=100&forceRefresh={forceRefresh.ToString().ToLowerInvariant()}");
		var body = await response.Content.ReadAsStringAsync();
		response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
		var preview = await response.Content.ReadFromJsonAsync<RunPayrollResponse>(JsonOptions);
		preview.ShouldNotBeNull();
		return preview;
	}

	private async Task<SalarySlipResponse> GetSalarySlipResponseAsync(Guid employeeId, string monthYear)
	{
		var response = await Client.GetAsync(
			$"/api/payroll/salary-slips?monthYear={Uri.EscapeDataString(monthYear)}&employeeId={employeeId}");
		var body = await response.Content.ReadAsStringAsync();
		response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
		var salarySlip = await response.Content.ReadFromJsonAsync<SalarySlipResponse>(JsonOptions);
		salarySlip.ShouldNotBeNull();
		return salarySlip;
	}

	private async Task ReviewAndLockPayrollAsync(Guid employeeId, string month)
	{
		var reviewResponse = await Client.PostAsJsonAsync(
			ReviewUrl,
			new PayrollRunReviewRequest
			{
				Month = month,
				EmployeeIds = new List<Guid> { employeeId }
			},
			JsonOptions);
		var reviewBody = await reviewResponse.Content.ReadAsStringAsync();
		reviewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, reviewBody);
		await MarkPayrollSnapshotReviewedAsync(employeeId, month);
		await AssertSnapshotAdjustmentsMatchAsync(employeeId, month);

		var lockResponse = await Client.PostAsJsonAsync(
			BaseUrl,
			new RunPayrollRequest
			{
				Month = month,
				EmployeeIds = new List<Guid> { employeeId }
			},
			JsonOptions);
		var lockBody = await lockResponse.Content.ReadAsStringAsync();
		lockResponse.StatusCode.ShouldBe(HttpStatusCode.OK, lockBody);
	}

	private async Task AssertSnapshotAdjustmentsMatchAsync(Guid employeeId, string monthYear)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var snapshotRepository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>();
		var adjustmentRepository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollAdjustment, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var snapshotQuery = await snapshotRepository.GetQueryableAsync();
			var snapshot = snapshotQuery.Single(x => x.MonthYear == monthYear && x.EmployeeId == employeeId);
			var snapshotFingerprints = (JsonSerializer.Deserialize<List<PayrollAdjustmentDto>>(snapshot.PayrollAdjustmentsJson)
				?? new List<PayrollAdjustmentDto>())
				.Select(x => new PayrollAdjustmentFingerprint(
					x.Id,
					x.MonthYear,
					x.EmployeeId,
					x.Name,
					(decimal)x.Amount,
					x.IsEarning,
					x.Reason,
					x.Description,
					x.SourceType,
					x.SourceId,
					x.IsSystemGenerated))
				.OrderBy(x => x.Id)
				.ToList();

			var adjustmentQuery = await adjustmentRepository.GetQueryableAsync();
			var currentAdjustments = adjustmentQuery
				.Where(x => x.MonthYear == monthYear && x.EmployeeId == employeeId)
				.OrderBy(x => x.CreationTime)
				.ToList();
			var currentFingerprints = currentAdjustments
				.Select(x => new PayrollAdjustmentFingerprint(
					x.Id,
					x.MonthYear,
					x.EmployeeId,
					x.Name,
					x.Amount,
					x.IsEarning,
					x.Reason,
					x.Description,
					x.SourceType,
					x.SourceId,
					x.IsSystemGenerated))
				.OrderBy(x => x.Id)
				.ToList();

			currentFingerprints.ShouldBe(snapshotFingerprints);
			await uow.CompleteAsync();
		}
	}

	private async Task RemoveLateArrivalDeductionFromSnapshotAndAdjustmentsAsync(
		Guid employeeId,
		string monthYear,
		decimal amount)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var snapshotRepository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>();
		var adjustmentRepository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollAdjustment, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var snapshotQuery = await snapshotRepository.GetQueryableAsync();
			var snapshot = snapshotQuery.Single(x => x.MonthYear == monthYear && x.EmployeeId == employeeId);
			var snapshotAdjustments = JsonSerializer.Deserialize<List<PayrollAdjustmentDto>>(snapshot.PayrollAdjustmentsJson)
				?? new List<PayrollAdjustmentDto>();
			snapshot.PayrollAdjustmentsJson = JsonSerializer.Serialize(
				snapshotAdjustments
					.Where(x => x.SourceType != PayrollAdjustmentSourceTypes.LateArrivalSalaryDeduction)
					.ToList());
			var deductionComponents = JsonSerializer.Deserialize<List<RunPayrollComponentItem>>(snapshot.DeductionComponentsJson)
				?? new List<RunPayrollComponentItem>();
			snapshot.DeductionComponentsJson = JsonSerializer.Serialize(
				deductionComponents
					.Where(x => x.Name != "Late arrival deduction")
					.ToList());
			snapshot.TotalDeduction -= amount;
			snapshot.NetPay += amount;
			if (snapshot.NetPayDelta.HasValue)
			{
				snapshot.NetPayDelta += amount;
			}
			await snapshotRepository.UpdateAsync(snapshot, autoSave: false);

			var adjustmentQuery = await adjustmentRepository.GetQueryableAsync();
			var adjustments = adjustmentQuery
				.Where(x =>
					x.MonthYear == monthYear &&
					x.EmployeeId == employeeId &&
					x.SourceType == PayrollAdjustmentSourceTypes.LateArrivalSalaryDeduction)
				.ToList();
			foreach (var adjustment in adjustments)
			{
				await adjustmentRepository.DeleteAsync(adjustment, autoSave: false);
			}

			await uow.CompleteAsync();
		}
	}

	private readonly record struct PayrollAdjustmentFingerprint(
		Guid Id,
		string MonthYear,
		Guid EmployeeId,
		string Name,
		decimal Amount,
		bool IsEarning,
		string Reason,
		string? Description,
		string? SourceType,
		Guid? SourceId,
		bool IsSystemGenerated);

	private async Task MarkPayrollSnapshotReviewedAsync(Guid employeeId, string monthYear)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var snapshotRepository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var reviewerId = Guid.Parse(TestAuthHandler.TestUserId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var query = await snapshotRepository.GetQueryableAsync();
			var snapshot = query.Single(x => x.MonthYear == monthYear && x.EmployeeId == employeeId);
			snapshot.MarkReviewed(reviewerId, DateTime.UtcNow);
			await snapshotRepository.UpdateAsync(snapshot, autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private async Task AssertPayrollReviewInvalidatedAsync(Guid employeeId, string monthYear)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var snapshotRepository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>();
		var reviewRepository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollRunReview, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var snapshotQuery = await snapshotRepository.GetQueryableAsync();
			var snapshot = snapshotQuery.Single(x => x.MonthYear == monthYear && x.EmployeeId == employeeId);
			snapshot.ReviewStatus.ShouldBe(ReviewStatuses.Pending);
			snapshot.ReviewedByUserId.ShouldBeNull();
			snapshot.ReviewedAt.ShouldBeNull();

			var reviewQuery = await reviewRepository.GetQueryableAsync();
			reviewQuery.Any(x => x.MonthYear == monthYear && x.EmployeeId == employeeId).ShouldBeFalse();
			await uow.CompleteAsync();
		}
	}

	private async Task SetEmployeeStatusAsync(Guid employeeId, int status, DateOnly effectiveDate)
	{
		var response = await Client.PutAsJsonAsync(
			$"/api/employees/{employeeId}/status",
			new EmployeeStatusUpdateRequest
			{
				EmployeeStatusType = status,
				Remarks = "Payroll eligibility regression",
				EffectiveDate = effectiveDate
			},
			JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	private async Task AssignSalaryStructureAsync(
		Guid employeeId,
		string suffix,
		decimal earningAmount = 25000m,
		decimal deductionAmount = 1000m,
		bool useMasterComponents = true,
		Guid? pfRuleId = null)
	{
		var detailsResponse = await Client.PutAsJsonAsync(
			$"/api/employees/{employeeId}/payroll",
			new PayrollContract
			{
				BankDetails = new BankDetailsContract
				{
					BankName = "State Bank of India",
					BranchName = "Main Branch",
					AccountHolderName = "Payroll Test Employee",
					AccountNumber = $"9{Math.Abs(employeeId.GetHashCode()):D9}",
					IfscCode = "SBIN0001234"
				},
				SalaryDetails = new SalaryDetailsContract
				{
					PfRuleId = pfRuleId,
					TotalWorkingHourPerMonth = 160m,
					PunchId = suffix.Length > 20 ? suffix[..20] : suffix
				}
			},
			JsonOptions);
		var detailsBody = await detailsResponse.Content.ReadAsStringAsync();
		detailsResponse.StatusCode.ShouldBe(HttpStatusCode.OK, detailsBody);

		var earningComponent = useMasterComponents
			? await PayrollTestHelper.CreateEarningComponentAsync(Client, $"{suffix}Earn")
			: null;
		var deductionComponent = useMasterComponents
			? await PayrollTestHelper.CreateDeductionComponentAsync(Client, $"{suffix}Ded")
			: null;
		var response = await Client.PutAsJsonAsync(
			$"/api/payroll/employees/{employeeId}/salary-structure",
			new UpdateSalaryStructureDto
			{
				EarningComponents =
				[
					new SalaryComponentDto
					{
						ComponentId = earningComponent?.Id,
						Name = earningComponent?.Name ?? $"{suffix}CustomEarn",
						Amount = earningAmount,
						IsFixed = true,
						IsTemp = false,
						ApplicableMonth = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)
					}
				],
				DeductionComponents =
				[
					new SalaryComponentDto
					{
						ComponentId = deductionComponent?.Id,
						Name = deductionComponent?.Name ?? $"{suffix}CustomDed",
						Amount = deductionAmount,
						IsFixed = true,
						IsTemp = false,
						ApplicableMonth = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)
					}
				]
			},
			JsonOptions);

		var body = await response.Content.ReadAsStringAsync();
		response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
	}

	private async Task MarkAttendanceAsync(Guid employeeId, string attendanceDate)
	{
		var response = await Client.PostAsJsonAsync(
			"/api/hr/attendance/take-attendance/day/save",
			new TakeAttendanceSaveInput
			{
				AttendanceDate = attendanceDate,
				Entries =
				[
					new TakeAttendanceSaveEntryDto
					{
						EmployeeId = employeeId,
						StatusWire = "PRESENT",
						InTime = "09:00",
						OutTime = "17:00",
						Remarks = "Payroll lock setup"
					}
				]
			},
			JsonOptions);

		var body = await response.Content.ReadAsStringAsync();
		response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
	}

	private async Task MarkTimedAttendanceAsync(
		Guid employeeId,
		DateOnly attendanceDate,
		string inTime,
		string outTime = "17:00")
	{
		var response = await Client.PostAsJsonAsync(
			"/api/hr/attendance/take-attendance/day/save",
			new TakeAttendanceSaveInput
			{
				AttendanceDate = attendanceDate.ToString("dd-MM-yyyy"),
				Entries =
				[
					new TakeAttendanceSaveEntryDto
					{
						EmployeeId = employeeId,
						StatusWire = "PRESENT",
						InTime = inTime,
						OutTime = outTime,
						Remarks = "Payroll late deduction regression"
					}
				]
			},
			JsonOptions);

		var body = await response.Content.ReadAsStringAsync();
		response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
	}

	private async Task SeedTimedAttendanceOnDateAsync(
		Guid employeeId,
		DateOnly attendanceDate,
		string inTime,
		string outTime = "17:00")
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var attendanceRepository = scope.ServiceProvider.GetRequiredService<IRepository<EmployeeAttendance, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var timeIn = TimeOnly.Parse(inTime);
		var timeOut = TimeOnly.Parse(outTime);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			await attendanceRepository.InsertAsync(new EmployeeAttendance
			{
				TenantId = tenantId,
				EmployeeId = employeeId,
				AttendanceDate = attendanceDate,
				EducationMediumStageId = EmployeeTestHelper.DefaultMediumStageId,
				StatusType = EmployeeAttendanceStatusType.Present,
				TimeIn = new DateTimeOffset(attendanceDate.ToDateTime(timeIn), TimeSpan.Zero),
				TimeOut = new DateTimeOffset(attendanceDate.ToDateTime(timeOut), TimeSpan.Zero),
				Remarks = "Payroll late deduction legacy setup regression"
			}, autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private async Task<AttendancePolicyOvertimeSnapshot> ConfigureDefaultAttendancePolicyOvertimeAsync(DateTime monthDate)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var academicYearRepository = scope.ServiceProvider.GetRequiredService<IAcademicYearRepository>();
		var attendancePolicyRepository = scope.ServiceProvider.GetRequiredService<IRepository<AttendancePolicy, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var academicYearQuery = await academicYearRepository.GetQueryableAsync();
			var academicYear = academicYearQuery.Single(x =>
				x.StartDate <= monthDate.Date &&
				x.EndDate >= monthDate.Date);
			var policyQuery = await attendancePolicyRepository.GetQueryableAsync();
			var policy = policyQuery.Single(x =>
				x.AcademicYearId == academicYear.Id &&
				x.IsDefault &&
				x.Status == HrPolicyStatus.Active);
			var snapshot = new AttendancePolicyOvertimeSnapshot(
				policy.Id,
				policy.OvertimeEnabled,
				policy.OvertimeRate,
				policy.MinOvertimeMinutes,
				policy.PenaltyDeductionAmount);

			policy.Update(
				policy.WorkCalendarPolicyId,
				policy.Name,
				policy.Status,
				policy.IsDefault,
				policy.CaptureMode,
				policy.FullDayThresholdPercentage,
				policy.HalfDayThresholdPercentage,
				policy.AbsentThresholdPercentage,
				policy.MissingPunchBehavior,
				policy.ManualCorrectionAllowed,
				policy.GraceTime,
				policy.MaxLateAllowedPerMonth,
				policy.PenaltyType,
				policy.PenaltyCountThreshold,
				policy.EarlyExitEnabled,
				policy.EarlyExitGraceTime,
				overtimeEnabled: true,
				overtimeRate: AttendanceOvertimeRate.TwoX,
				minOvertimeMinutes: 30,
				holidayWorkBehavior: policy.HolidayWorkBehavior,
				payrollPreviewEnabled: policy.PayrollPreviewEnabled,
				penaltyDeductionAmount: policy.PenaltyDeductionAmount);
			await attendancePolicyRepository.UpdateAsync(policy, autoSave: false);
			await uow.CompleteAsync();
			return snapshot;
		}
	}

	private async Task<AttendancePolicyLateDeductionSnapshot> ConfigureDefaultAttendancePolicySalaryDeductionAsync(
		DateTime monthDate,
		decimal penaltyDeductionAmount,
		int graceTime = 0,
		int maxLateAllowedPerMonth = 0,
		int penaltyCountThreshold = 1)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var academicYearRepository = scope.ServiceProvider.GetRequiredService<IAcademicYearRepository>();
		var attendancePolicyRepository = scope.ServiceProvider.GetRequiredService<IRepository<AttendancePolicy, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var academicYearQuery = await academicYearRepository.GetQueryableAsync();
			var academicYear = academicYearQuery.Single(x =>
				x.StartDate <= monthDate.Date &&
				x.EndDate >= monthDate.Date);
			var policyQuery = await attendancePolicyRepository.GetQueryableAsync();
			var policy = policyQuery.Single(x =>
				x.AcademicYearId == academicYear.Id &&
				x.IsDefault &&
				x.Status == HrPolicyStatus.Active);
			var snapshot = new AttendancePolicyLateDeductionSnapshot(
				policy.Id,
				policy.GraceTime,
				policy.MaxLateAllowedPerMonth,
				policy.PenaltyType,
				policy.PenaltyCountThreshold,
				policy.PenaltyDeductionAmount);

			policy.Update(
				policy.WorkCalendarPolicyId,
				policy.Name,
				policy.Status,
				policy.IsDefault,
				policy.CaptureMode,
				policy.FullDayThresholdPercentage,
				policy.HalfDayThresholdPercentage,
				policy.AbsentThresholdPercentage,
				policy.MissingPunchBehavior,
				policy.ManualCorrectionAllowed,
				graceTime,
				maxLateAllowedPerMonth,
				AttendancePenaltyType.SalaryDeduction,
				penaltyCountThreshold,
				policy.EarlyExitEnabled,
				policy.EarlyExitGraceTime,
				policy.OvertimeEnabled,
				policy.OvertimeRate,
				policy.MinOvertimeMinutes,
				policy.HolidayWorkBehavior,
				policy.PayrollPreviewEnabled,
				penaltyDeductionAmount);
			await attendancePolicyRepository.UpdateAsync(policy, autoSave: false);
			await uow.CompleteAsync();
			return snapshot;
		}
	}

	private async Task UpdateDefaultAttendancePolicySalaryDeductionAmountAsync(
		DateTime monthDate,
		decimal penaltyDeductionAmount)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var academicYearRepository = scope.ServiceProvider.GetRequiredService<IAcademicYearRepository>();
		var attendancePolicyRepository = scope.ServiceProvider.GetRequiredService<IRepository<AttendancePolicy, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var academicYearQuery = await academicYearRepository.GetQueryableAsync();
			var academicYear = academicYearQuery.Single(x =>
				x.StartDate <= monthDate.Date &&
				x.EndDate >= monthDate.Date);
			var policyQuery = await attendancePolicyRepository.GetQueryableAsync();
			var policy = policyQuery.Single(x =>
				x.AcademicYearId == academicYear.Id &&
				x.IsDefault &&
				x.Status == HrPolicyStatus.Active);

			policy.Update(
				policy.WorkCalendarPolicyId,
				policy.Name,
				policy.Status,
				policy.IsDefault,
				policy.CaptureMode,
				policy.FullDayThresholdPercentage,
				policy.HalfDayThresholdPercentage,
				policy.AbsentThresholdPercentage,
				policy.MissingPunchBehavior,
				policy.ManualCorrectionAllowed,
				policy.GraceTime,
				policy.MaxLateAllowedPerMonth,
				AttendancePenaltyType.SalaryDeduction,
				policy.PenaltyCountThreshold,
				policy.EarlyExitEnabled,
				policy.EarlyExitGraceTime,
				policy.OvertimeEnabled,
				policy.OvertimeRate,
				policy.MinOvertimeMinutes,
				policy.HolidayWorkBehavior,
				policy.PayrollPreviewEnabled,
				penaltyDeductionAmount);
			await attendancePolicyRepository.UpdateAsync(policy, autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private async Task ForceDefaultAttendancePolicyPenaltyDeductionAmountAsync(
		DateTime monthDate,
		decimal? penaltyDeductionAmount)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var academicYearRepository = scope.ServiceProvider.GetRequiredService<IAcademicYearRepository>();
		var attendancePolicyRepository = scope.ServiceProvider.GetRequiredService<IRepository<AttendancePolicy, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var academicYearQuery = await academicYearRepository.GetQueryableAsync();
			var academicYear = academicYearQuery.Single(x =>
				x.StartDate <= monthDate.Date &&
				x.EndDate >= monthDate.Date);
			var policyQuery = await attendancePolicyRepository.GetQueryableAsync();
			var policy = policyQuery.Single(x =>
				x.AcademicYearId == academicYear.Id &&
				x.IsDefault &&
				x.Status == HrPolicyStatus.Active);

			var setter = typeof(AttendancePolicy)
				.GetProperty(nameof(AttendancePolicy.PenaltyDeductionAmount))!
				.GetSetMethod(nonPublic: true)!;
			setter.Invoke(policy, new object?[] { penaltyDeductionAmount });
			await attendancePolicyRepository.UpdateAsync(policy, autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private async Task<Guid> EnsureActiveDefaultLeavePolicyAsync(DateTime monthDate)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var academicYearRepository = scope.ServiceProvider.GetRequiredService<IAcademicYearRepository>();
		var leavePolicyRepository = scope.ServiceProvider.GetRequiredService<IRepository<LeavePolicy, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var academicYearQuery = await academicYearRepository.GetQueryableAsync();
			var academicYear = academicYearQuery.Single(x =>
				x.StartDate <= monthDate.Date &&
				x.EndDate >= monthDate.Date);
			var policyQuery = await leavePolicyRepository.GetQueryableAsync();
			var existing = policyQuery.FirstOrDefault(x =>
				x.AcademicYearId == academicYear.Id &&
				x.IsActive &&
				x.IsDefault &&
				x.Status == HrPolicyStatus.Active);
			if (existing != null)
			{
				return existing.Id;
			}

			var suffix = Guid.NewGuid().ToString("N")[..12];
			var leavePolicy = new LeavePolicy(
				Guid.NewGuid(),
				tenantId,
				academicYear.Id,
				$"OT Policy {suffix}",
				academicYear.StartDate,
				academicYear.EndDate,
				HrPolicyStatus.Active,
				isDefault: true);
			var leavePolicyRule = new LeavePolicyRule(
				Guid.NewGuid(),
				tenantId,
				leavePolicy.Id,
				LeavePaidType.Paid,
				LeaveEntitlementMode.Limited,
				LeaveAccrualPolicyType.Yearly,
				12,
				carryForward: false,
				carryForwardLimit: null,
				encashable: false,
				encashmentTiming: null,
				requiresApproval: true,
				minNoticeDays: 0,
				maxDaysPerRequest: 30,
				allowHalfDay: true,
				attachmentRequiredAfterDays: null,
				allowNegativeBalance: false);
			var unpaidRule = new LeavePolicyRule(
				Guid.NewGuid(),
				tenantId,
				leavePolicy.Id,
				LeavePaidType.Unpaid,
				LeaveEntitlementMode.Unlimited,
				LeaveAccrualPolicyType.Yearly,
				null,
				carryForward: false,
				carryForwardLimit: null,
				encashable: false,
				encashmentTiming: null,
				requiresApproval: true,
				minNoticeDays: 0,
				maxDaysPerRequest: 30,
				allowHalfDay: true,
				attachmentRequiredAfterDays: null,
				allowNegativeBalance: true);
			leavePolicy.ReplaceRules([leavePolicyRule, unpaidRule]);
			await leavePolicyRepository.InsertAsync(leavePolicy, autoSave: false);
			await uow.CompleteAsync();
			return leavePolicy.Id;
		}
	}

	private async Task SeedApprovedPaidLeaveAsync(
		Guid employeeId,
		Guid leavePolicyId,
		DateTime leaveDate,
		decimal approvedUnits)
		=> await SeedApprovedLeaveAsync(
			employeeId,
			leavePolicyId,
			leaveDate,
			LeavePaidType.Paid,
			approvedUnits);

	private async Task SeedApprovedLeaveAsync(
		Guid employeeId,
		Guid leavePolicyId,
		DateTime leaveDate,
		LeavePaidType paidType,
		decimal approvedUnits)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var leavePolicyRepository = scope.ServiceProvider.GetRequiredService<ILeavePolicyRepository>();
		var leaveRepository = scope.ServiceProvider.GetRequiredService<IEmployeeLeaveRepository>();
		var leaveManager = scope.ServiceProvider.GetRequiredService<EmployeeLeaveManager>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var leavePolicy = await leavePolicyRepository.GetWithRulesAsync(leavePolicyId)
				?? throw new InvalidOperationException("Leave policy not found for payroll regression setup.");
			var leaveRule = leavePolicy.Rules.Single(x => x.PaidType == paidType);
			var leave = EmployeeLeave.Create(
				Guid.NewGuid(),
				tenantId,
				employeeId,
				leavePolicy.Id,
				leaveRule.Id,
				paidType,
				new DateTimeOffset(leaveDate.Date),
				new DateTimeOffset(leaveDate.Date),
				"Approved leave for payroll setup regression");

			await leaveRepository.InsertAsync(leave, autoSave: false);
			await leaveManager.ApproveAsync(leave, paidType, approvedUnits);
			await uow.CompleteAsync();
		}
	}

	private async Task RestoreAttendancePolicyOvertimeAsync(AttendancePolicyOvertimeSnapshot snapshot)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var attendancePolicyRepository = scope.ServiceProvider.GetRequiredService<IRepository<AttendancePolicy, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var policy = await attendancePolicyRepository.GetAsync(snapshot.PolicyId);
			policy.Update(
				policy.WorkCalendarPolicyId,
				policy.Name,
				policy.Status,
				policy.IsDefault,
				policy.CaptureMode,
				policy.FullDayThresholdPercentage,
				policy.HalfDayThresholdPercentage,
				policy.AbsentThresholdPercentage,
				policy.MissingPunchBehavior,
				policy.ManualCorrectionAllowed,
				policy.GraceTime,
				policy.MaxLateAllowedPerMonth,
				policy.PenaltyType,
				policy.PenaltyCountThreshold,
				policy.EarlyExitEnabled,
				policy.EarlyExitGraceTime,
				snapshot.OvertimeEnabled,
				snapshot.OvertimeRate,
				snapshot.MinOvertimeMinutes,
				policy.HolidayWorkBehavior,
				policy.PayrollPreviewEnabled,
				snapshot.PenaltyDeductionAmount);
			await attendancePolicyRepository.UpdateAsync(policy, autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private async Task RestoreAttendancePolicyLateDeductionAsync(AttendancePolicyLateDeductionSnapshot snapshot)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var attendancePolicyRepository = scope.ServiceProvider.GetRequiredService<IRepository<AttendancePolicy, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var policy = await attendancePolicyRepository.GetAsync(snapshot.PolicyId);
			policy.Update(
				policy.WorkCalendarPolicyId,
				policy.Name,
				policy.Status,
				policy.IsDefault,
				policy.CaptureMode,
				policy.FullDayThresholdPercentage,
				policy.HalfDayThresholdPercentage,
				policy.AbsentThresholdPercentage,
				policy.MissingPunchBehavior,
				policy.ManualCorrectionAllowed,
				snapshot.GraceTime,
				snapshot.MaxLateAllowedPerMonth,
				snapshot.PenaltyType,
				snapshot.PenaltyCountThreshold,
				policy.EarlyExitEnabled,
				policy.EarlyExitGraceTime,
				policy.OvertimeEnabled,
				policy.OvertimeRate,
				policy.MinOvertimeMinutes,
				policy.HolidayWorkBehavior,
				policy.PayrollPreviewEnabled,
				snapshot.PenaltyDeductionAmount);
			await attendancePolicyRepository.UpdateAsync(policy, autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private async Task<Guid> InsertShiftAsync(
		string shiftName,
		TimeOnly startTime,
		TimeOnly endTime)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var shiftRepository = scope.ServiceProvider.GetRequiredService<IRepository<ShiftManagement, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var shift = await shiftRepository.InsertAsync(new ShiftManagement
			{
				ShiftName = shiftName,
				StartTime = startTime,
				EndTime = endTime
			}, autoSave: false);
			await uow.CompleteAsync();
			return shift.Id;
		}
	}

	private async Task AssignEmployeeShiftAsync(Guid employeeId, Guid shiftId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var employeeRepository = scope.ServiceProvider.GetRequiredService<IRepository<Employee, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var employee = await employeeRepository.GetAsync(employeeId);
			employee.ShiftId = shiftId;
			await employeeRepository.UpdateAsync(employee, autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private async Task ClearEmployeeShiftAsync(Guid employeeId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var employeeRepository = scope.ServiceProvider.GetRequiredService<IRepository<Employee, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var employee = await employeeRepository.GetAsync(employeeId);
			employee.ShiftId = null;
			await employeeRepository.UpdateAsync(employee, autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private async Task SeedAttendanceOnDateAsync(Guid employeeId, DateOnly attendanceDate)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var attendanceRepository = scope.ServiceProvider.GetRequiredService<IRepository<EmployeeAttendance, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			await attendanceRepository.InsertAsync(new EmployeeAttendance
			{
				TenantId = tenantId,
				EmployeeId = employeeId,
				AttendanceDate = attendanceDate,
				EducationMediumStageId = EmployeeTestHelper.DefaultMediumStageId,
				StatusType = EmployeeAttendanceStatusType.Present,
				Remarks = "Holiday attendance row"
			}, autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private async Task ClearStatusEffectiveDateAsync(Guid employeeId, DateTimeOffset statusLastChangedAt)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var employeeRepository = scope.ServiceProvider.GetRequiredService<IRepository<Employee, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var employee = await employeeRepository.GetAsync(employeeId);
			typeof(Employee).GetProperty(nameof(Employee.StatusEffectiveDate))!.SetValue(employee, null);
			typeof(Employee).GetProperty(nameof(Employee.StatusLastChangedAt))!.SetValue(employee, statusLastChangedAt);
			await employeeRepository.UpdateAsync(employee, autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private async Task SetPayrollPolicyDayCalculationAsync(Guid payrollPolicyId, DayCalculationType dayCalculationType)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var payrollPolicyRepository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollPolicy, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var policy = await payrollPolicyRepository.GetAsync(payrollPolicyId);
			policy.Update(
				policy.WorkCalendarPolicyId,
				policy.Name,
				policy.Status,
				policy.IsDefault,
				dayCalculationType,
				policy.ManualDays,
				policy.GetManualMonthDays(),
				policy.SaturdayWorkPolicy,
				policy.GetHolidayOverrides(),
				policy.GetLockedMonthSettings());
			await payrollPolicyRepository.UpdateAsync(policy, autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private async Task<Guid> AssignTotalMonthEmployeeOverrideAsync(Guid employeeId, DateTime effectiveFrom)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var academicYearRepository = scope.ServiceProvider.GetRequiredService<IAcademicYearRepository>();
		var payrollPolicyRepository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollPolicy, Guid>>();
		var attendancePolicyRepository = scope.ServiceProvider.GetRequiredService<IRepository<AttendancePolicy, Guid>>();
		var assignmentRepository = scope.ServiceProvider.GetRequiredService<IRepository<HrPolicyAssignment, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var targetDate = effectiveFrom.Date;
			var academicYears = await academicYearRepository.GetListAsync();
			var academicYear = academicYears.Single(x => x.StartDate <= targetDate && x.EndDate >= targetDate);
			var payrollQuery = await payrollPolicyRepository.GetQueryableAsync();
			var defaultPayroll = payrollQuery.Single(x =>
				x.AcademicYearId == academicYear.Id &&
				x.IsDefault &&
				x.Status == HrPolicyStatus.Active);
			var attendanceQuery = await attendancePolicyRepository.GetQueryableAsync();
			var defaultAttendance = attendanceQuery.Single(x =>
				x.AcademicYearId == academicYear.Id &&
				x.IsDefault &&
				x.Status == HrPolicyStatus.Active);
			var overridePayroll = new PayrollPolicy(
				Guid.NewGuid(),
				tenantId,
				academicYear.Id,
				defaultPayroll.WorkCalendarPolicyId,
				$"Mid-month override total month {Guid.NewGuid():N}",
				HrPolicyStatus.Active,
				false,
				DayCalculationType.TotalMonthDays,
				null,
				null,
				defaultPayroll.SaturdayWorkPolicy,
				defaultPayroll.GetHolidayOverrides(),
				defaultPayroll.GetLockedMonthSettings());
			await payrollPolicyRepository.InsertAsync(overridePayroll, autoSave: false);
			await assignmentRepository.InsertAsync(new HrPolicyAssignment(
				Guid.NewGuid(),
				tenantId,
				academicYear.Id,
				HrPolicyAssignmentTargetType.Employee,
				employeeId.ToString("D"),
				"Mid-month override employee",
				defaultAttendance.Id,
				overridePayroll.Id,
				null,
				effectiveFrom,
				null,
				true,
				100,
				"Mid-month payroll override regression"), autoSave: false);
			await uow.CompleteAsync();
			return overridePayroll.Id;
		}
	}

	private async Task AssertAttendanceStagePersistedAsync(Guid employeeId, DateOnly attendanceDate)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var attendanceRepository = scope.ServiceProvider.GetRequiredService<IRepository<EmployeeAttendance, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var query = await attendanceRepository.GetQueryableAsync();
			var attendance = query.Single(x =>
				x.EmployeeId == employeeId &&
				x.AttendanceDate == attendanceDate);

			attendance.EducationMediumStageId.ShouldBe(EmployeeTestHelper.DefaultMediumStageId);
			await uow.CompleteAsync();
		}
	}

	private async Task AssertNoStalePayrollRowsAsync(Guid employeeId, string monthYear)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var snapshotRepository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>();
		var reviewRepository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollRunReview, Guid>>();
		var adjustmentRepository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollAdjustment, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var snapshots = await snapshotRepository.GetQueryableAsync();
			var reviews = await reviewRepository.GetQueryableAsync();
			var adjustments = await adjustmentRepository.GetQueryableAsync();

			snapshots.Any(x => x.MonthYear == monthYear && x.EmployeeId == employeeId).ShouldBeFalse();
			reviews.Any(x => x.MonthYear == monthYear && x.EmployeeId == employeeId).ShouldBeFalse();
			adjustments.Any(x => x.MonthYear == monthYear && x.EmployeeId == employeeId).ShouldBeFalse();
			await uow.CompleteAsync();
		}
	}

	private async Task<Guid> ClearDefaultPayrollPolicyForMonthAsync(DateTime monthDate)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var academicYearRepository = scope.ServiceProvider.GetRequiredService<IAcademicYearRepository>();
		var payrollPolicyRepository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollPolicy, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var academicYearQuery = await academicYearRepository.GetQueryableAsync();
			var academicYear = academicYearQuery.Single(x =>
				x.StartDate <= monthDate &&
				x.EndDate >= monthDate);
			var payrollPolicyQuery = await payrollPolicyRepository.GetQueryableAsync();
			var defaultPolicy = payrollPolicyQuery.Single(x =>
				x.AcademicYearId == academicYear.Id &&
				x.IsDefault &&
				x.Status == HrPolicyStatus.Active);

			defaultPolicy.SetDefault(false);
			await payrollPolicyRepository.UpdateAsync(defaultPolicy, autoSave: false);
			await uow.CompleteAsync();
			return defaultPolicy.Id;
		}
	}

	private async Task<List<Guid>> DisablePolicyAssignmentsForMonthAsync(DateTime monthDate)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var academicYearRepository = scope.ServiceProvider.GetRequiredService<IAcademicYearRepository>();
		var assignmentRepository = scope.ServiceProvider.GetRequiredService<IRepository<HrPolicyAssignment, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var academicYearQuery = await academicYearRepository.GetQueryableAsync();
			var academicYear = academicYearQuery.Single(x =>
				x.StartDate <= monthDate &&
				x.EndDate >= monthDate);
			var assignmentQuery = await assignmentRepository.GetQueryableAsync();
			var assignments = assignmentQuery
				.Where(x =>
					x.AcademicYearId == academicYear.Id &&
					x.IsActive &&
					x.EffectiveFrom <= monthDate &&
					(!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= monthDate))
				.ToList();

			foreach (var assignment in assignments)
			{
				assignment.Update(
					assignment.AttendancePolicyId,
					assignment.PayrollPolicyId,
					assignment.LeavePolicyId,
					assignment.EffectiveFrom,
					assignment.EffectiveTo,
					isActive: false,
					assignment.Priority,
					assignment.AssignmentReason);
				await assignmentRepository.UpdateAsync(assignment, autoSave: false);
			}

			await uow.CompleteAsync();
			return assignments.Select(x => x.Id).ToList();
		}
	}

	private async Task RestoreDefaultPayrollPolicyAsync(Guid payrollPolicyId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var payrollPolicyRepository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollPolicy, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var policy = await payrollPolicyRepository.GetAsync(payrollPolicyId);
			policy.SetDefault(true);
			await payrollPolicyRepository.UpdateAsync(policy, autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private async Task RestorePolicyAssignmentsAsync(List<Guid> assignmentIds)
	{
		if (assignmentIds.Count == 0)
		{
			return;
		}

		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var assignmentRepository = scope.ServiceProvider.GetRequiredService<IRepository<HrPolicyAssignment, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			foreach (var assignmentId in assignmentIds)
			{
				var assignment = await assignmentRepository.GetAsync(assignmentId);
				assignment.Update(
					assignment.AttendancePolicyId,
					assignment.PayrollPolicyId,
					assignment.LeavePolicyId,
					assignment.EffectiveFrom,
					assignment.EffectiveTo,
					isActive: true,
					assignment.Priority,
					assignment.AssignmentReason);
				await assignmentRepository.UpdateAsync(assignment, autoSave: false);
			}

			await uow.CompleteAsync();
		}
	}

	private static decimal CalculateExpectedOvertime(
		decimal monthlyGrossSalary,
		decimal basisDays,
		TimeOnly shiftStart,
		TimeOnly shiftEnd,
		decimal overtimeMinutes,
		decimal multiplier)
	{
		var shiftHours = (decimal)(shiftEnd.ToTimeSpan() - shiftStart.ToTimeSpan()).TotalHours;
		var overtimeHours = overtimeMinutes / 60m;
		return Math.Round(
			monthlyGrossSalary / basisDays / shiftHours * overtimeHours * multiplier,
			2,
			MidpointRounding.AwayFromZero);
	}

	private sealed record AttendancePolicyOvertimeSnapshot(
		Guid PolicyId,
		bool OvertimeEnabled,
		AttendanceOvertimeRate? OvertimeRate,
		int? MinOvertimeMinutes,
		decimal? PenaltyDeductionAmount);

	private sealed record AttendancePolicyLateDeductionSnapshot(
		Guid PolicyId,
		int GraceTime,
		int MaxLateAllowedPerMonth,
		AttendancePenaltyType PenaltyType,
		int PenaltyCountThreshold,
		decimal? PenaltyDeductionAmount);
}
