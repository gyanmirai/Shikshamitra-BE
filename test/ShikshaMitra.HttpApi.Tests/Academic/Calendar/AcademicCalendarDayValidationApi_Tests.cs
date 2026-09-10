using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace ShikshaMitra.Academic.Calendar;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Academic)]
public class AcademicCalendarDayValidationApi_Tests : ShikshaMitraApiTestBase
{
	public AcademicCalendarDayValidationApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
	{
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Update_Should_Return_Field_Validation_Errors_For_Oversized_Text()
	{
		using var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, TestAuthHandler.TestTenantId);
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_CalendarManagement.Edit);
		var input = new CreateCalendarDayDto
		{
			Date = new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.Zero),
			DayType = DayType.Holiday,
			Title = new string('T', AcademicCalendarConsts.CalendarDayTitleMaxLength + 1),
			Description = new string('D', AcademicCalendarConsts.CalendarDayDescriptionMaxLength + 1),
			AppliesToAll = true
		};

		var response = await client.PutAsJsonAsync(
			$"/api/academic/calendar-days/{Guid.NewGuid():D}",
			input,
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		var root = document.RootElement;
		root.GetProperty("errorCode").GetString().ShouldBe("VALIDATION_ERROR");
		var errors = root.GetProperty("errors");
		GetFieldError(errors, nameof(CreateCalendarDayDto.Title))
			.ShouldContain($"Title cannot exceed {AcademicCalendarConsts.CalendarDayTitleMaxLength} characters.");
		GetFieldError(errors, nameof(CreateCalendarDayDto.Description))
			.ShouldContain(
				$"Description cannot exceed {AcademicCalendarConsts.CalendarDayDescriptionMaxLength} characters.");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Update_Should_Reject_A_Whitespace_Only_Title()
	{
		using var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, TestAuthHandler.TestTenantId);
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_CalendarManagement.Edit);
		var input = new CreateCalendarDayDto
		{
			Date = new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.Zero),
			DayType = DayType.Holiday,
			Title = "   ",
			Description = "A readable description",
			AppliesToAll = true
		};

		var response = await client.PutAsJsonAsync(
			$"/api/academic/calendar-days/{Guid.NewGuid():D}",
			input,
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		var root = document.RootElement;
		root.GetProperty("errorCode").GetString().ShouldBe("VALIDATION_ERROR");
		GetFieldError(root.GetProperty("errors"), nameof(CreateCalendarDayDto.Title))
			.ShouldBe("Title is required.");
	}

	private static string GetFieldError(JsonElement errors, string fieldName)
	{
		var property = errors.EnumerateObject().Single(x =>
			string.Equals(x.Name, fieldName, StringComparison.OrdinalIgnoreCase));
		return property.Value.EnumerateArray().First().GetString() ?? string.Empty;
	}
}