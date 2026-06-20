using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.Groups;
using ShikshaMitra.Application.Contracts.HR.Calendar.Events;
using ShikshaMitra.Application.Contracts.HR.Calendar.Holidays;
using ShikshaMitra.Common;
using ShikshaMitra.Groups;
using ShikshaMitra.HR.Calendar;
using ShikshaMitra.HR.Calendar.Events;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Xunit;

namespace ShikshaMitra.Communication;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Communication)]
public class CalendarCommunicationApi_Tests : ShikshaMitraApiTestBase
{
	private const string ProviderSettingsUrl = "/api/communication/provider-settings";
	private const string ApprovalRulesUrl = "/api/communication/approval-rules";

	public CalendarCommunicationApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Event_Announcement_Should_Publish_Through_Communication()
	{
		await CreateProviderAsync(CommunicationChannelType.InApp);
		var group = await CreateEmployeeGroupAsync("CalEvtGroup");
		var calendarEvent = await CreateEventAsync(group.Id);

		var result = await PostAndReadAsync<CalendarCommunicationResultDto>(
			$"/api/calendar/events/{calendarEvent.Id}/communication/announcement",
			new CalendarCommunicationRequestDto
			{
				Channels = { CommunicationChannelType.InApp },
				PublishImmediately = true
			});

		result.Published.ShouldBeTrue();
		result.SourceEntityType.ShouldBe("CalendarEvent");
		result.SourceEntityId.ShouldBe(calendarEvent.Id);
		result.Campaign.SourceModule.ShouldBe(CommunicationSourceModule.Calendar);
		result.Campaign.SourceEntityType.ShouldBe("CalendarEvent");
		result.Campaign.SourceEntityId.ShouldBe(calendarEvent.Id);
		result.Campaign.SourceReference.ShouldBe(calendarEvent.Title);
		result.Campaign.Status.ShouldBe(CommunicationCampaignStatus.Queued);
		result.Campaign.QueuedCount.ShouldBe(1);

		var attempts = await GetAttemptsAsync(result.Campaign.Id);
		attempts.TotalCount.ShouldBe(1);
		attempts.Items[0].Status.ShouldBe(CommunicationDeliveryStatus.Queued);
		attempts.Items[0].ProviderMessageId.ShouldStartWith("queued:");

		var usage = await GetUsageAsync(result.Campaign.Id);
		usage.TotalCount.ShouldBe(1);
		usage.Items[0].AttemptCount.ShouldBe(1);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Holiday_Announcement_Should_Publish_Through_Communication()
	{
		await CreateProviderAsync(CommunicationChannelType.InApp);
		var group = await CreateEmployeeGroupAsync("CalHolGroup");
		var holiday = await CreateHolidayAsync(group.Id);

		var result = await PostAndReadAsync<CalendarCommunicationResultDto>(
			$"/api/calendar/holidays/{holiday.Id}/communication/announcement",
			new CalendarCommunicationRequestDto
			{
				Channels = { CommunicationChannelType.InApp },
				PublishImmediately = true
			});

		result.Published.ShouldBeTrue();
		result.SourceEntityType.ShouldBe("CalendarHoliday");
		result.SourceEntityId.ShouldBe(holiday.Id);
		result.Campaign.SourceModule.ShouldBe(CommunicationSourceModule.Calendar);
		result.Campaign.SourceEntityType.ShouldBe("CalendarHoliday");
		result.Campaign.SourceEntityId.ShouldBe(holiday.Id);
		result.Campaign.SourceReference.ShouldBe(holiday.HolidayName);
		result.Campaign.Status.ShouldBe(CommunicationCampaignStatus.Queued);
		result.Campaign.QueuedCount.ShouldBe(1);
		result.Campaign.Attachments.Select(x => x.Url).ShouldContain("holiday-a.pdf");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Calendar_Approval_Rule_Should_Block_Immediate_Publish()
	{
		await CreateProviderAsync(CommunicationChannelType.InApp);
		await CreateCriticalCalendarApprovalRuleAsync();
		var group = await CreateEmployeeGroupAsync("CalApproval");
		var calendarEvent = await CreateEventAsync(group.Id);

		var result = await PostAndReadAsync<CalendarCommunicationResultDto>(
			$"/api/calendar/events/{calendarEvent.Id}/communication/announcement",
			new CalendarCommunicationRequestDto
			{
				ImportanceLevel = CommunicationImportanceLevel.Critical,
				Channels = { CommunicationChannelType.InApp },
				PublishImmediately = true
			});

		result.Published.ShouldBeFalse();
		result.ApprovalRequired.ShouldBeTrue();
		result.Campaign.Status.ShouldBe(CommunicationCampaignStatus.PendingApproval);

		var attempts = await GetAttemptsAsync(result.Campaign.Id);
		attempts.TotalCount.ShouldBe(0);
	}

	private async Task<ChannelProviderSettingDto> CreateProviderAsync(CommunicationChannelType channel)
	{
		var response = await Client.PostAsJsonAsync(
			ProviderSettingsUrl,
			new CreateUpdateChannelProviderSettingDto
			{
				Channel = channel,
				ProviderName = $"Calendar Test {channel} {Guid.NewGuid():N}",
				IsEnabled = true,
				IsDefault = true,
				ConsentRequired = false,
				TemplateApprovalRequired = false,
				Status = CommunicationProviderStatus.Enabled
			},
			JsonOptions);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<ChannelProviderSettingDto>(JsonOptions)
			?? throw new InvalidOperationException("Provider setting response was empty.");
	}

	private async Task CreateCriticalCalendarApprovalRuleAsync()
	{
		var response = await Client.PostAsJsonAsync(
			ApprovalRulesUrl,
			new CreateUpdateCommunicationApprovalRuleDto
			{
				RuleName = $"Critical calendar approval {Guid.NewGuid():N}",
				IsEnabled = true,
				CampaignType = "Announcement",
				SourceModule = CommunicationSourceModule.Calendar,
				Channel = CommunicationChannelType.InApp,
				MinimumAudienceSize = 1,
				ImportanceLevel = CommunicationImportanceLevel.Critical,
				RequiresApproval = true
			},
			JsonOptions);
		response.EnsureSuccessStatusCode();
	}

	private async Task<EventDto> CreateEventAsync(Guid audienceGroupId)
	{
		var eventType = await CalendarTestHelper.CreateEventTypeAsync(Client, "CalCommEvt");
		var input = new CreateUpdateEventDto
		{
			AcademicYearId = Factory.SeededAcademicYearId,
			Title = TestDataHelper.UniqueName("CalCommEvt"),
			EventTypeId = eventType.Id,
			Mode = EventMode.Offline,
			Venue = "School Auditorium",
			StartDate = DateTimeOffset.UtcNow.AddDays(3),
			EndDate = DateTimeOffset.UtcNow.AddDays(3),
			StartTime = "09:00",
			EndTime = "10:00",
			IsRecurring = false,
			AudienceIds = new List<Guid> { audienceGroupId }
		};

		var response = await Client.PostAsJsonAsync(
			$"/api/calendar/events?academicYearId={Factory.SeededAcademicYearId}",
			input,
			JsonOptions);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<EventDto>(JsonOptions)
			?? throw new InvalidOperationException("Event response was empty.");
	}

	private async Task<HolidayDto> CreateHolidayAsync(Guid audienceGroupId)
	{
		var holidayType = await CalendarTestHelper.CreateHolidayTypeAsync(Client, "CalCommHol");
		var input = new CreateUpdateHolidayDto
		{
			AcademicYearId = Factory.SeededAcademicYearId,
			HolidayName = TestDataHelper.UniqueName("CalCommHol"),
			HolidayTypeId = holidayType.Id,
			StartDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10),
			EndDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10),
			Description = "Calendar communication holiday",
			Attachments = "holiday-a.pdf",
			AudienceIds = new List<Guid> { audienceGroupId }
		};

		var response = await Client.PostAsJsonAsync(
			$"/api/calendar/holidays?academicYearId={Factory.SeededAcademicYearId}",
			input,
			JsonOptions);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<HolidayDto>(JsonOptions)
			?? throw new InvalidOperationException("Holiday response was empty.");
	}

	private async Task<GroupDto> CreateEmployeeGroupAsync(string suffix)
	{
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, suffix);
		employee.UserId.ShouldNotBeNull();

		var input = new CreateUpdateGroupDto
		{
			Name = $"CalComm{Guid.NewGuid():N}"[..20],
			NameBased = new NameBasedSelectionDto
			{
				SelectedMembers =
				{
					new CreateGroupMemberDto
					{
						MemberType = GroupMemberType.Employee,
						MemberId = employee.Id
					}
				}
			}
		};

		var response = await Client.PostAsJsonAsync(
			$"/api/groups?academicYearId={Factory.SeededAcademicYearId}",
			input,
			JsonOptions);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<GroupDto>(JsonOptions)
			?? throw new InvalidOperationException("Group response was empty.");
	}

	private async Task<PagedResultDto<DeliveryAttemptDto>> GetAttemptsAsync(Guid campaignId)
	{
		return await GetAndReadAsync<PagedResultDto<DeliveryAttemptDto>>(
			$"/api/communication/delivery-attempts?campaignId={campaignId}&maxResultCount=20");
	}

	private async Task<PagedResultDto<UsageLedgerEntryDto>> GetUsageAsync(Guid campaignId)
	{
		return await GetAndReadAsync<PagedResultDto<UsageLedgerEntryDto>>(
			$"/api/communication/usage-ledger?campaignId={campaignId}&maxResultCount=20");
	}
}
