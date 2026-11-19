using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public sealed class PayslipDocumentMatrixFactAttribute : FactAttribute
{
	public PayslipDocumentMatrixFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYROLL_PAYSLIP_MATRIX_TEST_POSTGRES")))
			Skip = "Set PAYROLL_PAYSLIP_MATRIX_TEST_POSTGRES to a separate fresh disposable loopback payroll_tests_* database.";
	}
}

public sealed class PayslipDocumentMatrixTestFactory : PayslipPostgresTestFactory
{
	protected override string ConnectionVariable => "PAYROLL_PAYSLIP_MATRIX_TEST_POSTGRES";
}

[Collection(ApiTestCollection.Name)]
public class PayslipDocumentPayrollMatrix_Tests
{
	[PayslipDocumentMatrixFact]
	public async Task Issued_documents_reconcile_actual_salary_bases_attendance_additional_earnings_and_off_mode()
	{
		using var factory = new PayslipDocumentMatrixTestFactory();
		var workflow = new EsiPayrollWorkflowScenario(factory);
		var api = workflow.Api;
		var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(factory);
		await workflow.ConfigureAsync(true);
		var now = DateTime.UtcNow;
		var month = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		var protectedDocuments = new List<PayslipDocumentDto>();
		foreach (var scenario in new[] { "gross", "ctc", "partial", "joining", "zero", "additional", "exempt", "noncovered", "leaving", "overtime", "relief", "unicode", "off", "off-bank" })
		{
			if (scenario is "off" or "off-bank") await workflow.ConfigureAsync(false);
			var joining = scenario == "joining" ? month.AddDays(14) : month;
			var employee = await EmployeeTestHelper.CreateEmployeeAsync(api, "PayslipMatrix_" + scenario, dateOfJoining: joining);
			if (scenario is "exempt" or "unicode")
			{
				employee.Department = scenario == "unicode" ? "ગુજરાતી વિભાગ" : "Synthetic Science Department";
				employee.Designation = scenario == "unicode" ? "શિક્ષક" : "Synthetic Science Teacher";
				if (scenario == "unicode")
				{
					employee.FirstName = "દીપ્તિ";
					employee.LastName = "શાહ";
				}
				await EsiPayrollWorkflowScenario.RequireSuccess(await api.PutAsJsonAsync(
					$"/api/employees/{employee.Id}/personal-info", employee, json));
			}
			await workflow.EnrollAsync(employee.Id, "1234567890");
			if (scenario == "relief")
				await EsiPayrollWorkflowScenario.RequireSuccess(await api.PutAsJsonAsync(
					$"/api/payroll/statutory-schemes/esi/employees/{employee.Id}/enrollment", new UpdateEmployeeStatutoryEnrollmentDto
					{
						Status = "PendingReview",
						WageCeilingCategory = "PersonWithDisability",
						EmployerShareReliefEligible = true,
						EmployerShareReliefFrom = month,
						DisabilityEvidenceReference = "Synthetic employer-share relief evidence",
						StatusReason = "Synthetic document reconciliation"
					}, json));
			var monthlyAmount = scenario switch { "exempt" => 4000m, "noncovered" => 23000m, "overtime" => 21000m, _ => 10000m };
			var compensation = await workflow.SaveCompensationAsync(employee.Id, month, scenario == "ctc" ? "EMPLOYER_COST" : "GROSS", monthlyAmount);
			if (scenario == "off-bank")
				await EsiPayrollWorkflowScenario.RequireSuccess(await api.PutAsJsonAsync($"/api/employees/{employee.Id}/payroll", new PayrollContract
				{
					BankDetails = new BankDetailsContract
					{
						BankName = "Synthetic Bank",
						BranchName = "Synthetic branch",
						AccountHolderName = employee.FullName,
						AccountNumber = "1234567890",
						IfscCode = "SBIN0001234"
					},
					SalaryDetails = new SalaryDetailsContract
					{
						QuotedSalaryBasis = "GROSS",
						QuotedMonthlyAmount = compensation.QuotedMonthlyAmount,
						SourceSalaryTemplateId = compensation.SourceSalaryTemplateId,
						SourceSalaryTemplateVersionId = compensation.SourceSalaryTemplateVersionId,
						SourceSalaryTemplateVersion = compensation.SourceSalaryTemplateVersion,
						SalaryGeneratedAt = compensation.SavedAt
					}
				}, json));
			await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.ActivateAsync(employee.Id));
			await workflow.SeedAttendanceAsync(employee.Id, month);
			if (scenario is "partial" or "zero") await ApplyOwnedAttendanceFixtureAsync(factory, employee.Id, month, scenario == "zero");
			if (scenario == "leaving")
			{
				// Attendance already exists for the month when HR records a retroactive exit.
				// Keep those source facts to prove the payroll pipeline respects the lifecycle boundary.
				await ConfigureOwnedOvertimeAsync(factory, employee.Id, month, true, DateOnly.FromDateTime(month.AddDays(14)));
				await SeedOwnedPostExitPaidLeaveAsync(factory, employee.Id, month);
				await EsiPayrollWorkflowScenario.RequireSuccess(await api.PutAsJsonAsync($"/api/employees/{employee.Id}/status",
					new
					{
						employeeStatusType = 2,
						effectiveDate = DateOnly.FromDateTime(month.AddDays(14)),
						remarks = "Synthetic leaving-month document verification"
					}, json));
			}
			if (scenario == "overtime") await ConfigureOwnedOvertimeAsync(factory, employee.Id, month, true);
			if (scenario == "additional")
			{
				var adjustmentRequest = new
				{
					month = month.ToString("yyyy-MM"),
					employeeId = employee.Id,
					name = "Synthetic additional earnings",
					amount = 500,
					isEarning = true,
					reason = "Synthetic document reconciliation evidence"
				};
				var premature = await api.PostAsJsonAsync("/api/payroll/run/adjustments", adjustmentRequest, json);
				premature.IsSuccessStatusCode.ShouldBeFalse();
				(await premature.Content.ReadAsStringAsync()).ShouldContain(HRErrorCodes.PayrollAdjustmentRequiresReviewedPreview);
				var preparation = await workflow.CalculateAsync(month);
				await workflow.ReviewAsync(preparation.Id, employee.Id, month);
				var adjustmentResponse = await api.PostAsJsonAsync("/api/payroll/run/adjustments", adjustmentRequest, json);
				await EsiPayrollWorkflowScenario.RequireSuccess(adjustmentResponse);
				var adjustment = (await adjustmentResponse.Content.ReadFromJsonAsync<PayrollAdjustmentDto>(json))!;
				const string settingsUrl = "/api/payroll/statutory-schemes/esi/configuration";
				var settings = (await api.GetFromJsonAsync<EsiConfigurationSettingsDto>(settingsUrl, json))!;
				await EsiPayrollWorkflowScenario.RequireSuccess(await api.PutAsJsonAsync(settingsUrl, new SaveEsiConfigurationDto
				{
					Enabled = true,
					ConcurrencyStamp = settings.Current!.ConcurrencyStamp,
					IdempotencyKey = Guid.NewGuid(),
					Treatments = [new() { ComponentId = adjustment.Id, Treatment = "Included" }]
				}, json));
			}
			var run = await workflow.CalculateAsync(month);
			var item = await workflow.EmployeeAsync(run.Id, employee.Id);
			// New employees have no historical comparison, and low paid attendance is a review warning.
			// Registration warnings must not hide a genuine calculation blocker.
			item.IssueCodes.ShouldContain(IssueCodes.NoPreviousMonthComparison);
			item.IssueCodes.Where(PayrollLockPreconditionGuard.IsBlockingLockIssue).ShouldBeEmpty(scenario);
			item.IssueCodes.Count(code => code == "PF_APPLY_DISABLED").ShouldBe(1, scenario);
			item.IssueCodes.Where(code => code is not (IssueCodes.NoPreviousMonthComparison or IssueCodes.LowAttendance or "PF_APPLY_DISABLED"))
				.ShouldBeEmpty(scenario);
			if (scenario == "ctc")
			{
				Money(item.EmployerCost).ShouldBe(10000m);
				item.FullGrossSalary.ShouldBeLessThan(10000);
				item.EmployerContributionTotal.ShouldBeGreaterThan(0);
			}
			else Money(item.FullGrossSalary).ShouldBe(monthlyAmount);
			if (scenario is "partial" or "joining" or "leaving")
			{
				item.ProrationFactor.ShouldBeGreaterThan(0);
				item.ProrationFactor.ShouldBeLessThan(1);
				item.GrossSalary.ShouldBeLessThan(item.FullGrossSalary);
				item.DeductionComponents.ShouldNotContain(x => x.Name.Contains("LOP", StringComparison.OrdinalIgnoreCase));
			}
			if (scenario == "leaving")
			{
				var eligibleWeekdays = Enumerable.Range(0, 15).Select(day => month.AddDays(day))
					.Count(date => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday));
				item.PayableDays.ShouldBe(eligibleWeekdays);
				item.AdditionalEarnings.ShouldBe(0);
				item.EarningComponents.ShouldNotContain(x => x.Name == "Overtime Pay");
			}
			if (scenario == "zero")
			{
				item.PayableDays.ShouldBe(0);
				item.NetPay.ShouldBe(0);
				item.EmployerContributionTotal.ShouldBe(0);
				item.EmployeeStateInsuranceCoverageDecision.ShouldNotBeNullOrWhiteSpace();
				item.EmployeeStateInsuranceNumber.ShouldBe("1234567890");
			}
			if (scenario == "additional") Money(item.AdditionalEarnings).ShouldBe(500m);
			if (scenario == "exempt")
			{
				item.EmployeeStateInsuranceCoverageDecision.ShouldBe("WITHIN_WAGE_CEILING");
				item.DeductionComponents.Where(x => x.Name.Contains("ESI", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount).ShouldBe(0);
				Money(item.EmployerContributionTotal).ShouldBe(130m);
				Money(item.NetPay).ShouldBe(4000m);
			}
			if (scenario == "noncovered")
			{
				item.EmployeeStateInsuranceCoverageDecision.ShouldBe("ABOVE_WAGE_CEILING");
				item.EmployerContributionTotal.ShouldBe(0);
				item.DeductionComponents.ShouldNotContain(x => x.Name.Contains("ESI", StringComparison.OrdinalIgnoreCase));
				// The existing Gujarat profile still charges professional tax above its threshold.
				Money(item.TotalDeduction).ShouldBe(200m);
				Money(item.NetPay).ShouldBe(22800m);
			}
			if (scenario == "relief")
			{
				item.EmployeeStateInsuranceCoverageDecision.ShouldBe("WITHIN_WAGE_CEILING");
				Money(item.EmployeeStateInsuranceGovernmentContribution).ShouldBe(325m);
				item.EmployerContributionTotal.ShouldBe(0);
				Money(item.EmployerCost).ShouldBe(10000m);
				Money(item.TotalDeduction).ShouldBe(75m);
				Money(item.NetPay).ShouldBe(9925m);
			}
			if (scenario == "overtime")
			{
				var expectedOvertime = Math.Round(monthlyAmount / Money(item.BasisDays) / 8m * 2m * 2m, 2, MidpointRounding.AwayFromZero);
				Money(item.AdditionalEarnings).ShouldBe(expectedOvertime);
				item.EarningComponents.Single(x => x.Name == "Overtime Pay").Amount.ShouldBe((double) expectedOvertime);
				item.TotalEarnings.ShouldBeGreaterThan(21000);
				item.EmployeeStateInsuranceCoverageDecision.ShouldBe("WITHIN_WAGE_CEILING");
				Money(item.EmployeeStateInsuranceWages).ShouldBe(21000m);
				Money(item.DeductionComponents.Where(x => x.Name.Contains("ESI", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount)).ShouldBe(158m);
			}
			await workflow.ReviewAsync(run.Id, employee.Id, month);
			await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(run.Id, employee.Id, month));
			var query = $"employeeId={employee.Id}&monthYear={month:yyyy-MM}";
			var slip = (await api.GetFromJsonAsync<SalarySlipResponse>($"/api/payroll/salary-slips?{query}", json))!;
			slip.Status.ShouldBe("Finalized");
			slip.GrossSalary.ShouldBe(Money(item.GrossSalary));
			slip.FullGrossSalary.ShouldBe(Money(item.FullGrossSalary));
			slip.AdditionalEarnings.ShouldBe(Money(item.AdditionalEarnings));
			Money(slip.TotalEarning).ShouldBe(Money(item.TotalEarnings));
			Money(slip.TotalDeduction).ShouldBe(Money(item.TotalDeduction));
			Money(slip.NetPay).ShouldBe(Money(item.NetPay));
			Money(slip.Earnings.Sum(x => x.Amount) - slip.Deductions.Sum(x => x.Amount)).ShouldBe(Money(slip.NetPay));
			slip.PayableDays.ShouldBe(item.PayableDays);
			// This aggregate includes both unpaid attendance and days outside the employment period.
			Convert.ToDecimal(slip.LopDays!.Value).ShouldBe(Math.Max(0m, slip.PayrollBasisDays!.Value - Convert.ToDecimal(item.PayableDays)));
			// The authoritative persisted factor has four decimal places; currency reconciliation stays exact.
			slip.ProrationFactor.ShouldBe(Math.Round(item.ProrationFactor, 4, MidpointRounding.AwayFromZero));
			slip.PayrollBasisDays.ShouldBe(Money(item.TotalWorkingDays));
			if (scenario is "off" or "off-bank")
			{
				slip.EmployeeStateInsuranceNumber.ShouldBeNull();
				slip.EmployeeStateInsuranceRuleId.ShouldBeNull();
				slip.Deductions.ShouldNotContain(x => x.Name.Contains("ESI", StringComparison.OrdinalIgnoreCase));
				item.EmployerContributionTotal.ShouldBe(0);
			}
			else slip.EmployeeStateInsuranceNumber.ShouldBe("1234567890");
			if (scenario is "exempt" or "unicode")
			{
				slip.Department.ShouldBe(employee.Department);
				slip.Designation.ShouldBe(employee.Designation);
			}
			if (scenario == "unicode") slip.EmployeeName.ShouldBe("દીપ્તિ શાહ");
			var serialized = JsonSerializer.Serialize(slip, json);
			serialized.ShouldNotContain("employerCost");
			serialized.ShouldNotContain("employerContribution");
			serialized.ShouldNotContain("compensationProtection");
			serialized.ToLowerInvariant().ShouldNotContain("governmentcontribution");
			var document = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>($"/api/payroll/payslip-documents?{query}", json))!.Single();
			var canonical = await api.GetByteArrayAsync($"/api/payroll/payslip-documents/{document.Id}/pdf");
			Convert.ToHexString(SHA256.HashData(canonical)).ShouldBe(document.Sha256);
			Convert.ToHexString(SHA256.HashData(await api.GetByteArrayAsync($"/api/payroll/salary-slips/pdf?{query}"))).ShouldBe(document.Sha256);
			var artifacts = Environment.GetEnvironmentVariable("PAYSLIP_VERIFY_ARTIFACTS");
			if (!string.IsNullOrWhiteSpace(artifacts))
			{
				Directory.CreateDirectory(artifacts);
				await File.WriteAllBytesAsync(Path.Combine(artifacts, scenario + ".pdf"), canonical);
				await File.WriteAllTextAsync(Path.Combine(artifacts, scenario + ".json"), JsonSerializer.Serialize(slip, json));
			}
			protectedDocuments.Add(document);
			if (scenario is "overtime" or "leaving") await ConfigureOwnedOvertimeAsync(factory, employee.Id, month, false);
			if (scenario == "leaving")
			{
				var nextMonth = await workflow.CalculateAsync(month.AddMonths(1));
				var nextRows = (await api.GetFromJsonAsync<RunPayrollResponse>($"{EsiPayrollWorkflowScenario.Previews}/{nextMonth.Id}/employees?pageSize=100", json))!;
				nextRows.Items.ShouldNotContain(x => x.EmployeeId == employee.Id);
			}
		}
		foreach (var original in protectedDocuments)
			Convert.ToHexString(SHA256.HashData(await api.GetByteArrayAsync($"/api/payroll/payslip-documents/{original.Id}/pdf")))
				.ShouldBe(original.Sha256);
		factory.Messages.ShouldBeEmpty();
	}

	private static decimal Money(double amount) => Math.Round(Convert.ToDecimal(amount), 2);

	private static async Task SeedOwnedPostExitPaidLeaveAsync(PayslipDocumentMatrixTestFactory factory, Guid employeeId, DateTime month)
	{
		using var scope = factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(EsiPayrollWorkflowScenario.Tenant);
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var years = scope.ServiceProvider.GetRequiredService<IAcademicYearRepository>();
		var year = (await years.GetListAsync()).Single(x => x.StartDate <= month.Date && x.EndDate >= month.Date);
		var policies = scope.ServiceProvider.GetRequiredService<IRepository<LeavePolicy, Guid>>();
		var policy = await policies.InsertAsync(new(Guid.NewGuid(), EsiPayrollWorkflowScenario.Tenant, year.Id,
			"Synthetic post-exit document leave", year.StartDate, year.EndDate, HrPolicyStatus.Active), autoSave: true);
		var rules = scope.ServiceProvider.GetRequiredService<IRepository<LeavePolicyRule, Guid>>();
		var rule = await rules.InsertAsync(new(Guid.NewGuid(), EsiPayrollWorkflowScenario.Tenant, policy.Id,
			LeavePaidType.Paid, LeaveEntitlementMode.Limited, LeaveAccrualPolicyType.Yearly, 12,
			false, null, false, null, true, 0, 30, false, null, false), autoSave: true);
		var attendance = scope.ServiceProvider.GetRequiredService<IRepository<EmployeeAttendance, Guid>>();
		var day = (await attendance.GetListAsync(x => x.EmployeeId == employeeId && x.AttendanceDate.Year == month.Year && x.AttendanceDate.Month == month.Month))
			.OrderByDescending(x => x.AttendanceDate).First(x => x.AttendanceDate.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday));
		day.StatusType = EmployeeAttendanceStatusType.FullDayLeave;
		day.TimeIn = day.TimeOut = null;
		await attendance.UpdateAsync(day, autoSave: true);
		var leaveDate = new DateTimeOffset(day.AttendanceDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
		var leaves = scope.ServiceProvider.GetRequiredService<IEmployeeLeaveRepository>();
		var leave = await leaves.InsertAsync(EmployeeLeave.Create(Guid.NewGuid(), EsiPayrollWorkflowScenario.Tenant,
			employeeId, policy.Id, rule.Id, LeavePaidType.Paid, leaveDate, leaveDate, "Synthetic approved leave recorded before retroactive exit"));
		await scope.ServiceProvider.GetRequiredService<EmployeeLeaveManager>().ApproveAsync(leave, LeavePaidType.Paid, 1m);
		await unit.CompleteAsync();
	}

	private static async Task ConfigureOwnedOvertimeAsync(PayslipDocumentMatrixTestFactory factory, Guid employeeId, DateTime month, bool enabled, DateOnly? afterDate = null)
	{
		using var scope = factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(EsiPayrollWorkflowScenario.Tenant);
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var academicYears = scope.ServiceProvider.GetRequiredService<IAcademicYearRepository>();
		var academicYear = (await academicYears.GetListAsync()).Single(x => x.StartDate <= month.Date && x.EndDate >= month.Date);
		var policies = scope.ServiceProvider.GetRequiredService<IRepository<AttendancePolicy, Guid>>();
		var policy = (await policies.GetListAsync(x => x.AcademicYearId == academicYear.Id && x.IsDefault && x.Status == HrPolicyStatus.Active)).Single();
		policy.Update(policy.WorkCalendarPolicyId, policy.Name, policy.Status, policy.IsDefault, policy.CaptureMode,
			policy.FullDayThresholdPercentage, policy.HalfDayThresholdPercentage, policy.AbsentThresholdPercentage,
			policy.MissingPunchBehavior, policy.ManualCorrectionAllowed, policy.GraceTime, policy.MaxLateAllowedPerMonth,
			policy.PenaltyType, policy.PenaltyCountThreshold, policy.EarlyExitEnabled, policy.EarlyExitGraceTime,
			enabled, AttendanceOvertimeRate.TwoX, enabled ? 30 : null, policy.HolidayWorkBehavior, policy.PayrollPreviewEnabled,
			policy.PenaltyDeductionAmount);
		await policies.UpdateAsync(policy, autoSave: true);
		if (enabled)
		{
			var shifts = scope.ServiceProvider.GetRequiredService<IRepository<ShiftManagement, Guid>>();
			var shift = await shifts.InsertAsync(new() { ShiftName = "Synthetic payslip overtime shift", StartTime = new(9, 0), EndTime = new(17, 0) }, autoSave: true);
			var employees = scope.ServiceProvider.GetRequiredService<IEmployeeRepository>();
			var employee = await employees.GetAsync(employeeId);
			employee.ShiftId = shift.Id;
			await employees.UpdateAsync(employee, autoSave: true);
			var attendance = scope.ServiceProvider.GetRequiredService<IRepository<EmployeeAttendance, Guid>>();
			var day = (await attendance.GetListAsync(x => x.EmployeeId == employeeId && x.AttendanceDate.Year == month.Year && x.AttendanceDate.Month == month.Month))
				.OrderBy(x => x.AttendanceDate).First(x => (!afterDate.HasValue || x.AttendanceDate > afterDate) && x.AttendanceDate.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday));
			day.TimeOut = new DateTimeOffset(DateTime.SpecifyKind(day.AttendanceDate.ToDateTime(new(19, 0)), DateTimeKind.Utc));
			await attendance.UpdateAsync(day, autoSave: true);
		}
		await unit.CompleteAsync();
	}

	private static async Task ApplyOwnedAttendanceFixtureAsync(PayslipDocumentMatrixTestFactory factory,
		Guid employeeId, DateTime month, bool allUnpaid)
	{
		using var scope = factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(EsiPayrollWorkflowScenario.Tenant);
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var repository = scope.ServiceProvider.GetRequiredService<IRepository<EmployeeAttendance, Guid>>();
		var attendance = (await repository.GetListAsync(x => x.EmployeeId == employeeId &&
			x.AttendanceDate.Year == month.Year && x.AttendanceDate.Month == month.Month)).OrderBy(x => x.AttendanceDate).ToList();
		var selected = allUnpaid ? attendance : attendance.Where(x => x.AttendanceDate.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).Take(1);
		foreach (var day in selected)
		{
			day.StatusType = EmployeeAttendanceStatusType.FullDayLeave;
			day.TimeIn = null;
			day.TimeOut = null;
			await repository.UpdateAsync(day, autoSave: true);
		}
		await unit.CompleteAsync();
	}
}