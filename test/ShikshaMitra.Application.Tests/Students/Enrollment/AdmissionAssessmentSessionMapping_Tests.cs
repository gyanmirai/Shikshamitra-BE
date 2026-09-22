using System;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionAssessmentSessionMapping_Tests
{
	[Fact]
	public void Session_Timestamps_Are_Exposed_With_An_Explicit_Utc_Offset()
	{
		var startsAt = new DateTime(2026, 9, 21, 5, 0, 0, DateTimeKind.Utc);
		var session = new AdmissionAssessmentSession(
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			"Entrance exam", startsAt, startsAt.AddMinutes(30), "Room 1", 30);

		var dto = AdmissionAssessmentSessionService.Map(session);

		dto.StartsAt.Offset.ShouldBe(TimeSpan.Zero);
		dto.StartsAt.UtcDateTime.ShouldBe(startsAt);
		dto.EndsAt.Offset.ShouldBe(TimeSpan.Zero);
		dto.EndsAt.UtcDateTime.ShouldBe(startsAt.AddMinutes(30));
	}
}
