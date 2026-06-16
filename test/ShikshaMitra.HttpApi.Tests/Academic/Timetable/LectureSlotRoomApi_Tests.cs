using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.Settings.LectureSlots;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Academic.Timetable;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Academic)]
public class LectureSlotRoomApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/academics/timetable/settings/lecture-slots";

	public LectureSlotRoomApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Create_Lecture_Slot_With_Room_And_Return_Room_Name()
	{
		var standardIds = await GetAvailableStandardIdsAsync(1);
		var standardId = standardIds[0];
		var room = await GetSeededRoomAsync();

		var response = await Client.PostAsJsonAsync(
			BaseUrl,
			BuildCreateDto(standardId, room.Id),
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<LectureSlotDto>(JsonOptions);
		body.ShouldNotBeNull();
		var timeSlot = body.SlotConfigurations.Single().TimeSlots.Single();
		timeSlot.RoomId.ShouldBe(room.Id);
		timeSlot.RoomName.ShouldBe(room.RoomName);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Reject_Lecture_Slot_With_Invalid_Room()
	{
		var standardIds = await GetAvailableStandardIdsAsync(1, skip: 10);
		var standardId = standardIds[0];

		var response = await Client.PostAsJsonAsync(
			BaseUrl,
			BuildCreateDto(standardId, Guid.NewGuid()),
			JsonOptions);

		response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Copy_Room_When_Assigning_Lecture_Slot_Template()
	{
		var standardIds = await GetAvailableStandardIdsAsync(2, skip: 20);
		var sourceStandardId = standardIds[0];
		var targetStandardId = standardIds[1];
		var room = await GetSeededRoomAsync();

		var createResponse = await Client.PostAsJsonAsync(
			BaseUrl,
			BuildCreateDto(sourceStandardId, room.Id),
			JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var source = await createResponse.Content.ReadFromJsonAsync<LectureSlotDto>(JsonOptions);
		source.ShouldNotBeNull();

		var assignResponse = await Client.PostAsJsonAsync(
			$"{BaseUrl}/{source.Id}/assign",
			new AssignLectureSlotDto { SelectedStandardIds = [targetStandardId] },
			JsonOptions);
		assignResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var targetConfigId = await GetLectureSlotConfigIdForStandardAsync(targetStandardId);
		var targetResponse = await Client.GetAsync($"{BaseUrl}/{targetConfigId}");
		targetResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var target = await targetResponse.Content.ReadFromJsonAsync<LectureSlotDto>(JsonOptions);

		target.ShouldNotBeNull();
		var targetTimeSlot = target.SlotConfigurations.Single().TimeSlots.Single();
		targetTimeSlot.RoomId.ShouldBe(room.Id);
		targetTimeSlot.RoomName.ShouldBe(room.RoomName);
	}

	private static CreateLectureSlotDto BuildCreateDto(Guid standardId, Guid roomId) => new()
	{
		StandardId = standardId,
		SchoolHours = "08:00-14:00",
		SlotConfigurations =
		[
			new SlotConfigurationDto
			{
				TotalSlots = 1,
				WeekRange = [1, 5],
				TimeSlots =
				[
					new TimeSlotDto
					{
						StartTime = new TimeOnly(8, 0),
						EndTime = new TimeOnly(8, 45),
						SlotType = SlotType.Period,
						RoomId = roomId
					}
				]
			}
		]
	};

	private async Task<List<Guid>> GetAvailableStandardIdsAsync(int count, int skip = 0)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var standardRepository = scope.ServiceProvider.GetRequiredService<IRepository<Standard, Guid>>();
		var lectureSlotRepository = scope.ServiceProvider.GetRequiredService<IRepository<LectureSlotConfig, Guid>>();
		var dataFilter = scope.ServiceProvider.GetRequiredService<IDataFilter>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		using (dataFilter.Disable<ISoftDelete>())
		{
			var configuredStandardIds = (await lectureSlotRepository.GetQueryableAsync())
				.Select(config => config.StandardId)
				.ToHashSet();

			var standards = (await standardRepository.GetQueryableAsync())
				.OrderBy(standard => standard.Id)
				.Select(standard => standard.Id)
				.ToList()
				.Where(standardId => !configuredStandardIds.Contains(standardId))
				.Skip(skip)
				.Take(count)
				.ToList();
			await uow.CompleteAsync();
			standards.Count.ShouldBeGreaterThanOrEqualTo(count);
			return standards;
		}
	}

	private async Task<Room> GetSeededRoomAsync()
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var roomRepository = scope.ServiceProvider.GetRequiredService<IRepository<Room, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var room = (await roomRepository.GetQueryableAsync())
				.OrderBy(item => item.RoomName)
				.FirstOrDefault();
			await uow.CompleteAsync();
			return room ?? throw new InvalidOperationException("Expected seeded room management data for lecture slot tests.");
		}
	}

	private async Task<Guid> GetLectureSlotConfigIdForStandardAsync(Guid standardId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider.GetRequiredService<ILectureSlotConfigRepository>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var config = await repository.FindByStandardIdAsync(standardId)
			             ?? throw new InvalidOperationException($"Lecture slot config not found for standard {standardId}");
			await uow.CompleteAsync();
			return config.Id;
		}
	}
}
