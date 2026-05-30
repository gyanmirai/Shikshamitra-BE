using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegister;
using ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class AttendanceRegisterApi_Tests : ShikshaMitraApiTestBase
{
	private const string RegisterUrl = "/api/hr/attendance/register";
	private const string TakeAttendanceUrl = "/api/hr/attendance/take-attendance";

	public AttendanceRegisterApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Grouped_Register_Should_Count_All_Scoped_Employees_Including_NotMarked()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var marker = $"RegCount{Guid.NewGuid():N}";
		var attendanceDate = DateTime.UtcNow.AddDays(-1);
		var attendanceDateText = attendanceDate.ToString("dd-MM-yyyy");
		var startEnd = attendanceDate.ToString("yyyy-MM-dd");
		var presentEmployee = await EmployeeTestHelper.CreateEmployeeAsync(Client, $"{marker}Present", dateOfJoining: attendanceDate.AddDays(-10));
		var absentEmployee = await EmployeeTestHelper.CreateEmployeeAsync(Client, $"{marker}Absent", dateOfJoining: attendanceDate.AddDays(-10));
		var notMarkedEmployee = await EmployeeTestHelper.CreateEmployeeAsync(Client, $"{marker}NotMarked", dateOfJoining: attendanceDate.AddDays(-10));

		var saveResponse = await Client.PostAsJsonAsync($"{TakeAttendanceUrl}/day/save", new TakeAttendanceSaveInput
		{
			AttendanceDate = attendanceDateText,
			Entries =
			[
				new TakeAttendanceSaveEntryDto
				{
					EmployeeId = presentEmployee.Id,
					StatusWire = "PRESENT",
					InTime = "09:00:00",
					OutTime = "17:00:00"
				},
				new TakeAttendanceSaveEntryDto
				{
					EmployeeId = absentEmployee.Id,
					StatusWire = "ABSENT"
				}
			]
		}, JsonOptions);
		saveResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var response = await Client.GetAsync(
			$"{RegisterUrl}/grouped?startDate={startEnd}&endDate={startEnd}&search={Uri.EscapeDataString(marker)}&skipCount=0&maxResultCount=50");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var payload = await response.Content.ReadFromJsonAsync<GroupedAttendanceRegisterResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize grouped register response.");

		payload.TotalCount.ShouldBe(3);
		payload.Items.Count.ShouldBe(3);
		payload.Summary.StaffCount.ShouldBe(3);
		payload.Summary.Present.ShouldBe(1);
		payload.Summary.Absent.ShouldBe(1);
		payload.Summary.NotMarked.ShouldBe(1);

		var notMarked = payload.Items.Single(x => x.EmployeeId == notMarkedEmployee.Id).Days.Single();
		notMarked.Status.ShouldBe(AttendanceRegisterStatus.NotMarked);
		notMarked.IsMarked.ShouldBeFalse();
		notMarked.AttendanceId.ShouldBeNull();
		notMarked.CanEdit.ShouldBeFalse();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Grouped_Register_Status_Filter_Should_Exclude_NotMarked_Rows()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var marker = $"RegStatus{Guid.NewGuid():N}";
		var attendanceDate = DateTime.UtcNow.AddDays(-1);
		var attendanceDateText = attendanceDate.ToString("dd-MM-yyyy");
		var startEnd = attendanceDate.ToString("yyyy-MM-dd");
		var presentEmployee = await EmployeeTestHelper.CreateEmployeeAsync(Client, $"{marker}Present", dateOfJoining: attendanceDate.AddDays(-10));
		await EmployeeTestHelper.CreateEmployeeAsync(Client, $"{marker}NotMarked", dateOfJoining: attendanceDate.AddDays(-10));

		var saveResponse = await Client.PostAsJsonAsync($"{TakeAttendanceUrl}/day/save", new TakeAttendanceSaveInput
		{
			AttendanceDate = attendanceDateText,
			Entries =
			[
				new TakeAttendanceSaveEntryDto
				{
					EmployeeId = presentEmployee.Id,
					StatusWire = "PRESENT",
					InTime = "09:00:00",
					OutTime = "17:00:00"
				}
			]
		}, JsonOptions);
		saveResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var response = await Client.GetAsync(
			$"{RegisterUrl}/grouped?startDate={startEnd}&endDate={startEnd}&status=Present&search={Uri.EscapeDataString(marker)}&skipCount=0&maxResultCount=50");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var payload = await response.Content.ReadFromJsonAsync<GroupedAttendanceRegisterResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize grouped register response.");

		payload.TotalCount.ShouldBe(1);
		payload.Items.Single().EmployeeId.ShouldBe(presentEmployee.Id);
		payload.Summary.StaffCount.ShouldBe(1);
		payload.Summary.Present.ShouldBe(1);
		payload.Summary.NotMarked.ShouldBe(0);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Grouped_Register_Search_Should_Match_Attendance_Remarks()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var marker = $"RegRemark{Guid.NewGuid():N}";
		var attendanceDate = DateTime.UtcNow.AddDays(-1);
		var attendanceDateText = attendanceDate.ToString("dd-MM-yyyy");
		var startEnd = attendanceDate.ToString("yyyy-MM-dd");
		var matchedEmployee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "Remark Search Matched", dateOfJoining: attendanceDate.AddDays(-10));
		await EmployeeTestHelper.CreateEmployeeAsync(Client, "Remark Search Unmarked", dateOfJoining: attendanceDate.AddDays(-10));

		var saveResponse = await Client.PostAsJsonAsync($"{TakeAttendanceUrl}/day/save", new TakeAttendanceSaveInput
		{
			AttendanceDate = attendanceDateText,
			Entries =
			[
				new TakeAttendanceSaveEntryDto
				{
					EmployeeId = matchedEmployee.Id,
					StatusWire = "PRESENT",
					InTime = "09:00:00",
					OutTime = "17:00:00",
					Remarks = marker
				}
			]
		}, JsonOptions);
		saveResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var response = await Client.GetAsync(
			$"{RegisterUrl}/grouped?startDate={startEnd}&endDate={startEnd}&search={Uri.EscapeDataString(marker)}&skipCount=0&maxResultCount=50");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var payload = await response.Content.ReadFromJsonAsync<GroupedAttendanceRegisterResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize grouped register response.");

		payload.TotalCount.ShouldBe(1);
		payload.Items.Single().EmployeeId.ShouldBe(matchedEmployee.Id);
		payload.Items.Single().Days.Single().Remarks.ShouldBe(marker);
		payload.Summary.StaffCount.ShouldBe(1);
		payload.Summary.NotMarked.ShouldBe(0);
	}
}
