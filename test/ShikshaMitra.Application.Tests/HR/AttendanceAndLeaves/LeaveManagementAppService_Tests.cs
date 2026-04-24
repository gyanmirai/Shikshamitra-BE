using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR;
using ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class LeaveManagementAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly Guid TestAcademicYearId = Guid.Parse("00000000-0000-0000-0000-000000000020");

	private readonly ILeaveManagementAppService _leaveManagement;

	public LeaveManagementAppService_Tests()
	{
		_leaveManagement = GetRequiredService<ILeaveManagementAppService>();
		SeedAcademicYearForListTests();
	}

	private static LeaveManagementGetListInput BuildListInput(
		Guid academicYearId,
		DateTime monthYearAnchor,
		string? search,
		int page,
		int pageSize)
	{
		return new LeaveManagementGetListInput
		{
			AcademicYearId = academicYearId,
			MonthYearAnchor = monthYearAnchor,
			Search = search,
			Page = page,
			PageSize = pageSize
		};
	}

	private static LeaveManagementGetListInput BuildListInputFullAcademicYear(
		Guid academicYearId,
		string? search,
		int page,
		int pageSize)
	{
		return new LeaveManagementGetListInput
		{
			AcademicYearId = academicYearId,
			MonthYearAnchor = null,
			Search = search,
			Page = page,
			PageSize = pageSize
		};
	}

	private static LeaveManagementCalendarInput BuildCalendarInput(
		Guid academicYearId,
		DateTimeOffset rangeStart,
		DateTimeOffset rangeEnd,
		int page = 1,
		int pageSize = 100)
	{
		return new LeaveManagementCalendarInput
		{
			AcademicYearId = academicYearId,
			RangeStart = rangeStart,
			RangeEnd = rangeEnd,
			Page = page,
			PageSize = pageSize
		};
	}

	private void SeedAcademicYearForListTests()
	{
		WithUnitOfWorkAsync(async () =>
		{
			var dbContext = ServiceProvider.GetRequiredService<ShikshaMitraDbContext>();
			var now = DateTime.UtcNow;
			var stamp = Guid.NewGuid().ToString();
			var start = new DateTime(now.Year, 1, 1);
			var end = new DateTime(now.Year, 12, 31);

			await dbContext.Database.ExecuteSqlRawAsync(
				"INSERT OR IGNORE INTO AppAcademicYears (Id, Name, StartDate, EndDate, IsDeleted, ConcurrencyStamp, ExtraProperties, CreationTime) VALUES ({0}, {1}, {2}, {3}, 0, {4}, {5}, {6})",
				TestAcademicYearId,
				"LeaveMgmt Test Year",
				start,
				end,
				stamp,
				"{}",
				now);
		}).GetAwaiter().GetResult();
	}

	[Fact]
	public async Task GetListAsync_Should_Return_Empty_When_No_Leaves()
	{
		var anchor = new DateTime(DateTime.UtcNow.Year, 6, 15);
		var result = await _leaveManagement.GetListAsync(BuildListInput(TestAcademicYearId, anchor, null, 1, 10));

		result.ShouldNotBeNull();
		result.TotalCount.ShouldBe(0);
		result.Items.ShouldNotBeNull();
		result.Items.Count.ShouldBe(0);
	}

	[Fact]
	public async Task GetListAsync_Should_Return_Empty_When_No_Leaves_Full_Academic_Year()
	{
		var result = await _leaveManagement.GetListAsync(BuildListInputFullAcademicYear(TestAcademicYearId, null, 1, 10));

		result.ShouldNotBeNull();
		result.TotalCount.ShouldBe(0);
		result.Items.ShouldNotBeNull();
		result.Items.Count.ShouldBe(0);
	}

	[Fact]
	public async Task GetListAsync_Should_Reject_Invalid_Page()
	{
		var ex = await Should.ThrowAsync<BusinessException>(async () =>
			await _leaveManagement.GetListAsync(
				BuildListInput(TestAcademicYearId, new DateTime(DateTime.UtcNow.Year, 6, 1), null, 0, 10)));

		ex.Code.ShouldBe(HRErrorCodes.LeaveInvalidPage);
	}

	[Fact]
	public async Task GetListAsync_Should_Reject_PageSize_Below_Minimum()
	{
		var ex = await Should.ThrowAsync<BusinessException>(async () =>
			await _leaveManagement.GetListAsync(
				BuildListInput(TestAcademicYearId, new DateTime(DateTime.UtcNow.Year, 6, 1), null, 1, 5)));

		ex.Code.ShouldBe(HRErrorCodes.LeaveInvalidPageSize);
	}

	[Fact]
	public async Task GetListAsync_Should_Reject_PageSize_Above_Maximum()
	{
		var ex = await Should.ThrowAsync<BusinessException>(async () =>
			await _leaveManagement.GetListAsync(
				BuildListInput(TestAcademicYearId, new DateTime(DateTime.UtcNow.Year, 6, 1), null, 1, 200)));

		ex.Code.ShouldBe(HRErrorCodes.LeaveInvalidPageSize);
	}

	[Fact]
	public async Task GetListAsync_Should_Reject_Invalid_Year_Anchor()
	{
		var ex = await Should.ThrowAsync<BusinessException>(async () =>
			await _leaveManagement.GetListAsync(
				BuildListInput(TestAcademicYearId, new DateTime(1800, 6, 1), null, 1, 10)));

		ex.Code.ShouldBe(HRErrorCodes.LeaveInvalidYear);
	}

	[Fact]
	public async Task GetListAsync_Should_Throw_When_Academic_Year_Missing()
	{
		await Should.ThrowAsync<EntityNotFoundException>(async () =>
			await _leaveManagement.GetListAsync(
				BuildListInput(Guid.NewGuid(), new DateTime(DateTime.UtcNow.Year, 6, 1), null, 1, 10)));
	}

	[Fact]
	public async Task GetListAsync_Should_Throw_When_Month_Outside_Academic_Year()
	{
		var now = DateTime.UtcNow;
		var shortAyId = Guid.Parse("00000000-0000-0000-0000-000000000021");

		WithUnitOfWorkAsync(async () =>
		{
			var dbContext = ServiceProvider.GetRequiredService<ShikshaMitraDbContext>();
			var stamp = Guid.NewGuid().ToString();
			await dbContext.Database.ExecuteSqlRawAsync(
				"INSERT OR IGNORE INTO AppAcademicYears (Id, Name, StartDate, EndDate, IsDeleted, ConcurrencyStamp, ExtraProperties, CreationTime) VALUES ({0}, {1}, {2}, {3}, 0, {4}, {5}, {6})",
				shortAyId,
				"Short AY",
				new DateTime(now.Year, 3, 1),
				new DateTime(now.Year, 3, 31),
				stamp,
				"{}",
				now);
		}).GetAwaiter().GetResult();

		var ex = await Should.ThrowAsync<BusinessException>(async () =>
			await _leaveManagement.GetListAsync(
				BuildListInput(shortAyId, new DateTime(now.Year, 6, 1), null, 1, 10)));

		ex.Code.ShouldBe(HRErrorCodes.LeaveInvalidMonthForAcademicYear);
	}

	[Fact]
	public async Task GetCalendarAsync_Should_Clip_Offset_Range_To_AcademicYear()
	{
		var now = DateTime.UtcNow;
		var shortAyId = Guid.Parse("00000000-0000-0000-0000-000000000022");

		WithUnitOfWorkAsync(async () =>
		{
			var dbContext = ServiceProvider.GetRequiredService<ShikshaMitraDbContext>();
			var stamp = Guid.NewGuid().ToString();
			await dbContext.Database.ExecuteSqlRawAsync(
				"INSERT OR IGNORE INTO AppAcademicYears (Id, Name, StartDate, EndDate, IsDeleted, ConcurrencyStamp, ExtraProperties, CreationTime) VALUES ({0}, {1}, {2}, {3}, 0, {4}, {5}, {6})",
				shortAyId,
				"Calendar Offset AY",
				new DateTime(now.Year, 3, 1),
				new DateTime(now.Year, 3, 31),
				stamp,
				"{}",
				now);
		}).GetAwaiter().GetResult();

		var result = await _leaveManagement.GetCalendarAsync(BuildCalendarInput(
			shortAyId,
			new DateTimeOffset(new DateTime(now.Year, 2, 20, 0, 0, 0), TimeSpan.FromHours(-7)),
			new DateTimeOffset(new DateTime(now.Year, 4, 5, 23, 59, 59), TimeSpan.FromHours(-7))));

		result.ShouldNotBeNull();
		result.TotalCount.ShouldBe(0);
		result.Items.ShouldNotBeNull();
		result.Items.Count.ShouldBe(0);
	}

	[Fact]
	public async Task GetCalendarAsync_Should_Throw_When_Offset_Range_Is_Completely_Outside_AcademicYear()
	{
		var now = DateTime.UtcNow;
		var shortAyId = Guid.Parse("00000000-0000-0000-0000-000000000023");

		WithUnitOfWorkAsync(async () =>
		{
			var dbContext = ServiceProvider.GetRequiredService<ShikshaMitraDbContext>();
			var stamp = Guid.NewGuid().ToString();
			await dbContext.Database.ExecuteSqlRawAsync(
				"INSERT OR IGNORE INTO AppAcademicYears (Id, Name, StartDate, EndDate, IsDeleted, ConcurrencyStamp, ExtraProperties, CreationTime) VALUES ({0}, {1}, {2}, {3}, 0, {4}, {5}, {6})",
				shortAyId,
				"Calendar Outside Offset AY",
				new DateTime(now.Year, 3, 1),
				new DateTime(now.Year, 3, 31),
				stamp,
				"{}",
				now);
		}).GetAwaiter().GetResult();

		var ex = await Should.ThrowAsync<BusinessException>(async () =>
			await _leaveManagement.GetCalendarAsync(BuildCalendarInput(
				shortAyId,
				new DateTimeOffset(new DateTime(now.Year, 6, 1, 0, 0, 0), TimeSpan.FromHours(-7)),
				new DateTimeOffset(new DateTime(now.Year, 6, 30, 23, 59, 59), TimeSpan.FromHours(-7)))));

		ex.Code.ShouldBe(HRErrorCodes.LeaveInvalidMonthForAcademicYear);
	}

	[Fact]
	public void IsValidStatusTransition_Cancelled_Should_Be_Valid_From_Pending()
	{
		var leave = EmployeeLeave.Create(
			Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Guid.NewGuid(),
			DateTimeOffset.UtcNow.AddDays(5), DateTimeOffset.UtcNow.AddDays(6),
			"test", false);

		leave.Status.ShouldBe(EmployeeLeaveStatusType.Pending);
		leave.Cancel();
		leave.Status.ShouldBe(EmployeeLeaveStatusType.Cancelled);
	}

	[Fact]
	public void Cancel_Should_Throw_From_Approved()
	{
		var leave = EmployeeLeave.Create(
			Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Guid.NewGuid(),
			DateTimeOffset.UtcNow.AddDays(5), DateTimeOffset.UtcNow.AddDays(6),
			"test", false);

		leave.UpdateLeaveStatus(EmployeeLeaveStatusType.Approved, null);

		var ex = Should.Throw<BusinessException>(() => leave.Cancel());
		ex.Code.ShouldBe(HRErrorCodes.LeaveNotCancellable);
	}

	[Fact]
	public void Cancel_Should_Throw_From_Rejected()
	{
		var leave = EmployeeLeave.Create(
			Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Guid.NewGuid(),
			DateTimeOffset.UtcNow.AddDays(5), DateTimeOffset.UtcNow.AddDays(6),
			"test", false);

		leave.UpdateLeaveStatus(EmployeeLeaveStatusType.Rejected, "reason");

		var ex = Should.Throw<BusinessException>(() => leave.Cancel());
		ex.Code.ShouldBe(HRErrorCodes.LeaveNotCancellable);
	}

	[Fact]
	public void Cancel_Should_Throw_From_Already_Cancelled()
	{
		var leave = EmployeeLeave.Create(
			Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Guid.NewGuid(),
			DateTimeOffset.UtcNow.AddDays(5), DateTimeOffset.UtcNow.AddDays(6),
			"test", false);

		leave.Cancel();

		var ex = Should.Throw<BusinessException>(() => leave.Cancel());
		ex.Code.ShouldBe(HRErrorCodes.LeaveNotCancellable);
	}
}
