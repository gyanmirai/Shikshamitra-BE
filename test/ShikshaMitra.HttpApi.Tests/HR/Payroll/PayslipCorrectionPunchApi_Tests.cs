using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public sealed class PayslipCorrectionPunchFactAttribute : FactAttribute
{
	public PayslipCorrectionPunchFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYROLL_PAYSLIP_CORRECTION_PUNCH_TEST_POSTGRES")))
			Skip = "Set PAYROLL_PAYSLIP_CORRECTION_PUNCH_TEST_POSTGRES to a fresh disposable loopback payroll_tests_* database.";
	}
}

public sealed class PayslipCorrectionPunchTestFactory : PayslipPostgresTestFactory
{
	protected override string ConnectionVariable => "PAYROLL_PAYSLIP_CORRECTION_PUNCH_TEST_POSTGRES";
}

[Collection(ApiTestCollection.Name)]
public class PayslipCorrectionPunchApi_Tests
{
	[PayslipCorrectionPunchFact]
	public async Task Invalid_punches_are_rejected_and_valid_time_edits_reissue_overtime_and_late_pay_without_rewriting_originals()
	{
		using var factory = new PayslipCorrectionPunchTestFactory();
		var workflow = new EsiPayrollWorkflowScenario(factory);
		var api = workflow.Api;
		var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(factory);
		var now = DateTime.UtcNow;
		var month = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(api, "PayslipPunchCorrection", dateOfJoining: month);
		await workflow.ConfigureAsync(true);
		await workflow.EnrollAsync(employee.Id, "1234567890");
		await workflow.SaveCompensationAsync(employee.Id, month, "GROSS", 10000m);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.ActivateAsync(employee.Id));
		await workflow.SeedAttendanceAsync(employee.Id, month);
		await ConfigurePunchFixtureAsync(factory, employee.Id, month);
		var run = await workflow.CalculateAsync(month);
		var initial = await workflow.EmployeeAsync(run.Id, employee.Id);
		initial.EarningComponents.Single(x => x.Name == "Overtime Pay").Amount.ShouldBeGreaterThan(0);
		initial.DeductionComponents.Single(x => x.Name == "Late arrival deduction").Amount.ShouldBe(100);
		await workflow.ReviewAsync(run.Id, employee.Id, month);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(run.Id, employee.Id, month));
		var documentsUrl = $"/api/payroll/payslip-documents?employeeId={employee.Id}&monthYear={month:yyyy-MM}";
		var original = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(documentsUrl, json))!.Single();
		var start = await api.PostAsJsonAsync("/api/payroll/payslip-corrections", new StartPayslipCorrectionDto
		{
			DocumentId = original.Id, IdempotencyKey = Guid.NewGuid(), Reason = "Correct synthetic overtime and clock-in"
		}, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(start);
		var draft = (await start.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		var punchDate = draft.Attendance.Single(x => x.TimeOut!.Value.UtcDateTime.Hour == 19).Date;
		foreach (var invalid in new[] { "multi_day_out", "wrong_day_in", "reversed", "equal" })
		{
			var attendance = JsonSerializer.Deserialize<List<PayslipCorrectionAttendanceDto>>(JsonSerializer.Serialize(draft.Attendance))!;
			var day = attendance.Single(x => x.Date == punchDate);
			switch (invalid)
			{
				case "multi_day_out": day.TimeOut = day.TimeOut!.Value.AddDays(10); break;
				case "wrong_day_in": day.TimeIn = day.TimeIn!.Value.AddDays(-1); break;
				case "reversed": day.TimeOut = day.TimeIn!.Value.AddMinutes(-1); break;
				case "equal": day.TimeOut = day.TimeIn; break;
			}
			var rejected = await api.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/calculate",
				new CalculatePayslipCorrectionDto { ConcurrencyStamp = draft.ConcurrencyStamp, Attendance = attendance }, json);
			rejected.IsSuccessStatusCode.ShouldBeFalse(invalid);
			(await rejected.Content.ReadAsStringAsync()).ShouldContain(HRErrorCodes.AttendancePolicyEvaluationInvalidPunches);
			var persisted = (await api.GetFromJsonAsync<List<PayslipCorrectionDto>>($"/api/payroll/payslip-corrections?documentId={original.Id}", json))!.Single();
			persisted.Status.ShouldBe("DRAFT");
			persisted.ConcurrencyStamp.ShouldBe(draft.ConcurrencyStamp);
			persisted.Candidate.ShouldBeNull();
		}
		var corrected = draft.Attendance.Single(x => x.Date == punchDate);
		corrected.TimeIn = new DateTimeOffset(punchDate.ToDateTime(new(9, 0)), TimeSpan.Zero);
		corrected.TimeOut = new DateTimeOffset(punchDate.ToDateTime(new(17, 0)), TimeSpan.Zero);
		var calculate = await api.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/calculate",
			new CalculatePayslipCorrectionDto { ConcurrencyStamp = draft.ConcurrencyStamp, Attendance = draft.Attendance }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(calculate);
		var candidate = (await calculate.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		candidate.Candidate!.TotalEarnings.ShouldBe(10000);
		candidate.Candidate.NetPay.ShouldBe(9925);
		candidate.Candidate.EarningComponents.ShouldNotContain(x => x.Name == "Overtime Pay");
		candidate.Candidate.DeductionComponents.ShouldNotContain(x => x.Name == "Late arrival deduction");
		var review = await api.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/review",
			new PayslipCorrectionActionDto { ConcurrencyStamp = candidate.ConcurrencyStamp }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(review);
		var reviewed = (await review.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		await EsiPayrollWorkflowScenario.RequireSuccess(await api.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/apply",
			new PayslipCorrectionActionDto { ConcurrencyStamp = reviewed.ConcurrencyStamp }, json));
		var versions = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(documentsUrl, json))!;
		versions.Count.ShouldBe(2);
		versions.Single(x => x.IsCurrent).Revision.ShouldBe(2);
		Convert.ToHexString(SHA256.HashData(await api.GetByteArrayAsync($"/api/payroll/payslip-documents/{original.Id}/pdf"))).ShouldBe(original.Sha256);
		var slip = (await api.GetFromJsonAsync<SalarySlipResponse>($"/api/payroll/salary-slips?employeeId={employee.Id}&monthYear={month:yyyy-MM}", json))!;
		slip.NetSalary.ShouldBe(9925);
	}

	private static async Task ConfigurePunchFixtureAsync(PayslipCorrectionPunchTestFactory factory, Guid employeeId, DateTime month)
	{
		using var scope = factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(EsiPayrollWorkflowScenario.Tenant);
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var years = scope.ServiceProvider.GetRequiredService<IAcademicYearRepository>();
		var year = (await years.GetListAsync()).Single(x => x.StartDate <= month.Date && x.EndDate >= month.Date);
		var policies = scope.ServiceProvider.GetRequiredService<IRepository<AttendancePolicy, Guid>>();
		var policy = (await policies.GetListAsync(x => x.AcademicYearId == year.Id && x.IsDefault && x.Status == HrPolicyStatus.Active)).Single();
		policy.Update(policy.WorkCalendarPolicyId, policy.Name, policy.Status, policy.IsDefault, policy.CaptureMode,
			policy.FullDayThresholdPercentage, policy.HalfDayThresholdPercentage, policy.AbsentThresholdPercentage,
			policy.MissingPunchBehavior, policy.ManualCorrectionAllowed, 0, 0, AttendancePenaltyType.SalaryDeduction, 1,
			policy.EarlyExitEnabled, policy.EarlyExitGraceTime, true, AttendanceOvertimeRate.TwoX, 30,
			policy.HolidayWorkBehavior, policy.PayrollPreviewEnabled, 100m);
		await policies.UpdateAsync(policy, autoSave: true);
		var shifts = scope.ServiceProvider.GetRequiredService<IRepository<ShiftManagement, Guid>>();
		var shift = await shifts.InsertAsync(new() { ShiftName = "Synthetic correction clock shift", StartTime = new(9, 0), EndTime = new(17, 0) }, autoSave: true);
		var employees = scope.ServiceProvider.GetRequiredService<IEmployeeRepository>();
		var employee = await employees.GetAsync(employeeId);
		employee.ShiftId = shift.Id;
		await employees.UpdateAsync(employee, autoSave: true);
		var attendance = scope.ServiceProvider.GetRequiredService<IRepository<EmployeeAttendance, Guid>>();
		var day = (await attendance.GetListAsync(x => x.EmployeeId == employeeId && x.AttendanceDate.Year == month.Year && x.AttendanceDate.Month == month.Month))
			.OrderBy(x => x.AttendanceDate).First(x => x.AttendanceDate.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday));
		day.TimeIn = new DateTimeOffset(day.AttendanceDate.ToDateTime(new(10, 0)), TimeSpan.Zero);
		day.TimeOut = new DateTimeOffset(day.AttendanceDate.ToDateTime(new(19, 0)), TimeSpan.Zero);
		await attendance.UpdateAsync(day, autoSave: true);
		await unit.CompleteAsync();
	}
}
