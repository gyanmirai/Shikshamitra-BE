using System;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationDeliveryTimingPolicy_Tests
{
	private readonly NotificationDeliveryTimingPolicy _policy = new();
	private static readonly TimeOnly QuietStart = new(20, 0);
	private static readonly TimeOnly QuietEnd = new(7, 0);
	private static TimeZoneInfo India => TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
	private static DateTimeOffset At(string value) => DateTimeOffset.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

	[Theory]
	[InlineData(DateTimeKind.Unspecified)]
	[InlineData(DateTimeKind.Local)]
	[InlineData(DateTimeKind.Utc)]
	public void A_saved_school_boundary_uses_the_school_clock_for_any_storage_kind(DateTimeKind kind)
	{
		var midnight = new DateTime(2026, 11, 19, 0, 0, 0, kind);

		var boundary = NotificationDeliveryTimingPolicy.Boundary(midnight, India, later: false);

		boundary.ShouldBe(At("2026-11-18T18:30:00Z"));
		boundary.Offset.ShouldBe(TimeSpan.Zero);
	}

	[Theory]
	[InlineData("2026-11-18T20:00:00+05:30", "2026-11-19T01:30:00Z")]
	[InlineData("2026-11-18T23:30:00+05:30", "2026-11-19T01:30:00Z")]
	[InlineData("2026-11-19T06:59:00+05:30", "2026-11-19T01:30:00Z")]
	[InlineData("2026-11-19T07:00:00+05:30", "2026-11-19T01:30:00Z")]
	[InlineData("2026-11-19T12:00:00+05:30", "2026-11-19T06:30:00Z")]
	public void Quiet_hours_use_the_school_day_and_return_a_utc_due_instant(string notBefore, string expected)
	{
		var result = _policy.Evaluate(At(notBefore), India, true, QuietStart, QuietEnd);
		result.DueAt.ShouldBe(At(expected)); result.DueAt!.Value.Offset.ShouldBe(TimeSpan.Zero); result.IsExpired.ShouldBeFalse();
	}

	[Theory]
	[InlineData("2026-11-18T15:00:00Z")]
	[InlineData("2026-11-18T22:00:00Z")]
	[InlineData("2026-11-19T01:30:00Z")]
	public void Reminders_that_would_arrive_at_or_after_expiry_are_expired_instead_of_deferred(string expiresAt)
	{
		var result = _policy.Evaluate(At("2026-11-18T20:00:00+05:30"), India, true, QuietStart, QuietEnd, At(expiresAt));
		result.IsExpired.ShouldBeTrue(); result.DueAt.ShouldBeNull();
	}

	[Fact]
	public void Explicit_urgent_bypass_skips_quiet_hours_but_never_bypasses_expiry()
	{
		var notBefore = At("2026-11-18T20:00:00+05:30");
		var allowed = _policy.Evaluate(notBefore, India, true, QuietStart, QuietEnd, bypassQuietHours: true);
		allowed.DueAt.ShouldBe(notBefore.ToUniversalTime()); allowed.DueAt!.Value.Offset.ShouldBe(TimeSpan.Zero);
		var expired = _policy.Evaluate(notBefore, India, true, QuietStart, QuietEnd, notBefore, true);
		expired.IsExpired.ShouldBeTrue(); expired.DueAt.ShouldBeNull();
	}

	[Fact]
	public void A_daytime_quiet_window_ends_on_the_same_school_date()
	{
		var result = _policy.Evaluate(At("2026-11-18T12:30:00+05:30"), India, true, new(12, 0), new(14, 0));
		result.DueAt.ShouldBe(At("2026-11-18T08:30:00Z"));
	}

	[Fact]
	public void Disabled_quiet_hours_preserve_the_requested_instant_but_still_reject_expiry()
	{
		var instant = At("2026-11-18T21:00:00+05:30");
		_policy.Evaluate(instant, India, false, QuietStart, QuietEnd).DueAt!.Value.Offset.ShouldBe(TimeSpan.Zero);
		_policy.Evaluate(instant, India, false, QuietStart, QuietEnd, instant).IsExpired.ShouldBeTrue();
	}

	[Fact]
	public void An_equal_start_and_end_cannot_accidentally_create_an_all_day_quiet_window()
	{
		Should.Throw<ArgumentException>(() => _policy.Evaluate(At("2026-11-18T12:00:00Z"), India, true, new(7, 0), new(7, 0)));
	}

	[Fact]
	public void Spring_forward_skips_a_nonexistent_quiet_end_to_the_first_valid_school_minute()
	{
		var zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
		var result = _policy.Evaluate(At("2026-03-08T06:45:00Z"), zone, true, new(20, 0), new(2, 30));
		result.DueAt.ShouldBe(At("2026-03-08T07:00:00Z"));
	}

	[Fact]
	public void Fall_back_uses_the_later_quiet_end_even_between_the_two_occurrences()
	{
		var zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
		var result = _policy.Evaluate(At("2026-11-01T05:45:00Z"), zone, true, new(20, 0), new(1, 30));
		result.DueAt.ShouldBe(At("2026-11-01T06:30:00Z"));
	}
}
