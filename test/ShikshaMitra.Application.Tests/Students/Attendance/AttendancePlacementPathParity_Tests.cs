using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Students.StudentEnrollments;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Attendance;

public class AttendancePlacementPathParity_Tests
{
	[Fact]
	public void Existing_range_history_preserves_daily_path_population_at_effective_date_boundaries()
	{
		var year = Guid.NewGuid(); var tenant = Guid.NewGuid(); var standard = Guid.NewGuid();
		var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
		var first = new DateTime(2026, 9, 1); var move = first.AddDays(2);
		StudentEnrollment Member(bool confirmed = true, bool placed = true)
		{
			var member = StudentEnrollment.Create(Guid.NewGuid(), tenant, Guid.NewGuid(), year, placed ? standard : null, placed ? a : null);
			if (confirmed) member.ConfirmAttendanceStart(first);
			return member;
		}
		var moved = Member(); moved.ChangePlacement(standard, b, 2, move);
		var replaced = Member(); replaced.ChangePlacement(standard, b, 2, move); replaced.ChangePlacement(standard, c, 3, move);
		var inactive = Member(); inactive.ChangePlacement(standard, b, 2, move); inactive.Deactivate(move.AddDays(1)); inactive.Activate(move.AddDays(2));
		var later = Member(false); later.ConfirmAttendanceStart(move.AddDays(3));
		var initiallyUnplaced = Member(placed: false); initiallyUnplaced.ChangePlacement(standard, b, 2, move);
		var cases = new[] {
			new Case("legacy unknown start", Member(false), first, move, [a]),
			new Case("legacy unplaced", Member(false, false), first, move, []),
			new Case("confirmed start after range", later, first, move, [a]),
			new Case("move before range", moved, move.AddDays(1), move.AddDays(2), [b]),
			new Case("move at range start", moved, move, move.AddDays(1), [b]),
			new Case("move at range end", moved, first.AddDays(1), move, [a, b]),
			new Case("same-date replacement", replaced, first, move, [a, c]),
			new Case("inactive/reactivated history", inactive, first, move.AddDays(2), [a, b]),
			new Case("initial null placement", initiallyUnplaced, first, move, [b]) };
		foreach (var scenario in cases)
		{
			var daily = Paths(scenario.Member, Enumerable.Range(0, (scenario.End - scenario.Start).Days + 1)
				.Select(offset => scenario.Member.AttendancePlacementOn(scenario.Start.AddDays(offset))));
			var ranged = Paths(scenario.Member, scenario.Member.AttendancePlacementsBetween(scenario.Start, scenario.End));
			daily.SetEquals(scenario.Divisions.Select(division => (standard, division))).ShouldBeTrue(scenario.Name);
			ranged.SetEquals(daily).ShouldBeTrue(scenario.Name);
		}
	}

	private static HashSet<(Guid Standard, Guid Division)> Paths(StudentEnrollment member, IEnumerable<AttendanceEnrollmentPlacement?> history)
	{
		var result = new HashSet<(Guid, Guid)>();
		void Add(Guid? standard, Guid? division) { if (standard.HasValue && division.HasValue) result.Add((standard.Value, division.Value)); }
		Add(member.StandardId, member.DivisionId);
		foreach (var placement in history) Add(placement?.StandardId, placement?.DivisionId);
		return result;
	}
	private sealed record Case(string Name, StudentEnrollment Member, DateTime Start, DateTime End, Guid[] Divisions);
}
