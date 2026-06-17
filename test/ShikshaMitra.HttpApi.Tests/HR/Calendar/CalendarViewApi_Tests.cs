using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.HR.Calendar.CalendarView;
using ShikshaMitra.Application.Contracts.HR.Calendar.Events;
using ShikshaMitra.Application.Contracts.HR.Calendar.Holidays;
using ShikshaMitra.HR.Calendar.Events;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Calendar;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class CalendarViewApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/calendar";

	public CalendarViewApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	#region Tier 0 - Critical

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Calendar_View()
	{
		var academicYearId = Guid.NewGuid();

		var response = await Client.GetAsync(
			$"{BaseUrl}/view?academicYearId={academicYearId}&page=1&pageSize=10");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	#endregion

	#region Tier 1 - Important

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Place_Event_On_Persisted_Date_Range()
	{
		var academicYearId = Factory.SeededAcademicYearId;
		var startDate = DateTimeOffset.UtcNow.AddDays(9);
		var endDate = DateTimeOffset.UtcNow.AddDays(10);
		var created = await CreateEventAsync("CalViewDateRange", startDate, endDate);

		var response = await Client.GetAsync(
			$"{BaseUrl}/view?academicYearId={academicYearId}&SkipCount=0&MaxResultCount=100");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<PagedResultDto<CalendarViewItemDto>>(JsonOptions);
		body.ShouldNotBeNull();
		var item = body.Items.Single(x => x.Id == created.Id);
		item.StartDate.Date.ShouldBe(startDate.Date);
		item.EndDate.Date.ShouldBe(endDate.Date);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Filter_Calendar_View_By_Multiple_Statuses()
	{
		var academicYearId = Factory.SeededAcademicYearId;
		var activeEvent = await CreateEventAsync("CalViewActiveStatus");
		var futureHoliday = await CreateHolidayAsync(
			"CalViewFutureStatus",
			DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14),
			DateOnly.FromDateTime(DateTime.UtcNow).AddDays(15));
		var completedHoliday = await CreateHolidayAsync(
			"CalViewCompletedStatus",
			DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-14),
			DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-13));

		var response = await Client.GetAsync(
			$"{BaseUrl}/view?academicYearId={academicYearId}&Statuses=0&Statuses=2&SkipCount=0&MaxResultCount=100");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<PagedResultDto<CalendarViewItemDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.Any(x => x.Id == futureHoliday.Id).ShouldBeTrue();
		body.Items.Any(x => x.Id == completedHoliday.Id).ShouldBeTrue();
		body.Items.Any(x => x.Id == activeEvent.Id).ShouldBeFalse();
		body.Items.All(x => x.Status is 0 or 2).ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Scoped_User_Without_Group_Should_Only_See_Public_Calendar_Items()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var academicYearId = Factory.SeededAcademicYearId;
		var marker = $"CalScope{Guid.NewGuid():N}";
		var publicEvent = await CreateEventAsync($"{marker}Public");
		var targetedEvent = await CreateEventAsync($"{marker}Targeted", audienceIds: [Guid.NewGuid()]);
		var scopedUserId = Guid.NewGuid();
		await EnsureUserHasStageScopeAsync(scopedUserId, EmployeeTestHelper.DefaultMediumStageId);
		var scopedClient = CreateClientAs(scopedUserId);

		var response = await scopedClient.GetAsync(
			$"{BaseUrl}/view?academicYearId={academicYearId}&search={Uri.EscapeDataString(marker)}&SkipCount=0&MaxResultCount=100");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var body = await response.Content.ReadFromJsonAsync<PagedResultDto<CalendarViewItemDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.Any(x => x.Id == publicEvent.Id).ShouldBeTrue();
		body.Items.Any(x => x.Id == targetedEvent.Id).ShouldBeFalse();
		body.Items.ShouldAllBe(x => x.AudienceCount == 0);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Item_Details_Returns_404_For_Nonexistent()
	{
		var response = await Client.GetAsync($"{BaseUrl}/items/{Guid.NewGuid()}");

		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	#endregion

	#region Helpers

	private async Task<EventDto> CreateEventAsync(
		string suffix,
		DateTimeOffset? startDate = null,
		DateTimeOffset? endDate = null,
		List<Guid>? audienceIds = null)
	{
		var eventType = await CalendarTestHelper.CreateEventTypeAsync(Client, suffix);
		var academicYearId = Factory.SeededAcademicYearId;

		var input = new CreateUpdateEventDto
		{
			AcademicYearId = academicYearId,
			Title = TestDataHelper.UniqueName(suffix),
			EventTypeId = eventType.Id,
			Mode = EventMode.Offline,
			Venue = "Calendar View Test Venue",
			StartDate = startDate ?? DateTimeOffset.UtcNow,
			EndDate = endDate ?? startDate ?? DateTimeOffset.UtcNow,
			StartTime = "09:00",
			EndTime = "10:00",
			IsRecurring = false,
			AudienceIds = audienceIds ?? [],
		};

		var response = await Client.PostAsJsonAsync(
			$"{BaseUrl}/events?academicYearId={academicYearId}", input, JsonOptions);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<EventDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create event");
	}

	private async Task<HolidayDto> CreateHolidayAsync(
		string suffix,
		DateOnly startDate,
		DateOnly endDate)
	{
		var holidayType = await CalendarTestHelper.CreateHolidayTypeAsync(Client, suffix);
		var academicYearId = Factory.SeededAcademicYearId;

		var input = new CreateUpdateHolidayDto
		{
			AcademicYearId = academicYearId,
			HolidayName = TestDataHelper.UniqueName(suffix),
			HolidayTypeId = holidayType.Id,
			StartDate = startDate,
			EndDate = endDate,
			AudienceIds = new List<Guid>(),
		};

		var response = await Client.PostAsJsonAsync(
			$"{BaseUrl}/holidays?academicYearId={academicYearId}", input, JsonOptions);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<HolidayDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create holiday");
	}

	private async Task EnsureUserHasStageScopeAsync(Guid userId, Guid stageId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var scopeRepository = scope.ServiceProvider.GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		var userScopeService = scope.ServiceProvider.GetRequiredService<IUserScopeService>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			await scopeRepository.InsertAsync(
				new UserScopeAssignment(
					Guid.NewGuid(),
					tenantId,
					userId: userId,
					educationMediumStageId: stageId,
					description: "Calendar view scoped API regression"),
				autoSave: false);
			await userScopeService.InvalidateUserScopeCacheAsync(userId);
			await uow.CompleteAsync();
		}
	}

	#endregion
}
