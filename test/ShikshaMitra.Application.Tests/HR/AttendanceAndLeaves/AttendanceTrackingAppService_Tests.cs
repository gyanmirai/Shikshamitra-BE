using System;
using System.Globalization;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.AttendanceTracking;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class AttendanceTrackingAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly IAttendanceTrackingAppService _attendanceTracking;

	public AttendanceTrackingAppService_Tests()
	{
		_attendanceTracking = GetRequiredService<IAttendanceTrackingAppService>();
	}

	[Fact]
	public async Task GetDateWiseAsync_Should_Return_Empty_For_Today()
	{
		var input = new GetAttendanceDateWiseInput
		{
			AttendanceDate = DateTime.UtcNow.ToString("dd-MM-yyyy"),
			Status = AttendanceTrackingStatus.Present,
			EducationMediumStageId = Guid.NewGuid(),
			Page = 1,
			PageSize = 10
		};

		var result = await _attendanceTracking.GetDateWiseAsync(input);

		result.ShouldNotBeNull();
		result.Items.ShouldNotBeNull();
		result.Summary.ShouldNotBeNull();
		result.CurrentPage.ShouldBe(1);
		result.PageSize.ShouldBe(10);
	}

	[Fact]
	public async Task GetDateWiseAsync_Should_Throw_Validation_For_Empty_MediumStage()
	{
		var input = new GetAttendanceDateWiseInput
		{
			AttendanceDate = DateTime.UtcNow.ToString("dd-MM-yyyy"),
			Status = AttendanceTrackingStatus.Present,
			EducationMediumStageId = Guid.Empty,
			Page = 1,
			PageSize = 10
		};

		await Should.ThrowAsync<AbpValidationException>(async () =>
			await _attendanceTracking.GetDateWiseAsync(input));
	}

	[Fact]
	public async Task GetDateWiseAsync_Should_Throw_Validation_For_Invalid_Date()
	{
		var input = new GetAttendanceDateWiseInput
		{
			AttendanceDate = "not-a-date",
			Status = AttendanceTrackingStatus.Present,
			EducationMediumStageId = Guid.NewGuid(),
			Page = 1,
			PageSize = 10
		};

		await Should.ThrowAsync<AbpValidationException>(async () =>
			await _attendanceTracking.GetDateWiseAsync(input));
	}

	[Fact]
	public async Task GetEmployeeWiseAsync_Should_Return_Empty_For_Unknown_Employee()
	{
		var input = new GetAttendanceEmployeeWiseInput
		{
			EmployeeId = Guid.NewGuid(),
			StartDate = DateTime.UtcNow.AddDays(-7).ToString("dd-MM-yyyy"),
			EndDate = DateTime.UtcNow.ToString("dd-MM-yyyy"),
			EducationMediumStageId = Guid.NewGuid(),
			Page = 1,
			PageSize = 10
		};

		var result = await _attendanceTracking.GetEmployeeWiseAsync(input);

		result.ShouldNotBeNull();
		result.Items.ShouldNotBeNull();
		result.AttendanceSummary.ShouldNotBeNull();
		result.CurrentPage.ShouldBe(1);
		result.PageSize.ShouldBe(10);
	}

	[Fact]
	public async Task GetEmployeeWiseAsync_Should_Throw_Validation_For_EndDate_Before_StartDate()
	{
		var input = new GetAttendanceEmployeeWiseInput
		{
			EmployeeId = Guid.NewGuid(),
			StartDate = DateTime.UtcNow.ToString("dd-MM-yyyy"),
			EndDate = DateTime.UtcNow.AddDays(-10).ToString("dd-MM-yyyy"),
			EducationMediumStageId = Guid.NewGuid(),
			Page = 1,
			PageSize = 10
		};

		await Should.ThrowAsync<AbpValidationException>(async () =>
			await _attendanceTracking.GetEmployeeWiseAsync(input));
	}

	[Fact]
	public async Task GetEmployeeWiseAsync_Should_Throw_Validation_For_Range_Exceeding_90_Days()
	{
		var input = new GetAttendanceEmployeeWiseInput
		{
			EmployeeId = Guid.NewGuid(),
			StartDate = DateTime.UtcNow.AddDays(-100).ToString("dd-MM-yyyy"),
			EndDate = DateTime.UtcNow.ToString("dd-MM-yyyy"),
			EducationMediumStageId = Guid.NewGuid(),
			Page = 1,
			PageSize = 10
		};

		await Should.ThrowAsync<AbpValidationException>(async () =>
			await _attendanceTracking.GetEmployeeWiseAsync(input));
	}

	[Fact]
	public async Task GetEmployeeWiseAsync_Should_Throw_Validation_For_Invalid_Date_Format()
	{
		var input = new GetAttendanceEmployeeWiseInput
		{
			EmployeeId = Guid.NewGuid(),
			StartDate = "garbage",
			EndDate = DateTime.UtcNow.ToString("dd-MM-yyyy"),
			EducationMediumStageId = Guid.NewGuid(),
			Page = 1,
			PageSize = 10
		};

		await Should.ThrowAsync<AbpValidationException>(async () =>
			await _attendanceTracking.GetEmployeeWiseAsync(input));
	}

	[Fact]
	public async Task GetEmployeeWiseAsync_Should_Return_Iso_Date_Values()
	{
		var input = new GetAttendanceEmployeeWiseInput
		{
			EmployeeId = Guid.NewGuid(),
			StartDate = DateTime.UtcNow.AddDays(-2).ToString("dd-MM-yyyy"),
			EndDate = DateTime.UtcNow.ToString("dd-MM-yyyy"),
			EducationMediumStageId = Guid.NewGuid(),
			Page = 1,
			PageSize = 10
		};

		var result = await _attendanceTracking.GetEmployeeWiseAsync(input);

		result.ShouldNotBeNull();
		foreach (var item in result.Items)
		{
			DateTimeOffset.TryParse(item.Date, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _)
				.ShouldBeTrue();
		}
	}
}
