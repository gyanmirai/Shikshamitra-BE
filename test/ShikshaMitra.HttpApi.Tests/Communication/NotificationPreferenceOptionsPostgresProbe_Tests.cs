using System.Threading.Tasks;
using ShikshaMitra.Academic.Performance;
using Xunit;

namespace ShikshaMitra.Communication;

[Collection(ApiTestCollection.Name)]
public sealed class NotificationPreferenceOptionsPostgresProbe_Tests
{
	[AcademicPerformancePostgresFact]
	public async Task The_preference_options_route_returns_only_current_owned_students_on_PostgreSQL()
	{
		await using var fixture = new AcademicPerformancePostgresTestFactory();
		var scenario = new NotificationSelfApi_Tests(fixture);
		await scenario.Preference_options_return_registered_categories_and_current_linked_students_without_staff_access();
	}
}
