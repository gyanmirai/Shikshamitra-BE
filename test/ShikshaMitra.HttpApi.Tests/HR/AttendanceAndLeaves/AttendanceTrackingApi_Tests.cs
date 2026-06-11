using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.AttendanceAndLeaves.AttendanceTracking;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class AttendanceTrackingApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/attendance-tracking";

	public AttendanceTrackingApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	#region Tier 0 — Critical

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Date_Wise_Attendance_With_Contract_Params()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var today = DateTime.UtcNow.ToString("dd-MM-yyyy");
		var mediumStageId = EmployeeTestHelper.DefaultMediumStageId;
		var response = await Client.GetAsync(
			$"{BaseUrl}/date-wise?attendanceDate={today}&status=PRESENT&page=1&pageSize=10&educationMediumStageId={mediumStageId}&search=abc");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var json = rawPayload;
		using var document = JsonDocument.Parse(json);
		document.RootElement.TryGetProperty("totalCount", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("currentPage", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("pageSize", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("totalPages", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("summary", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("items", out var items).ShouldBeTrue();
		if (items.ValueKind == JsonValueKind.Array && items.GetArrayLength() > 0)
		{
			items[0].TryGetProperty("status", out var status).ShouldBeTrue();
			status.ValueKind.ShouldBe(JsonValueKind.Number);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Date_Wise_Attendance_With_Year_Month_Day_Date()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
		var mediumStageId = EmployeeTestHelper.DefaultMediumStageId;

		var response = await Client.GetAsync(
			$"{BaseUrl}/date-wise?attendanceDate={today}&page=1&pageSize=10&educationMediumStageId={mediumStageId}");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);
	}

	#endregion

	#region Tier 1 — Important

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Employee_Wise_Attendance_With_Pagination_Fields()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "TrackPage");
		var mediumStageId = EmployeeTestHelper.DefaultMediumStageId;
		var start = DateTime.UtcNow.AddDays(-30).ToString("dd-MM-yyyy");
		var end = DateTime.UtcNow.ToString("dd-MM-yyyy");

		var response = await Client.GetAsync(
			$"{BaseUrl}/employee-wise?employeeId={employee.Id:D}&startDate={start}&endDate={end}&page=1&pageSize=10&educationMediumStageId={mediumStageId}&search=mon");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var json = rawPayload;
		using var document = JsonDocument.Parse(json);
		document.RootElement.TryGetProperty("totalCount", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("currentPage", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("pageSize", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("totalPages", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("attendanceSummary", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("items", out var items).ShouldBeTrue();
		if (items.ValueKind == JsonValueKind.Array && items.GetArrayLength() > 0)
		{
			items[0].TryGetProperty("status", out var status).ShouldBeTrue();
			status.ValueKind.ShouldBe(JsonValueKind.Number);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Employee_Wise_Attendance_With_Year_Month_Day_Dates()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "TrackYmd");
		var mediumStageId = EmployeeTestHelper.DefaultMediumStageId;
		var start = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)).ToString("yyyy-MM-dd");
		var end = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

		var response = await Client.GetAsync(
			$"{BaseUrl}/employee-wise?employeeId={employee.Id:D}&startDate={start}&endDate={end}&page=1&pageSize=10&educationMediumStageId={mediumStageId}");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Employee_Wise_Attendance_Without_EducationMediumStageId()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var attendanceDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1));
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			"TrackNoStage",
			dateOfJoining: attendanceDate.ToDateTime(TimeOnly.MinValue).AddDays(-10));
		await SeedAttendanceAsync(employee.Id, attendanceDate, EmployeeTestHelper.DefaultMediumStageId);

		var response = await Client.GetAsync(
			$"{BaseUrl}/employee-wise?employeeId={employee.Id:D}&startDate={attendanceDate:dd-MM-yyyy}&endDate={attendanceDate:dd-MM-yyyy}&page=1&pageSize=10");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var payload = await response.Content.ReadFromJsonAsync<AttendanceEmployeeWiseResponse>()
			?? throw new InvalidOperationException("Failed to deserialize employee-wise attendance response.");
		payload.TotalCount.ShouldBe(1);
		payload.AttendanceSummary.Present.ShouldBe(1);
		payload.Items.Count.ShouldBe(1);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Employee_Wise_Attendance_With_AcademicYearId_Filters_Attendance_To_Year_Stage()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var seeded = await SeedAttendanceAcademicOrganizationsAsync();
		var startDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-2));
		var endDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1));
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			"TrackYearScope",
			dateOfJoining: startDate.ToDateTime(TimeOnly.MinValue).AddDays(-10));

		await AssignEmployeeToStageAsync(employee.Id, seeded.CurrentYearStageId);
		await AssignEmployeeToStageAsync(employee.Id, seeded.OtherYearStageId);
		await SeedAttendanceAsync(employee.Id, startDate, seeded.OtherYearStageId);
		await SeedAttendanceAsync(employee.Id, endDate, seeded.CurrentYearStageId);

		var response = await Client.GetAsync(
			$"{BaseUrl}/employee-wise?employeeId={employee.Id:D}&startDate={startDate:dd-MM-yyyy}&endDate={endDate:dd-MM-yyyy}&page=1&pageSize=10&academicYearId={seeded.AcademicYearId:D}");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var payload = await response.Content.ReadFromJsonAsync<AttendanceEmployeeWiseResponse>()
			?? throw new InvalidOperationException("Failed to deserialize employee-wise attendance response.");
		payload.TotalCount.ShouldBe(2);
		payload.AttendanceSummary.Present.ShouldBe(1);
		payload.AttendanceSummary.Absent.ShouldBe(1);
		payload.Items.Count(x => x.Status == AttendanceTrackingStatus.Present).ShouldBe(1);
		payload.Items.Count(x => x.Status == AttendanceTrackingStatus.Absent).ShouldBe(1);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Ignore_Employee_Wise_Attendance_When_Row_Stage_Is_Not_In_Scope()
	{
		var seeded = await SeedAttendanceAcademicOrganizationsAsync();
		var scopedUserId = Guid.NewGuid();
		await EnsureUserHasStageScopeAsync(scopedUserId, seeded.CurrentYearStageId);
		var scopedClient = CreateClientAs(scopedUserId);
		var attendanceDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1));
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			"TrackScopedStale",
			dateOfJoining: attendanceDate.ToDateTime(TimeOnly.MinValue).AddDays(-10));

		await ReplaceEmployeeStageAssignmentsAsync(employee.Id, seeded.CurrentYearStageId);
		await SeedAttendanceAsync(employee.Id, attendanceDate, seeded.OtherYearStageId);

		var response = await scopedClient.GetAsync(
			$"{BaseUrl}/employee-wise?employeeId={employee.Id:D}&startDate={attendanceDate:dd-MM-yyyy}&endDate={attendanceDate:dd-MM-yyyy}&page=1&pageSize=10");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var payload = await response.Content.ReadFromJsonAsync<AttendanceEmployeeWiseResponse>()
			?? throw new InvalidOperationException("Failed to deserialize employee-wise attendance response.");
		payload.TotalCount.ShouldBe(1);
		payload.AttendanceSummary.Present.ShouldBe(0);
		payload.AttendanceSummary.Absent.ShouldBe(1);
		payload.Items.Single().Status.ShouldBe(AttendanceTrackingStatus.Absent);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Ignore_Date_Wise_Attendance_When_Row_Stage_Does_Not_Match_Filter()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var seeded = await SeedAttendanceAcademicOrganizationsAsync();
		var attendanceDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1));
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			"TrackDateStaleStage",
			dateOfJoining: attendanceDate.ToDateTime(TimeOnly.MinValue).AddDays(-10));

		await AssignEmployeeToStageAsync(employee.Id, seeded.CurrentYearStageId);
		await AssignEmployeeToStageAsync(employee.Id, seeded.OtherYearStageId);
		await SeedAttendanceAsync(employee.Id, attendanceDate, seeded.OtherYearStageId);

		var response = await Client.GetAsync(
			$"{BaseUrl}/date-wise?attendanceDate={attendanceDate:dd-MM-yyyy}&page=1&pageSize=10&educationMediumStageId={seeded.CurrentYearStageId:D}");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var payload = await response.Content.ReadFromJsonAsync<AttendanceDateWiseResponse>()
			?? throw new InvalidOperationException("Failed to deserialize date-wise attendance response.");
		payload.TotalCount.ShouldBe(0);
		payload.Summary.Present.ShouldBe(0);
		payload.Summary.Absent.ShouldBe(0);
		payload.Items.Count.ShouldBe(0);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Ignore_Employee_Wise_Attendance_When_Row_Has_No_Stage()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var attendanceDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1));
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			"TrackNullStage",
			dateOfJoining: attendanceDate.ToDateTime(TimeOnly.MinValue).AddDays(-10));

		await SeedAttendanceAsync(employee.Id, attendanceDate, null);

		var response = await Client.GetAsync(
			$"{BaseUrl}/employee-wise?employeeId={employee.Id:D}&startDate={attendanceDate:dd-MM-yyyy}&endDate={attendanceDate:dd-MM-yyyy}&page=1&pageSize=10&educationMediumStageId={EmployeeTestHelper.DefaultMediumStageId:D}");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var payload = await response.Content.ReadFromJsonAsync<AttendanceEmployeeWiseResponse>()
			?? throw new InvalidOperationException("Failed to deserialize employee-wise attendance response.");
		payload.TotalCount.ShouldBe(1);
		payload.AttendanceSummary.Present.ShouldBe(0);
		payload.AttendanceSummary.Absent.ShouldBe(1);
		payload.Items.Single().Status.ShouldBe(AttendanceTrackingStatus.Absent);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Employee_Wise_When_Provided_EducationMediumStageId_Is_Outside_AcademicYear()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var seeded = await SeedAttendanceAcademicOrganizationsAsync();
		var attendanceDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1));
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			"TrackStageYear",
			dateOfJoining: attendanceDate.ToDateTime(TimeOnly.MinValue).AddDays(-10));

		await ReplaceEmployeeStageAssignmentsAsync(employee.Id, seeded.OtherYearStageId);

		var response = await Client.GetAsync(
			$"{BaseUrl}/employee-wise?employeeId={employee.Id:D}&startDate={attendanceDate:dd-MM-yyyy}&endDate={attendanceDate:dd-MM-yyyy}&page=1&pageSize=10&educationMediumStageId={seeded.OtherYearStageId:D}&academicYearId={seeded.AcademicYearId:D}");

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Employee_Wise_When_Provided_EducationMediumStageId_Is_Out_Of_Scope()
	{
		var seeded = await SeedAttendanceAcademicOrganizationsAsync();
		var scopedUserId = Guid.NewGuid();
		await EnsureUserHasStageScopeAsync(scopedUserId, seeded.CurrentYearStageId);
		var scopedClient = CreateClientAs(scopedUserId);
		var attendanceDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1));
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			"TrackOutOfScope",
			dateOfJoining: attendanceDate.ToDateTime(TimeOnly.MinValue).AddDays(-10));

		await ReplaceEmployeeStageAssignmentsAsync(employee.Id, seeded.OtherYearStageId);

		var response = await scopedClient.GetAsync(
			$"{BaseUrl}/employee-wise?employeeId={employee.Id:D}&startDate={attendanceDate:dd-MM-yyyy}&endDate={attendanceDate:dd-MM-yyyy}&page=1&pageSize=10&educationMediumStageId={seeded.OtherYearStageId:D}");

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Employee_Wise_When_Employee_Is_Not_Assigned_To_Provided_Stage()
	{
		var seeded = await SeedAttendanceAcademicOrganizationsAsync();
		var scopedUserId = Guid.NewGuid();
		await EnsureUserHasStageScopeAsync(scopedUserId, seeded.CurrentYearStageId);
		var scopedClient = CreateClientAs(scopedUserId);
		var attendanceDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1));
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			"TrackStageLeak",
			dateOfJoining: attendanceDate.ToDateTime(TimeOnly.MinValue).AddDays(-10));

		await ReplaceEmployeeStageAssignmentsAsync(employee.Id, seeded.OtherYearStageId);
		await SeedAttendanceAsync(employee.Id, attendanceDate, seeded.OtherYearStageId);

		var response = await scopedClient.GetAsync(
			$"{BaseUrl}/employee-wise?employeeId={employee.Id:D}&startDate={attendanceDate:dd-MM-yyyy}&endDate={attendanceDate:dd-MM-yyyy}&page=1&pageSize=10&educationMediumStageId={seeded.CurrentYearStageId:D}");

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Invalid_Status_With_400_And_Raw_Abp_ValidationErrors()
	{
		var today = DateTime.UtcNow.ToString("dd-MM-yyyy");
		var mediumStageId = Guid.NewGuid();
		var response = await Client.GetAsync(
			$"{BaseUrl}/date-wise?attendanceDate={today}&status=INVALID&page=1&pageSize=10&educationMediumStageId={mediumStageId}");

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var json = await response.Content.ReadAsStringAsync();
		json.Contains("status", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Invalid_Date_Format_With_400()
	{
		var mediumStageId = Guid.NewGuid();
		var response = await Client.GetAsync(
			$"{BaseUrl}/date-wise?attendanceDate=not-a-date&status=PRESENT&page=1&pageSize=10&educationMediumStageId={mediumStageId}");

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Timestamp_AttendanceDate_With_400()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var mediumStageId = EmployeeTestHelper.DefaultMediumStageId;
		var timestamp = Uri.EscapeDataString("2026-06-01T22:15:34-07:00");
		var response = await Client.GetAsync(
			$"{BaseUrl}/date-wise?attendanceDate={timestamp}&status=PRESENT&page=1&pageSize=10&educationMediumStageId={mediumStageId}");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, rawPayload);
		rawPayload.Contains("attendanceDate", StringComparison.OrdinalIgnoreCase).ShouldBeTrue(rawPayload);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Timestamp_EmployeeWise_StartDate_With_400()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "TrackStartTimestamp");
		var mediumStageId = EmployeeTestHelper.DefaultMediumStageId;
		var start = Uri.EscapeDataString("2026-06-01T22:15:34-07:00");

		var response = await Client.GetAsync(
			$"{BaseUrl}/employee-wise?employeeId={employee.Id:D}&startDate={start}&endDate=2026-06-02&page=1&pageSize=10&educationMediumStageId={mediumStageId}");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, rawPayload);
		rawPayload.Contains("startDate", StringComparison.OrdinalIgnoreCase).ShouldBeTrue(rawPayload);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Timestamp_EmployeeWise_EndDate_With_400()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "TrackEndTimestamp");
		var mediumStageId = EmployeeTestHelper.DefaultMediumStageId;
		var end = Uri.EscapeDataString("2026-06-02T22:15:34-07:00");

		var response = await Client.GetAsync(
			$"{BaseUrl}/employee-wise?employeeId={employee.Id:D}&startDate=2026-06-01&endDate={end}&page=1&pageSize=10&educationMediumStageId={mediumStageId}");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, rawPayload);
		rawPayload.Contains("endDate", StringComparison.OrdinalIgnoreCase).ShouldBeTrue(rawPayload);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Missing_EducationMediumStageId_With_400()
	{
		var today = DateTime.UtcNow.ToString("dd-MM-yyyy");
		var response = await Client.GetAsync(
			$"{BaseUrl}/date-wise?attendanceDate={today}&status=PRESENT&page=1&pageSize=10");

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task HrSwagger_Should_Expose_AttendanceTrackingStatus_As_Numeric_Enum()
	{
		var response = await Client.GetAsync("/swagger/v1-hr/swagger.json");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var json = await response.Content.ReadAsStringAsync();
		using var document = JsonDocument.Parse(json);
		var statusSchema = document.RootElement
			.GetProperty("components")
			.GetProperty("schemas")
			.GetProperty("ShikshaMitra.HR.AttendanceAndLeaves.AttendanceTracking.AttendanceTrackingStatus");

		statusSchema.GetProperty("type").GetString().ShouldBe("integer");
		statusSchema.TryGetProperty("enum", out var enumValues).ShouldBeTrue();
		enumValues.ValueKind.ShouldBe(JsonValueKind.Array);
		enumValues.GetArrayLength().ShouldBeGreaterThan(0);
	}

	#endregion

	private async Task SeedAttendanceAsync(Guid employeeId, DateOnly attendanceDate, Guid? educationMediumStageId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var attendanceRepository = scope.ServiceProvider.GetRequiredService<IRepository<EmployeeAttendance, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			await attendanceRepository.InsertAsync(new EmployeeAttendance
			{
				TenantId = tenantId,
				EmployeeId = employeeId,
				AttendanceDate = attendanceDate,
				EducationMediumStageId = educationMediumStageId,
				StatusType = EmployeeAttendanceStatusType.Present,
				Source = AttendanceRecordSource.Manual,
				TimeIn = new DateTimeOffset(attendanceDate.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero),
				TimeOut = new DateTimeOffset(attendanceDate.ToDateTime(new TimeOnly(17, 0)), TimeSpan.Zero),
				Remarks = "Attendance tracking test"
			}, autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private async Task AssignEmployeeToStageAsync(Guid employeeId, Guid stageId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var assignmentRepository =
			scope.ServiceProvider.GetRequiredService<IRepository<EmployeeOrganizationAssignment, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var exists = await assignmentRepository.AnyAsync(x =>
				x.EmployeeId == employeeId &&
				x.EducationMediumStageId == stageId);
			if (!exists)
			{
				await assignmentRepository.InsertAsync(
					new EmployeeOrganizationAssignment(Guid.NewGuid(), tenantId, employeeId, stageId),
					autoSave: false);
			}
			await uow.CompleteAsync();
		}
	}

	private async Task ReplaceEmployeeStageAssignmentsAsync(Guid employeeId, Guid stageId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var assignmentRepository =
			scope.ServiceProvider.GetRequiredService<IRepository<EmployeeOrganizationAssignment, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var existing = await assignmentRepository.GetListAsync(x => x.EmployeeId == employeeId);
			foreach (var assignment in existing)
			{
				await assignmentRepository.DeleteAsync(assignment, autoSave: false);
			}

			await assignmentRepository.InsertAsync(
				new EmployeeOrganizationAssignment(Guid.NewGuid(), tenantId, employeeId, stageId),
				autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private async Task<AttendanceAcademicOrganizations> SeedAttendanceAcademicOrganizationsAsync()
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var academicYearRepository = scope.ServiceProvider.GetRequiredService<IAcademicYearRepository>();
			var boardRepository = scope.ServiceProvider.GetRequiredService<IEducationBoardRepository>();
			var mediumRepository = scope.ServiceProvider.GetRequiredService<IEducationMediumRepository>();
			var stageRepository = scope.ServiceProvider.GetRequiredService<IEducationMediumStageRepository>();
			var academicYearManager = scope.ServiceProvider.GetRequiredService<AcademicYearManager>();
			var boardManager = scope.ServiceProvider.GetRequiredService<EducationBoardManager>();
			var mediumManager = scope.ServiceProvider.GetRequiredService<EducationMediumManager>();
			var stageManager = scope.ServiceProvider.GetRequiredService<EducationMediumStageManager>();

			var suffix = Guid.NewGuid().ToString("N")[..8];
			var academicYear = await academicYearManager.CreateAsync(
				$"AttTrack-{suffix}",
				new DateTime(2033, 4, 1),
				new DateTime(2034, 3, 31));
			await academicYearRepository.InsertAsync(academicYear, autoSave: false);

			var otherYear = await academicYearManager.CreateAsync(
				$"AttTrackOther-{suffix}",
				new DateTime(2034, 4, 1),
				new DateTime(2035, 3, 31));
			await academicYearRepository.InsertAsync(otherYear, autoSave: false);

			var board = await boardManager.CreateAsync(academicYear.Id, EducationBoardType.GSEB);
			await boardRepository.InsertAsync(board, autoSave: false);
			var otherBoard = await boardManager.CreateAsync(otherYear.Id, EducationBoardType.CBSE);
			await boardRepository.InsertAsync(otherBoard, autoSave: false);

			var medium = await mediumManager.CreateAsync(board.Id, $"Gujarati-{suffix}");
			await mediumRepository.InsertAsync(medium, autoSave: false);
			var otherMedium = await mediumManager.CreateAsync(otherBoard.Id, $"English-{suffix}");
			await mediumRepository.InsertAsync(otherMedium, autoSave: false);

			var stage = await stageManager.CreateAsync(medium.Id, $"Primary-{suffix}");
			await stageRepository.InsertAsync(stage, autoSave: false);
			var otherStage = await stageManager.CreateAsync(otherMedium.Id, $"Secondary-{suffix}");
			await stageRepository.InsertAsync(otherStage, autoSave: false);

			await uow.CompleteAsync();

			return new AttendanceAcademicOrganizations(
				academicYear.Id,
				stage.Id,
				otherStage.Id);
		}
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
					description: "API regression test attendance tracking stage scope"),
				autoSave: false);
			await userScopeService.InvalidateUserScopeCacheAsync(userId);
			await uow.CompleteAsync();
		}
	}

	private sealed record AttendanceAcademicOrganizations(
		Guid AcademicYearId,
		Guid CurrentYearStageId,
		Guid OtherYearStageId);
}
