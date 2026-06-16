using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Application.Contracts.HR.Calendar.Holidays;
using ShikshaMitra.HR;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
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
		var academicYearId = Factory.SeededAcademicYearId;
		var (startDate, endDate) = await GetHolidayRangeInsideAcademicYearAsync(academicYearId);

		var input = new CreateUpdateHolidayDto
		{
			AcademicYearId = academicYearId,
			HolidayName = TestDataHelper.UniqueName("Diwali"),
			HolidayTypeId = holidayType.Id,
			StartDate = startDate,
			EndDate = endDate,
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
	public async Task Should_Reject_Holiday_Outside_Academic_Year()
	{
		var holidayType = await CalendarTestHelper.CreateHolidayTypeAsync(Client, "HolOutsideYear");
		var academicYearId = Factory.SeededAcademicYearId;
		var input = new CreateUpdateHolidayDto
		{
			AcademicYearId = academicYearId,
			HolidayName = TestDataHelper.UniqueName("OutsideYearHoliday"),
			HolidayTypeId = holidayType.Id,
			StartDate = new DateTimeOffset(2040, 5, 30, 0, 0, 0, TimeSpan.Zero),
			EndDate = new DateTimeOffset(2040, 5, 31, 0, 0, 0, TimeSpan.Zero),
			Description = "Out of academic year",
			AudienceIds = new List<Guid>(),
		};

		var response = await Client.PostAsJsonAsync(
			$"{BaseUrl}?academicYearId={academicYearId}", input, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var raw = await response.Content.ReadAsStringAsync();
		using var document = JsonDocument.Parse(raw);
		document.RootElement.GetProperty("errorCode").GetString()
			.ShouldBe(HRErrorCodes.WorkCalendarHolidayOutsideAcademicYear);
		var detail = document.RootElement.GetProperty("detail").GetString();
		detail.ShouldNotBeNull();
		detail!.ShouldContain(input.HolidayName);
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
		var newHolidayType = await CalendarTestHelper.CreateHolidayTypeAsync(Client, "UpdHolType");
		var (startDate, endDate) = await GetHolidayRangeInsideAcademicYearAsync(
			created.AcademicYearId,
			startOffsetDays: 40);

		var updateDto = new CreateUpdateHolidayDto
		{
			AcademicYearId = created.AcademicYearId,
			HolidayName = TestDataHelper.UniqueName("UpdatedHol"),
			HolidayTypeId = newHolidayType.Id,
			StartDate = startDate,
			EndDate = endDate,
			Description = string.Empty,
			Attachments = string.Empty,
			AudienceIds = new List<Guid>(),
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{created.Id}", updateDto, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<HolidayDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.HolidayTypeId.ShouldBe(newHolidayType.Id);
		body.Description.ShouldBe(string.Empty);
		body.Attachments.ShouldBe(string.Empty);
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

		var response = await Client.GetAsync(
			$"{BaseUrl}/by-type/{created.HolidayTypeId}?SkipCount=0&MaxResultCount=10");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<PagedResultDto<HolidayDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.Any(x => x.Id == created.Id).ShouldBeTrue();
		body.Items.All(x => x.HolidayTypeId == created.HolidayTypeId).ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Holidays_By_Status()
	{
		var academicYearId = await CreateAcademicYearAsync(
			DateTime.UtcNow.Date.AddDays(-20),
			DateTime.UtcNow.Date.AddDays(60));
		var future = await CreateHolidayAsync(
			"ByStatusHol",
			ToUtcDate(DateTime.UtcNow.Date.AddDays(10)),
			ToUtcDate(DateTime.UtcNow.Date.AddDays(11)),
			academicYearId);
		var past = await CreateHolidayAsync(
			"ByStatusPastHol",
			ToUtcDate(DateTime.UtcNow.Date.AddDays(-10)),
			ToUtcDate(DateTime.UtcNow.Date.AddDays(-9)),
			academicYearId);

		var response = await Client.GetAsync($"{BaseUrl}/by-status/0?SkipCount=0&MaxResultCount=50");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<PagedResultDto<HolidayDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.Any(x => x.Id == future.Id).ShouldBeTrue();
		body.Items.Any(x => x.Id == past.Id).ShouldBeFalse();
		body.Items.All(x => x.Status == 0).ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Filter_Holiday_List_By_Status()
	{
		var academicYearId = await CreateAcademicYearAsync(
			DateTime.UtcNow.Date.AddDays(-20),
			DateTime.UtcNow.Date.AddDays(60));
		var future = await CreateHolidayAsync(
			"FutureStatusHol",
			ToUtcDate(DateTime.UtcNow.Date.AddDays(10)),
			ToUtcDate(DateTime.UtcNow.Date.AddDays(11)),
			academicYearId);
		var past = await CreateHolidayAsync(
			"PastStatusHol",
			ToUtcDate(DateTime.UtcNow.Date.AddDays(-10)),
			ToUtcDate(DateTime.UtcNow.Date.AddDays(-9)),
			academicYearId);

		var response = await Client.GetAsync(
			$"{BaseUrl}?AcademicYearId={academicYearId}&Status=0&SkipCount=0&MaxResultCount=50");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<PagedResultDto<HolidayDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.Any(x => x.Id == future.Id).ShouldBeTrue();
		body.Items.Any(x => x.Id == past.Id).ShouldBeFalse();
		body.Items.All(x => x.Status == 0).ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_404_For_Nonexistent()
	{
		var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	#endregion

	#region Helpers

	private async Task<HolidayDto> CreateHolidayAsync(
		string suffix,
		DateTimeOffset? startDate = null,
		DateTimeOffset? endDate = null,
		Guid? academicYearIdOverride = null)
	{
		var holidayType = await CalendarTestHelper.CreateHolidayTypeAsync(Client, suffix);
		var academicYearId = academicYearIdOverride ?? Factory.SeededAcademicYearId;
		var (defaultStartDate, defaultEndDate) =
			await GetHolidayRangeInsideAcademicYearAsync(academicYearId);
		var resolvedStartDate = startDate ?? defaultStartDate;
		var resolvedEndDate = endDate ?? (startDate.HasValue ? startDate.Value.AddDays(1) : defaultEndDate);

		var input = new CreateUpdateHolidayDto
		{
			AcademicYearId = academicYearId,
			HolidayName = TestDataHelper.UniqueName(suffix),
			HolidayTypeId = holidayType.Id,
			StartDate = resolvedStartDate,
			EndDate = resolvedEndDate,
			Description = "Original holiday description",
			Attachments = "holiday-a.pdf",
			AudienceIds = new List<Guid>(),
		};

		var response = await Client.PostAsJsonAsync(
			$"{BaseUrl}?academicYearId={academicYearId}", input, JsonOptions);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<HolidayDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create holiday");
	}

	private async Task<(DateTimeOffset StartDate, DateTimeOffset EndDate)> GetHolidayRangeInsideAcademicYearAsync(
		Guid academicYearId,
		int startOffsetDays = 30)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var academicYearRepository = scope.ServiceProvider.GetRequiredService<IRepository<AcademicYear, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using var tenant = currentTenant.Change(tenantId);
		var academicYear = await academicYearRepository.GetAsync(academicYearId);
		var startDate = academicYear.StartDate.Date.AddDays(startOffsetDays);
		var endDate = startDate.AddDays(1);
		if (endDate.Date > academicYear.EndDate.Date)
		{
			endDate = academicYear.EndDate.Date;
			startDate = endDate.AddDays(-1);
		}

		return (ToUtcDate(startDate), ToUtcDate(endDate));
	}

	private async Task<Guid> CreateAcademicYearAsync(DateTime startDate, DateTime endDate)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var academicYearRepository = scope.ServiceProvider.GetRequiredService<IRepository<AcademicYear, Guid>>();
		var academicYearManager = scope.ServiceProvider.GetRequiredService<AcademicYearManager>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var suffix = Guid.NewGuid().ToString("N")[..8];

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var academicYear = await academicYearManager.CreateAsync(
				$"HolidayStatus-{suffix}",
				startDate,
				endDate);
			await academicYearRepository.InsertAsync(academicYear, autoSave: false);
			await uow.CompleteAsync();
			return academicYear.Id;
		}
	}

	private static DateTimeOffset ToUtcDate(DateTime value) =>
		new(DateTime.SpecifyKind(value.Date, DateTimeKind.Utc));

	#endregion
}
