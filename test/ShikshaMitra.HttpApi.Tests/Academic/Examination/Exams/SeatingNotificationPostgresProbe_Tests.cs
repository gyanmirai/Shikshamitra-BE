using System.Threading.Tasks;
using ShikshaMitra.Academic.Performance;
using Xunit;

namespace ShikshaMitra.Academic.Examination.Exams;

[Collection(ApiTestCollection.Name)]
public sealed class SeatingNotificationPostgresProbe_Tests
{
	[AcademicPerformancePostgresFact]
	public async Task Concurrent_exam_and_seating_releases_create_one_delivery_operation()
	{
		await using var fixture = new AcademicPerformancePostgresTestFactory();
		var scenario = new ExamNotificationEvents_Tests(fixture);
		await scenario.PublishSeatingAsync(concurrent: true);
	}
}
