using System;
using System.Linq;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class AttendanceConfigurationDomainTests
{
	[Fact]
	public void Should_Validate_Threshold_Ordering()
	{
		var config = BuildConfiguration();

		var ex = Should.Throw<BusinessException>(() =>
			config.Update(
				"Default Attendance Policy",
				AttendanceConfigurationStatus.Draft,
				null,
				null,
				AttendanceWeeklyOffPattern.SaturdaySunday,
				null,
				75,
				75,
				0,
				AttendanceMissingPunchBehavior.RequiresReview,
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
				AttendancePayrollIntegrationMode.Advisory,
				false));

		ex.Code.ShouldBe("ShikshaMitra:HalfDayThresholdMustBeLessThanFullDay");
	}

	[Fact]
	public void Should_Require_Effective_Date_For_Active_Policy()
	{
		var config = BuildConfiguration();

		var ex = Should.Throw<BusinessException>(() =>
			config.Update(
				"Default Attendance Policy",
				AttendanceConfigurationStatus.Active,
				null,
				null,
				AttendanceWeeklyOffPattern.SaturdaySunday,
				null,
				75,
				50,
				0,
				AttendanceMissingPunchBehavior.RequiresReview,
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
				AttendancePayrollIntegrationMode.Advisory,
				false));

		ex.Code.ShouldBe("ShikshaMitra:AttendanceEffectiveFromRequired");
	}

	[Fact]
	public void Should_Reject_Duplicate_Active_Overrides()
	{
		var config = BuildConfiguration();

		var ex = Should.Throw<BusinessException>(() =>
			config.ReplaceOverrides(new[]
			{
				BuildOverride("role-1", true),
				BuildOverride("role-1", true)
			}));

		ex.Code.ShouldBe("ShikshaMitra:DuplicateAttendancePolicyOverride");
	}

	[Fact]
	public void Should_Preserve_Duplicate_Inactive_Overrides()
	{
		var config = BuildConfiguration();

		config.ReplaceOverrides(new[]
		{
			BuildOverride("role-1", false),
			BuildOverride("role-1", false)
		});

		config.Overrides.Count.ShouldBe(2);
		config.Overrides.All(x => !x.IsActive).ShouldBeTrue();
	}

	[Fact]
	public void Should_Require_Overtime_Details_Only_When_Enabled()
	{
		var disabled = BuildConfiguration();

		disabled.Update(
			"Default Attendance Policy",
			AttendanceConfigurationStatus.Draft,
			null,
			null,
			AttendanceWeeklyOffPattern.SaturdaySunday,
			null,
			75,
			50,
			0,
			AttendanceMissingPunchBehavior.RequiresReview,
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
			AttendancePayrollIntegrationMode.Advisory,
			false);

		var enabled = BuildConfiguration();
		var ex = Should.Throw<BusinessException>(() =>
			enabled.Update(
				"Default Attendance Policy",
				AttendanceConfigurationStatus.Draft,
				null,
				null,
				AttendanceWeeklyOffPattern.SaturdaySunday,
				null,
				75,
				50,
				0,
				AttendanceMissingPunchBehavior.RequiresReview,
				true,
				15,
				3,
				AttendancePenaltyType.None,
				0,
				false,
				0,
				true,
				null,
				null,
				AttendanceHolidayWorkBehavior.CompOff,
				false,
				true,
				AttendancePayrollIntegrationMode.Advisory,
				false));

		ex.Code.ShouldBe("ShikshaMitra:OvertimeRateAndMinMinutesRequired");
	}

	[Fact]
	public void Evaluation_Run_Should_Enforce_Status_Transitions()
	{
		var run = new AttendancePolicyEvaluationRun(
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			2026,
			4,
			new DateOnly(2026, 4, 1),
			new DateOnly(2026, 4, 30),
			"hash");

		Should.Throw<BusinessException>(() => run.Publish(Guid.NewGuid(), DateTime.UtcNow))
			.Code.ShouldBe("ShikshaMitra:AttendanceEvaluationCannotPublish");

		run.SetEvaluationSummary(1, 0, 0, 0, 0, "[]", hasBlockingWarnings: false);
		run.Approve(Guid.NewGuid(), DateTime.UtcNow);
		run.Publish(Guid.NewGuid(), DateTime.UtcNow);
		run.Status.ShouldBe(AttendancePolicyEvaluationRunStatus.Published);

		run.Supersede();
		run.Status.ShouldBe(AttendancePolicyEvaluationRunStatus.Superseded);
	}

	[Fact]
	public void Configuration_Hash_Should_Change_When_Rules_Change()
	{
		var config = BuildConfiguration();
		var first = AttendanceConfigurationHasher.Compute(config);

		config.Update(
			"Default Attendance Policy",
			AttendanceConfigurationStatus.Draft,
			null,
			null,
			AttendanceWeeklyOffPattern.SaturdaySunday,
			null,
			80,
			50,
			0,
			AttendanceMissingPunchBehavior.RequiresReview,
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
			AttendancePayrollIntegrationMode.Advisory,
			false);

		AttendanceConfigurationHasher.Compute(config).ShouldNotBe(first);

	}

	private static AttendanceConfiguration BuildConfiguration() =>
		new(
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			75,
			15,
			3,
			AttendancePenaltyType.None,
			false,
			null,
			null);

	private static AttendancePolicyOverride BuildOverride(string targetId, bool isActive) =>
		new(
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			AttendancePolicyOverrideTargetType.Role,
			targetId,
			targetId,
			isActive,
			null,
			null,
			null,
			null,
			null,
			null,
			null);
}
