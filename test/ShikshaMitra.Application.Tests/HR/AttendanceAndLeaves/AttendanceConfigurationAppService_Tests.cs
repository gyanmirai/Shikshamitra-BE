using System;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using Shouldly;
using Volo.Abp;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class AttendanceConfigurationAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly Guid TestTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
	private static readonly Guid AcademicYearId = Guid.Parse("22222222-2222-2222-2222-222222222222");

	private readonly IAttendanceConfigurationAppService _appService;
	private readonly IAttendanceConfigurationRepository _repository;
	private readonly ICurrentTenant _currentTenant;

	public AttendanceConfigurationAppService_Tests()
	{
		_appService = GetRequiredService<IAttendanceConfigurationAppService>();
		_repository = GetRequiredService<IAttendanceConfigurationRepository>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
	}

	[Fact]
	public async Task Should_Create_Attendance_Configuration()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var response = await _appService.CreateOrUpdateAsync(AcademicYearId, BuildValidRequest());

			response.Success.ShouldBeTrue();
			response.Message.ShouldBe("Attendance configuration saved successfully");

			var saved = await _repository.FindByAcademicYearIdAsync(AcademicYearId);
			saved.ShouldNotBeNull();
			saved.AcademicYearId.ShouldBe(AcademicYearId);
			saved.HoursBasedPayPercentage.ShouldBe(80);
			saved.GraceTime.ShouldBe(15);
			saved.MaxLateAllowedPerMonth.ShouldBe(3);
			saved.PenaltyType.ShouldBe(AttendancePenaltyType.DeductHalfDay);
			saved.OvertimeEnabled.ShouldBeTrue();
			saved.OvertimeRate.ShouldBe(AttendanceOvertimeRate.OnePointFiveX);
			saved.MinOvertimeMinutes.ShouldBe(30);
		}
	}

	[Fact]
	public async Task Should_Upsert_Attendance_Configuration()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			await _appService.CreateOrUpdateAsync(AcademicYearId, BuildValidRequest());

			var update = BuildValidRequest();
			update.DailyRules.HoursBasedPayPercentage = 90;
			update.LateEarlyRules.GraceTime = 20;
			update.LateEarlyRules.MaxLateAllowedPerMonth = 5;
			update.LateEarlyRules.PenaltyType = "deduct_full_day";
			update.Overtime.Enabled = false;
			update.Overtime.Rate = null;
			update.Overtime.MinOvertimeMinutes = null;

			await _appService.CreateOrUpdateAsync(AcademicYearId, update);

			var saved = await _repository.FindByAcademicYearIdAsync(AcademicYearId);
			saved.ShouldNotBeNull();
			saved.HoursBasedPayPercentage.ShouldBe(90);
			saved.GraceTime.ShouldBe(20);
			saved.MaxLateAllowedPerMonth.ShouldBe(5);
			saved.PenaltyType.ShouldBe(AttendancePenaltyType.DeductFullDay);
			saved.OvertimeEnabled.ShouldBeFalse();
			saved.OvertimeRate.ShouldBeNull();
			saved.MinOvertimeMinutes.ShouldBeNull();
		}
	}

	[Fact]
	public async Task Should_Require_Overtime_Details_When_Enabled()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var invalid = BuildValidRequest();
			invalid.Overtime.Enabled = true;
			invalid.Overtime.Rate = null;
			invalid.Overtime.MinOvertimeMinutes = null;

			var ex = await Should.ThrowAsync<BusinessException>(() =>
				_appService.CreateOrUpdateAsync(Guid.NewGuid(), invalid));

			ex.Code.ShouldBe("ShikshaMitra:OvertimeRateAndMinMinutesRequired");
		}
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
}
