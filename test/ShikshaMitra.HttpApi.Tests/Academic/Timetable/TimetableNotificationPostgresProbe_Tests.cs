using System.Threading.Tasks;
using ShikshaMitra.Academic.Performance;
using Xunit;

namespace ShikshaMitra.Academic.Timetable;

[Collection(ApiTestCollection.Name)]
public sealed class TimetableNotificationPostgresProbe_Tests
{
	[AcademicPerformancePostgresFact]
	public async Task Published_timetable_and_weekly_lecture_changes_preserve_scoped_notification_delivery()
	{
		await using var fixture = new AcademicPerformancePostgresTestFactory();
		var scenario = new TimetableNotificationDelivery_Tests(fixture);
		await scenario.Timetable_transitions_notify_only_affected_locked_elective_cohorts_and_teachers();
	}
}
