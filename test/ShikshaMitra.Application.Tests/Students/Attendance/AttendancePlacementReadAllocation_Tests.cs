using System;
using System.Linq;
using System.Text.Json;
using ShikshaMitra.Students.StudentEnrollments;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Attendance;

public class AttendancePlacementReadAllocation_Tests
{
	[Fact]
	public void Repeated_school_date_reads_reuse_parsed_history_and_refresh_after_placement_changes()
	{
		var standard = Guid.NewGuid(); var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
		var start = new DateTime(2026, 1, 1);
		var member = StudentEnrollment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), standard, a);
		member.ConfirmAttendanceStart(start);
		for (var index = 1; index < 300; index++) member.ChangePlacement(standard, index % 2 == 0 ? a : b, 1, start.AddDays(index));
		member.AttendancePlacementOn(start); // Warm runtime/type setup before measuring synchronous allocation.
		var rows = new AttendanceEnrollmentPlacement?[366];
		var before = GC.GetAllocatedBytesForCurrentThread();
		for (var index = 0; index < rows.Length; index++) rows[index] = member.AttendancePlacementOn(start.AddDays(index));
		var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
		Console.WriteLine($"Synthetic domain placement history=300, school dates=366; allocated bytes={allocated}.");
		rows[0]!.DivisionId.ShouldBe(a); rows[1]!.DivisionId.ShouldBe(b); rows[^1]!.DivisionId.ShouldBe(b);
		allocated.ShouldBeLessThan(4L * 1024 * 1024, "Unchanged dated history must not be deserialized for every school date.");
		member.ChangePlacement(standard, c, 2, new DateTime(2026, 11, 1));
		member.AttendancePlacementOn(new DateTime(2026, 10, 31))!.DivisionId.ShouldBe(b);
		member.AttendancePlacementOn(new DateTime(2026, 11, 1))!.DivisionId.ShouldBe(c);
		member.Deactivate(new DateTime(2026, 11, 2));
		member.AttendancePlacementOn(new DateTime(2026, 11, 2))!.IsActive.ShouldBeFalse();
		member.Activate(new DateTime(2026, 11, 3));
		member.AttendancePlacementOn(new DateTime(2026, 11, 3))!.IsActive.ShouldBeTrue();
	}

	[Fact]
	public void Raw_legacy_history_replacement_keeps_same_date_tie_and_date_boundary_semantics()
	{
		var standard = Guid.NewGuid(); var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
		var start = new DateTime(2026, 1, 1);
		var member = StudentEnrollment.Create(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), standard, a);
		var history = new[] { new AttendanceEnrollmentPlacement(start, standard, a, 1, true),
			new AttendanceEnrollmentPlacement(start, standard, b, 2, false),
			new AttendanceEnrollmentPlacement(start.AddDays(1).AddHours(1), standard, c, 3, true) };
		void SetHistory(AttendanceEnrollmentPlacement[] rows) => typeof(StudentEnrollment)
			.GetProperty(nameof(StudentEnrollment.AttendancePlacementHistoryJson))!.SetValue(member, JsonSerializer.Serialize(rows));
		SetHistory(history);
		member.AttendancePlacementOn(start)!.DivisionId.ShouldBe(a);
		member.AttendancePlacementOn(start.AddDays(1))!.DivisionId.ShouldBe(a);
		member.AttendancePlacementOn(start.AddDays(2))!.DivisionId.ShouldBe(c);
		SetHistory(history.Take(2).Reverse().ToArray());
		member.AttendancePlacementOn(start)!.DivisionId.ShouldBe(b);
		member.AttendancePlacementOn(start)!.IsActive.ShouldBeFalse();
	}
}
