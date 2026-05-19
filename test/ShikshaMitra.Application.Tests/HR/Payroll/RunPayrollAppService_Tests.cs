using System;
using System.Collections.Generic;
using System.Text.Json;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class RunPayrollAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly IRunPayrollAppService _runPayrollAppService;

	public RunPayrollAppService_Tests()
	{
		_runPayrollAppService = GetRequiredService<IRunPayrollAppService>();
	}

	[Fact]
	public async Task LockPayrollAsync_Should_Deny_When_UserScope_Has_No_MediumStages()
	{
		var request = new RunPayrollRequest
		{
			Month = "2026-04",
			EmployeeIds = new List<Guid> { Guid.NewGuid() }
		};

		var ex = await Should.ThrowAsync<BusinessException>(async () =>
			await _runPayrollAppService.LockPayrollAsync(request));

		ex.Code.ShouldBe(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
	}

	[Fact]
	public void AttendancePolicySnapshots_Should_Be_Stale_When_Config_Warnings_Change()
	{
		var employeeId = Guid.NewGuid();
		var run = new AttendancePolicyEvaluationRun(
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			2026,
			4,
			new DateOnly(2026, 4, 1),
			new DateOnly(2026, 4, 30),
			"old-hash");
		run.SetEvaluationSummary(1, 0, 0, 0, 0, "[]", hasBlockingWarnings: false);
		run.Approve(Guid.NewGuid(), DateTime.UtcNow);
		run.Publish(Guid.NewGuid(), DateTime.UtcNow);

		var snapshot = new PayrollRunItemSnapshot(Guid.NewGuid(), Guid.NewGuid(), "2026-04", employeeId)
		{
			AttendancePolicyEvaluationRunId = run.Id,
			PayrollAttendanceSource = "published_attendance_policy",
			AttendancePolicyWarningsJson = "[]"
		};
		var context = new RunPayrollAppService.AttendancePolicyPayrollContext(
			BuildApprovedResultsConfiguration(),
			run,
			new Dictionary<Guid, List<AttendancePolicyEvaluationResult>> { [employeeId] = new() },
			new HashSet<Guid>(),
			new List<string> { "Published attendance evaluation was created from an older attendance configuration." });

		RunPayrollAppService.AreAttendancePolicySnapshotsStale(new List<PayrollRunItemSnapshot> { snapshot }, context)
			.ShouldBeTrue();
	}

	[Fact]
	public void AttendancePolicySnapshots_Should_Not_Be_Stale_When_Warnings_Match_Regardless_Of_Order()
	{
		var employeeId = Guid.NewGuid();
		var run = new AttendancePolicyEvaluationRun(
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			2026,
			4,
			new DateOnly(2026, 4, 1),
			new DateOnly(2026, 4, 30),
			"old-hash");
		run.SetEvaluationSummary(1, 0, 0, 0, 0, "[]", hasBlockingWarnings: false);
		run.Approve(Guid.NewGuid(), DateTime.UtcNow);
		run.Publish(Guid.NewGuid(), DateTime.UtcNow);

		var warnings = new List<string>
		{
			"Published attendance evaluation was created from an older attendance configuration.",
			"Payroll month overlaps attendance policy effective boundaries."
		};
		var snapshot = new PayrollRunItemSnapshot(Guid.NewGuid(), Guid.NewGuid(), "2026-04", employeeId)
		{
			AttendancePolicyEvaluationRunId = run.Id,
			PayrollAttendanceSource = "published_attendance_policy",
			AttendancePolicyWarningsJson = JsonSerializer.Serialize(new[] { warnings[1], warnings[0] })
		};
		var context = new RunPayrollAppService.AttendancePolicyPayrollContext(
			BuildApprovedResultsConfiguration(),
			run,
			new Dictionary<Guid, List<AttendancePolicyEvaluationResult>> { [employeeId] = new() },
			new HashSet<Guid>(),
			warnings);

		RunPayrollAppService.AreAttendancePolicySnapshotsStale(new List<PayrollRunItemSnapshot> { snapshot }, context)
			.ShouldBeFalse();
	}

	private static AttendanceConfiguration BuildApprovedResultsConfiguration()
	{
		var configuration = new AttendanceConfiguration(
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			80,
			15,
			3,
			AttendancePenaltyType.None,
			false,
			null,
			null);
		configuration.Update(
			"Default Attendance Policy",
			AttendanceConfigurationStatus.Active,
			new DateTime(2026, 4, 1),
			null,
			AttendanceWeeklyOffPattern.SaturdaySunday,
			null,
			80,
			50,
			0,
			AttendanceMissingPunchBehavior.MarkAbsent,
			true,
			15,
			3,
			AttendancePenaltyType.None,
			0,
			false,
			0,
			false,
			null,
			null,
			AttendanceHolidayWorkBehavior.CompOff,
			false,
			true,
			AttendancePayrollIntegrationMode.ApprovedResults,
			false);

		return configuration;
	}
}
