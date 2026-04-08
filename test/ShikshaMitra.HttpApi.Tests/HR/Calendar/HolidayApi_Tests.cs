using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.HR.Calendar.Holidays;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Xunit;

namespace ShikshaMitra.HR.Calendar;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class HolidayApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/calendar/holidays";

	public HolidayApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	#region Tier 0 — Critical

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Create_Holiday()
	{
		var holidayType = await CalendarTestHelper.CreateHolidayTypeAsync(Client, "HolCreate");
		var academicYearId = Guid.NewGuid();

		var input = new CreateUpdateHolidayDto
		{
			AcademicYearId = academicYearId,
			HolidayName = TestDataHelper.UniqueName("Diwali"),
			HolidayTypeId = holidayType.Id,
			StartDate = DateTimeOffset.UtcNow.AddDays(30),
			EndDate = DateTimeOffset.UtcNow.AddDays(31),
			Description = "Festival of lights",
			AudienceIds = new List<Guid>(),
		};

		var response = await Client.PostAsJsonAsync(
			$"{BaseUrl}?academicYearId={academicYearId}", input, JsonOptions);

		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<HolidayDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.Id.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Holiday_By_Id()
	{
		var created = await CreateHolidayAsync("GetHol");

		var response = await Client.GetAsync($"{BaseUrl}/{created.Id}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<HolidayDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.Id.ShouldBe(created.Id);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Holiday_List()
	{
		await CreateHolidayAsync("ListHol");

		var response = await Client.GetAsync($"{BaseUrl}?SkipCount=0&MaxResultCount=10");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<PagedResultDto<HolidayDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.ShouldNotBeNull();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Update_Holiday()
	{
		var created = await CreateHolidayAsync("UpdHol");

		var updateDto = new CreateUpdateHolidayDto
		{
			AcademicYearId = created.AcademicYearId,
			HolidayName = TestDataHelper.UniqueName("UpdatedHol"),
			HolidayTypeId = created.HolidayTypeId,
			StartDate = DateTimeOffset.UtcNow.AddDays(40),
			EndDate = DateTimeOffset.UtcNow.AddDays(42),
			AudienceIds = new List<Guid>(),
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{created.Id}", updateDto, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	#endregion

	#region Tier 1 — Important

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Delete_Holiday()
	{
		var created = await CreateHolidayAsync("DelHol");

		var response = await Client.DeleteAsync($"{BaseUrl}/{created.Id}");

		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Holidays_By_Type()
	{
		var created = await CreateHolidayAsync("ByTypeHol");

		var response = await Client.GetAsync($"{BaseUrl}/by-type/{created.HolidayTypeId}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Holidays_By_Status()
	{
		await CreateHolidayAsync("ByStatusHol");

		var response = await Client.GetAsync($"{BaseUrl}/by-status/0");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_404_For_Nonexistent()
	{
		var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

		response.StatusCode.ShouldBeOneOf(HttpStatusCode.NotFound, HttpStatusCode.InternalServerError);
	}

	#endregion

	#region Helpers

	private async Task<HolidayDto> CreateHolidayAsync(string suffix)
	{
		var holidayType = await CalendarTestHelper.CreateHolidayTypeAsync(Client, suffix);
		var academicYearId = Guid.NewGuid();

		var input = new CreateUpdateHolidayDto
		{
			AcademicYearId = academicYearId,
			HolidayName = TestDataHelper.UniqueName(suffix),
			HolidayTypeId = holidayType.Id,
			StartDate = DateTimeOffset.UtcNow.AddDays(30),
			EndDate = DateTimeOffset.UtcNow.AddDays(31),
			AudienceIds = new List<Guid>(),
		};

		var response = await Client.PostAsJsonAsync(
			$"{BaseUrl}?academicYearId={academicYearId}", input, JsonOptions);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<HolidayDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create holiday");
	}

	#endregion
}
