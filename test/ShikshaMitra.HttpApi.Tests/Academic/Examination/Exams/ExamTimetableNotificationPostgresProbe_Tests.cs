using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShikshaMitra.Academic.Performance;
using Volo.Abp.Timing;
using Xunit;

namespace ShikshaMitra.Academic.Examination.Exams;

[Collection(ApiTestCollection.Name)]
public sealed class ExamTimetableNotificationPostgresProbe_Tests
{
	[AcademicPerformancePostgresFact]
	public async Task Guardians_receive_their_own_school_local_sitting_and_a_closure_stops_its_reminder()
	{
		await using var fixture = new ExamDeliveryApiFactory();
		var scenario = new ExamNotificationEvents_Tests(fixture);
		await scenario.PublishExamAsync(fixture.Clock);
	}

	private sealed class ExamDeliveryApiFactory : AcademicPerformancePostgresTestFactory
	{
		public ExamNotificationTestClock Clock { get; } = new();

		protected override void ConfigureWebHost(IWebHostBuilder builder)
		{
			base.ConfigureWebHost(builder);
			builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
				new Dictionary<string, string?> { ["App:ClientUrl"] = "https://school.example.invalid" }));
			builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton<IClock>(Clock)));
		}
	}
}

internal sealed class ExamNotificationTestClock : IClock
{
	public DateTime Now { get; set; } = DateTime.UtcNow;
	public DateTimeKind Kind => DateTimeKind.Utc;
	public bool SupportsMultipleTimezone => true;
	public DateTime Normalize(DateTime value) => value.Kind == DateTimeKind.Local
		? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
