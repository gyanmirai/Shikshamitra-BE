using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryHoursCapabilitiesApi_Tests(LibraryApiTestFactory factory)
	: ShikshaMitraApiTestBase(factory), IClassFixture<LibraryApiTestFactory>
{
	[Theory]
	[InlineData(true, true)]
	[InlineData(true, false)]
	[InlineData(false, true)]
	public async Task Hours_Http_Capability_Should_Match_Exact_Add_And_Actual_Schoolwide_Scope(bool wide, bool add)
	{
		var (year, actor) = await SeedHoursContextAsync(wide);
		using var client = HoursClient(actor, view: true, add);
		var route = $"/api/library/library-hours?academicYearId={year:D}";
		using var read = await client.GetAsync(route);
		read.StatusCode.ShouldBe(HttpStatusCode.OK, await read.Content.ReadAsStringAsync());
		var result = await read.Content.ReadFromJsonAsync<LibraryHoursListDto>(JsonOptions);
		result.ShouldNotBeNull();
		result.TotalCount.ShouldBe(1);
		result.CanManageHours.ShouldBe(wide && add);
		using var saved = await client.PostAsJsonAsync(route, HoursRequest(), JsonOptions);
		saved.StatusCode.ShouldBe(!add ? HttpStatusCode.Forbidden : wide ? HttpStatusCode.OK : HttpStatusCode.BadRequest,
			await saved.Content.ReadAsStringAsync());
		var current = await client.GetFromJsonAsync<LibraryHoursListDto>(route, JsonOptions);
		current.ShouldNotBeNull();
		current.Items.ShouldHaveSingleItem().StartTime.ShouldBe(wide && add ? "11:45:00" : "09:00:00");
	}

	[Fact]
	public async Task Hours_Http_Capability_Should_Not_Expose_Read_Data_From_An_Add_Grant_Alone()
	{
		var (year, actor) = await SeedHoursContextAsync(true);
		using var client = HoursClient(actor, view: false, add: true);
		using var response = await client.GetAsync($"/api/library/library-hours?academicYearId={year:D}");
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	private static LibraryHoursUpdateRequest HoursRequest() => new()
	{
		Hours = [new() { Day = "Monday", Status = 0, StartTime = "11:45:00", EndTime = "15:00:00" }]
	};
}
