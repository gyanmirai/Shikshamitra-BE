using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class AutomatedNotificationsApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/attendance/automated-notifications";

	public AutomatedNotificationsApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	#region Tier 0 — Critical

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Create_Notification_Setting()
	{
		var input = new AutomatedNotificationsSettingsDto
		{
			EmployeeAttendanceStatusType = EmployeeAttendanceStatusType.FullDayLeave,
			NotificationMethods = new List<NotificationMethodType>
			{
				NotificationMethodType.Email,
				NotificationMethodType.SMS,
			},
		};

		var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.Created);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Notification_Setting_List()
	{
		var response = await Client.GetAsync($"{BaseUrl}?SkipCount=0&MaxResultCount=10");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<PagedResultDto<AutomatedNotificationsSettingsDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.ShouldNotBeNull();
	}

	#endregion

	#region Tier 1 — Important

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Update_Notification_Setting()
	{
		var created = await CreateSettingAsync();

		var updateDto = new AutomatedNotificationsSettingsDto
		{
			EmployeeAttendanceStatusType = EmployeeAttendanceStatusType.HalfDayLeave,
			NotificationMethods = new List<NotificationMethodType>
			{
				NotificationMethodType.PushNotification,
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{created.Id}", updateDto, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Delete_Notification_Setting()
	{
		var created = await CreateSettingAsync();

		var response = await Client.DeleteAsync($"{BaseUrl}/{created.Id}");

		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	#endregion

	#region Helpers

	private async Task<AutomatedNotificationsSettingsDto> CreateSettingAsync()
	{
		var input = new AutomatedNotificationsSettingsDto
		{
			EmployeeAttendanceStatusType = EmployeeAttendanceStatusType.Present,
			NotificationMethods = new List<NotificationMethodType>
			{
				NotificationMethodType.Email,
			},
		};

		var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<AutomatedNotificationsSettingsDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create notification setting");
	}

	#endregion
}
