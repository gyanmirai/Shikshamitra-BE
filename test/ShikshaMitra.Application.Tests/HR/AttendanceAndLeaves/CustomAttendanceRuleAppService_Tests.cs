using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class CustomAttendanceRuleAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly Guid TestTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

	private readonly ICustomAttendanceRuleAppService _customAttendanceRuleAppService;
	private readonly ICurrentTenant _currentTenant;

	public CustomAttendanceRuleAppService_Tests()
	{
		_customAttendanceRuleAppService = GetRequiredService<ICustomAttendanceRuleAppService>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
	}

	private static CreateUpdateCustomAttendanceRuleDto BuildValidInput(string name) =>
		new()
		{
			Name = name,
			Type = EmployeeAttendanceStatusType.Present,
			LateThreshold = 10,
			LateThresholdUnit = TimeUnitType.Minutes,
			HalfDayThreshold = 4,
			HalfDayThresholdUnit = TimeUnitType.Hours,
			EnableNotificationForUpperAuthority = true
		};

	[Fact]
	public async Task Should_Create_Custom_Attendance_Rule()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var input = BuildValidInput("Late arrival rule");

			var result = await _customAttendanceRuleAppService.CreateAsync(input);

			result.ShouldNotBeNull();
			result.Id.ShouldNotBe(Guid.Empty);
			result.Name.ShouldBe(input.Name);
			result.Type.ShouldBe(input.Type);
			result.LateThreshold.ShouldBe(input.LateThreshold);
			result.LateThresholdUnit.ShouldBe(input.LateThresholdUnit);
			result.HalfDayThreshold.ShouldBe(input.HalfDayThreshold);
			result.HalfDayThresholdUnit.ShouldBe(input.HalfDayThresholdUnit);
			result.EnableNotificationForUpperAuthority.ShouldBe(input.EnableNotificationForUpperAuthority);
		}
	}

	[Fact]
	public async Task Should_Get_Custom_Attendance_Rule_By_Id()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var created = await _customAttendanceRuleAppService.CreateAsync(BuildValidInput("Get by id rule"));

			var result = await _customAttendanceRuleAppService.GetAsync(created.Id);

			result.Id.ShouldBe(created.Id);
			result.Name.ShouldBe("Get by id rule");
		}
	}

	[Fact]
	public async Task Should_List_Include_Created_Rule()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var created = await _customAttendanceRuleAppService.CreateAsync(BuildValidInput("Listed rule"));

			var list = await _customAttendanceRuleAppService.GetListAsync(new PagedAndSortedResultRequestDto());

			list.Items.Any(x => x.Id == created.Id).ShouldBeTrue();
		}
	}

	[Fact]
	public async Task Should_Update_Custom_Attendance_Rule()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var created = await _customAttendanceRuleAppService.CreateAsync(BuildValidInput("Original name"));

			var update = BuildValidInput("Updated name");
			update.Type = EmployeeAttendanceStatusType.HalfDayLeave;
			update.LateThreshold = 20;
			update.EnableNotificationForUpperAuthority = false;

			var result = await _customAttendanceRuleAppService.UpdateAsync(created.Id, update);

			result.Name.ShouldBe("Updated name");
			result.Type.ShouldBe(EmployeeAttendanceStatusType.HalfDayLeave);
			result.LateThreshold.ShouldBe(20);
			result.EnableNotificationForUpperAuthority.ShouldBeFalse();

			var loaded = await _customAttendanceRuleAppService.GetAsync(created.Id);
			loaded.Name.ShouldBe("Updated name");
		}
	}

	[Fact]
	public async Task Should_Delete_Custom_Attendance_Rule()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var created = await _customAttendanceRuleAppService.CreateAsync(BuildValidInput("To delete"));

			await _customAttendanceRuleAppService.DeleteAsync(created.Id);

			await Should.ThrowAsync<EntityNotFoundException>(async () =>
				await _customAttendanceRuleAppService.GetAsync(created.Id));
		}
	}

	[Fact]
	public async Task Should_Not_Create_With_Empty_Name()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var input = BuildValidInput("");
			input.Name = "";

			await Should.ThrowAsync<AbpValidationException>(async () =>
				await _customAttendanceRuleAppService.CreateAsync(input));
		}
	}

	[Fact]
	public async Task Should_Throw_When_Get_Unknown_Id()
	{
		await Should.ThrowAsync<EntityNotFoundException>(async () =>
			await _customAttendanceRuleAppService.GetAsync(Guid.NewGuid()));
	}
}
