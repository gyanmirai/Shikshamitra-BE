using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class AttendanceConfigurationApi_Tests : ShikshaMitraApiTestBase
{
	private const string Url = "/api/attendance/configuration";

	public AttendanceConfigurationApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
	{
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Create_Or_Update_Attendance_Configuration()
	{
		var request = BuildValidRequest();

		var response = await Client.PostAsJsonAsync($"{Url}?academicYearId={AcademicYearId}", request, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var body = await response.Content.ReadFromJsonAsync<AttendanceConfigurationResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Success.ShouldBeTrue();
		body.Message.ShouldBe("Attendance configuration saved successfully");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_BadRequest_For_Invalid_Overtime_When_Enabled()
	{
		var request = BuildValidRequest();
		request.Overtime.Enabled = true;
		request.Overtime.Rate = null;
		request.Overtime.MinOvertimeMinutes = null;

		var response = await Client.PostAsJsonAsync($"{Url}?academicYearId={AcademicYearId}", request, JsonOptions);
		response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);
	}

	private static AttendanceConfigurationRequest BuildValidRequest() =>
		new()
		{
			DailyRules = new AttendanceDailyRulesRequest
			{
				HoursBasedPayPercentage = 80
			},
			LateEarlyRules = new AttendanceLateEarlyRulesRequest
			{
				GraceTime = 15,
				MaxLateAllowedPerMonth = 3,
				PenaltyType = "deduct_half_day"
			},
			Overtime = new AttendanceOvertimeRulesRequest
			{
				Enabled = true,
				Rate = "1.5x",
				MinOvertimeMinutes = 30
			}
		};

	private Guid AcademicYearId => Factory.SeededAcademicYearId;
}
