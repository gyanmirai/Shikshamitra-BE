using System;
using System.Collections.Generic;
using ShikshaMitra.Students.Attendance;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students;

public class AttendancePolicy_Tests
{
	[Fact]
	public void Default_policy_has_daily_credit_and_governed_correction_rules()
	{
		var policy = new AttendancePolicy(); AttendancePolicy.Validate(policy);
		policy.Defaults.LateCredit.ShouldBe(1m); policy.Defaults.HalfDayCredit.ShouldBe(.5m);
		policy.Defaults.HalfDayLeaveTreatment.ShouldBe("HalfDenominator");
		policy.Defaults.PriorDayCorrectionRequiresApproval.ShouldBeTrue(); policy.Defaults.LockedCorrectionRequiresApproval.ShouldBeTrue();
	}
	[Fact]
	public void Division_exception_takes_precedence_over_board_and_class()
	{
		var board = Guid.NewGuid(); var standard = Guid.NewGuid(); var division = Guid.NewGuid();
		var policy = new AttendancePolicy { Exceptions = [Rule("Board", board: board), Rule("Class", standard: standard), Rule("Division", division: division)] };
		policy.Exceptions[0].Options.HalfDayCredit = .2m; policy.Exceptions[1].Options.HalfDayCredit = .3m; policy.Exceptions[2].Options.HalfDayCredit = .4m;
		AttendancePolicy.Validate(policy);
		policy.Resolve(new(board, StandardId: standard, DivisionId: division)).HalfDayCredit.ShouldBe(.4m);
		policy.Resolve(new(board, StandardId: standard)).HalfDayCredit.ShouldBe(.3m);
	}
	[Fact]
	public void Equal_priority_overlapping_rules_are_rejected()
	{
		var division = Guid.NewGuid(); var policy = new AttendancePolicy { Exceptions = [Rule("A", division: division), Rule("B", division: division)] };
		Should.Throw<BusinessException>(() => AttendancePolicy.Validate(policy)).Code.ShouldBe(AttendancePolicyErrorCodes.AmbiguousRules);
	}
	[Fact]
	public void Independent_divisions_are_not_an_ambiguity()
	{
		AttendancePolicy.Validate(new() { Exceptions = [Rule("A", division: Guid.NewGuid()), Rule("B", division: Guid.NewGuid())] });
	}
	[Fact]
	public void Null_rule_from_external_json_is_a_validation_error()
	{
		var policy = AttendancePolicy.FromJson("{\"schemaVersion\":1,\"exceptions\":[null]}");
		Should.Throw<BusinessException>(() => AttendancePolicy.Validate(policy)).Code.ShouldBe(AttendancePolicyErrorCodes.InvalidPolicy);
	}
	[Theory]
	[InlineData(-.1)] [InlineData(1.1)]
	public void Invalid_credit_is_rejected_before_persistence(double credit)
	{
		var p = new AttendancePolicy(); p.Defaults.HalfDayCredit = (decimal)credit;
		Should.Throw<BusinessException>(() => new StudentAttendanceConfiguration(Guid.NewGuid(), Guid.NewGuid(), null, p.ToJson()));
	}
	[Fact]
	public void Unsupported_leave_treatment_and_invalid_timezone_are_rejected()
	{
		var p = new AttendancePolicy(); p.Defaults.ApprovedLeaveTreatment = "Present";
		Should.Throw<BusinessException>(() => AttendancePolicy.Validate(p));
		p.Defaults.ApprovedLeaveTreatment = "Exclude"; p.Defaults.SchoolTimeZone = "not/a-zone";
		Should.Throw<BusinessException>(() => AttendancePolicy.Validate(p));
	}
	[Fact]
	public void Published_version_is_immutable_when_draft_changes()
	{
		var p = new AttendancePolicy(); var config = new StudentAttendanceConfiguration(Guid.NewGuid(), Guid.NewGuid(), null, p.ToJson());
		var version = new StudentAttendancePolicyVersion(Guid.NewGuid(), config.TenantId!.Value, config.Id, null, 1, DateTime.Today, config.DraftPolicyJson);
		p.Defaults.HalfDayCredit = .25m; config.SaveDraft(p.ToJson());
		AttendancePolicy.FromJson(version.PolicyJson).Defaults.HalfDayCredit.ShouldBe(.5m);
	}
	[Fact]
	public void Temporary_schedule_overrides_fixed_schedule_without_reducing_present_credit()
	{
		var monday = new DateTime(2026, 10, 5); var enrollment = Guid.NewGuid();
		var regular = Schedule(enrollment, "Fixed", monday, monday.AddDays(30), [1, 2, 3, 4, 5]);
		var temporary = Schedule(enrollment, "Temporary", monday, monday.AddDays(2), [2]);
		var context = new AttendanceApplicabilityContext(new(), new Dictionary<DateTime, AttendanceApplicabilityResult>
			{ [monday] = new(true, "School day"), [monday.AddDays(1)] = new(true, "School day") }, [regular, temporary]);
		context.For(enrollment, monday).IsExpected.ShouldBeFalse();
		context.For(enrollment, monday.AddDays(1)).IsExpected.ShouldBeTrue();
		new AttendancePolicy().Defaults.LateCredit.ShouldBe(1m);
	}
	[Fact]
	public void School_closure_takes_precedence_over_individual_schedule()
	{
		var date = new DateTime(2026, 10, 5); var enrollment = Guid.NewGuid();
		var context = new AttendanceApplicabilityContext(new(), new Dictionary<DateTime, AttendanceApplicabilityResult> { [date] = new(false, "Emergency closure") },
			[Schedule(enrollment, "Fixed", date, date.AddDays(7), [1])]);
		context.For(enrollment, date).IsExpected.ShouldBeFalse(); context.For(enrollment, date).Reason.ShouldBe("Emergency closure");
	}
	[Fact]
	public void Retirement_preserves_past_schedule_and_excludes_future_applicability()
	{
		var monday = new DateTime(2026, 10, 5); var schedule = Schedule(Guid.NewGuid(), "Fixed", monday, null, [1, 2]);
		schedule.Retire(monday.AddDays(1)); schedule.Expected(monday).ShouldBeTrue(); schedule.Expected(monday.AddDays(1)).ShouldBeFalse();
	}
	[Fact]
	public void Invalid_time_duplicate_weekday_and_unbounded_temporary_schedule_are_rejected()
	{
		var date = DateTime.Today;
		Should.Throw<BusinessException>(() => Schedule(Guid.NewGuid(), "Temporary", date, null, [1]));
		Should.Throw<BusinessException>(() => Schedule(Guid.NewGuid(), "Fixed", date, null, [1, 1]));
		Should.Throw<BusinessException>(() => new StudentAttendanceSchedule(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Test", "Fixed", date, null, [1], "12:00", "11:00"));
	}
	private static AttendancePolicyException Rule(string name, Guid? board = null, Guid? standard = null, Guid? division = null) => new()
		{ Id = Guid.NewGuid(), Name = name, BoardId = board, StandardId = standard, DivisionId = division };
	private static StudentAttendanceSchedule Schedule(Guid enrollment, string kind, DateTime start, DateTime? end, int[] weekdays) =>
		new(Guid.NewGuid(), Guid.NewGuid(), enrollment, Guid.NewGuid(), "Individual", kind, start, end, weekdays, "09:00", "11:00");
}
