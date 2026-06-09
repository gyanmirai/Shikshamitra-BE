using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.AttendanceAndLeaves.Import;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.AttendanceImports;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class AttendanceImportApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/attendance-import";
	private const string IntegrationRunsUrl = "/api/hr/attendance/integration-runs";

	public AttendanceImportApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Preview_Csv_Import()
	{
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "ImportPreview");
		using var content = BuildCsvUpload($"11-04-2026,{employee.EmployeeCode},09:00 AM,05:30 PM,,Regular shift,{employee.FullName},{employee.MobileNo}");

		var response = await Client.PostAsync($"{BaseUrl}/preview", content);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<AttendanceImportPreviewResponseDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.CanConfirm.ShouldBeTrue();
		body.PreviewToken.ShouldNotBeNullOrWhiteSpace();
		body.Rows.Count.ShouldBe(1);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Csv_Import_Should_Strip_Raw_Punches_For_Manual_Status_Policy()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var attendanceDate = new DateOnly(2026, 4, 11);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, $"ManualCsv{Guid.NewGuid():N}");
		await AssignCaptureModePolicyAsync(employee.Id, attendanceDate, AttendanceCaptureMode.ManualStatus);
		using var previewContent = BuildCsvUpload($"11-04-2026,{employee.EmployeeCode},09:00 AM,09:30 AM,,Short raw punch,{employee.FullName},{employee.MobileNo}");

		var previewResponse = await Client.PostAsync($"{BaseUrl}/preview", previewContent);
		previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var preview = await previewResponse.Content.ReadFromJsonAsync<AttendanceImportPreviewResponseDto>(JsonOptions);
		preview.ShouldNotBeNull();
		preview.Rows.Single().Status.ShouldBe(nameof(EmployeeAttendanceStatusType.Present));

		var confirmResponse = await Client.PostAsJsonAsync($"{BaseUrl}/confirm", new AttendanceImportConfirmRequestDto
		{
			PreviewToken = preview.PreviewToken!
		}, JsonOptions);
		confirmResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var attendance = await GetAttendanceAsync(employee.Id, attendanceDate);
		attendance.TimeIn.ShouldBeNull();
		attendance.TimeOut.ShouldBeNull();
		attendance.StatusType.ShouldBe(EmployeeAttendanceStatusType.Present);
		attendance.Source.ShouldBe(AttendanceRecordSource.CsvImport);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Integration_Run_Should_Strip_Raw_Punches_For_Manual_Status_Policy()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var attendanceDate = new DateOnly(2026, 4, 12);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, $"ManualIntegration{Guid.NewGuid():N}");
		await AssignCaptureModePolicyAsync(employee.Id, attendanceDate, AttendanceCaptureMode.ManualStatus);

		var response = await Client.PostAsJsonAsync(IntegrationRunsUrl, new AttendanceIntegrationRunRequestDto
		{
			FileName = $"integration-{Guid.NewGuid():N}.json",
			RunSource = AttendanceImportRunSource.Integration,
			IntegrationProvider = "regression-test",
			Rows =
			[
				new AttendanceIntegrationRunRowDto
				{
					EmployeeId = employee.Id,
					AttendanceDate = "2026-04-12",
					TimeIn = "2026-04-12T09:00:00Z",
					TimeOut = "2026-04-12T17:30:00Z",
					Status = EmployeeAttendanceStatusType.Present,
					Remarks = "Integration raw punch"
				}
			]
		}, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var attendance = await GetAttendanceAsync(employee.Id, attendanceDate);
		attendance.TimeIn.ShouldBeNull();
		attendance.TimeOut.ShouldBeNull();
		attendance.Source.ShouldBe(AttendanceRecordSource.Integration);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Integration_Run_Should_Store_TimeOnly_Punches_On_Selected_Attendance_Date()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var attendanceDate = new DateOnly(2026, 4, 13);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, $"IntegrationTimeOnly{Guid.NewGuid():N}");

		var response = await Client.PostAsJsonAsync(IntegrationRunsUrl, new AttendanceIntegrationRunRequestDto
		{
			FileName = $"integration-time-only-{Guid.NewGuid():N}.json",
			RunSource = AttendanceImportRunSource.Integration,
			IntegrationProvider = "regression-test",
			Rows =
			[
				new AttendanceIntegrationRunRowDto
				{
					EmployeeId = employee.Id,
					AttendanceDate = "2026-04-13",
					TimeIn = "09:00:00",
					TimeOut = "17:30:00",
					Status = EmployeeAttendanceStatusType.Present,
					Remarks = "Integration time-only punch"
				}
			]
		}, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var attendance = await GetAttendanceAsync(employee.Id, attendanceDate);
		DateOnly.FromDateTime(attendance.TimeIn!.Value.DateTime).ShouldBe(attendanceDate);
		DateOnly.FromDateTime(attendance.TimeOut!.Value.DateTime).ShouldBe(attendanceDate);
		attendance.TimeIn.Value.TimeOfDay.ShouldBe(new TimeSpan(9, 0, 0));
		attendance.TimeOut.Value.TimeOfDay.ShouldBe(new TimeSpan(17, 30, 0));
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Integration_Run_Should_Normalize_DateTime_Punches_To_Selected_Attendance_Date()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var attendanceDate = new DateOnly(2026, 4, 14);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, $"IntegrationNormalize{Guid.NewGuid():N}");

		var response = await Client.PostAsJsonAsync(IntegrationRunsUrl, new AttendanceIntegrationRunRequestDto
		{
			FileName = $"integration-normalize-{Guid.NewGuid():N}.json",
			RunSource = AttendanceImportRunSource.Integration,
			IntegrationProvider = "regression-test",
			Rows =
			[
				new AttendanceIntegrationRunRowDto
				{
					EmployeeId = employee.Id,
					AttendanceDate = "2026-04-14",
					TimeIn = "2026-04-20T09:15:00-07:00",
					TimeOut = "2026-04-20T17:45:00-07:00",
					Status = EmployeeAttendanceStatusType.Present,
					Remarks = "Integration mismatched punch date"
				}
			]
		}, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var attendance = await GetAttendanceAsync(employee.Id, attendanceDate);
		DateOnly.FromDateTime(attendance.TimeIn!.Value.DateTime).ShouldBe(attendanceDate);
		DateOnly.FromDateTime(attendance.TimeOut!.Value.DateTime).ShouldBe(attendanceDate);
		attendance.TimeIn.Value.TimeOfDay.ShouldBe(new TimeSpan(9, 15, 0));
		attendance.TimeOut.Value.TimeOfDay.ShouldBe(new TimeSpan(17, 45, 0));
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Integration_Run_Should_Reject_Timestamp_AttendanceDate()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, $"IntegrationTimestamp{Guid.NewGuid():N}");

		var response = await Client.PostAsJsonAsync(IntegrationRunsUrl, new AttendanceIntegrationRunRequestDto
		{
			FileName = $"integration-timestamp-{Guid.NewGuid():N}.json",
			RunSource = AttendanceImportRunSource.Integration,
			IntegrationProvider = "regression-test",
			Rows =
			[
				new AttendanceIntegrationRunRowDto
				{
					EmployeeId = employee.Id,
					AttendanceDate = "2026-04-15T22:15:34-07:00",
					TimeIn = "09:00:00",
					TimeOut = "17:30:00",
					Status = EmployeeAttendanceStatusType.Present
				}
			]
		}, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var payload = await response.Content.ReadAsStringAsync();
		payload.ShouldContain(HRErrorCodes.AttendanceInvalidDateFormat);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Confirm_Imported_Attendance()
	{
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "ImportConfirm");
		using var previewContent = BuildCsvUpload($"11-04-2026,{employee.EmployeeCode},,,FullDayLeave,Approved leave,{employee.FullName},{employee.MobileNo}");
		var previewResponse = await Client.PostAsync($"{BaseUrl}/preview", previewContent);
		previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var preview = await previewResponse.Content.ReadFromJsonAsync<AttendanceImportPreviewResponseDto>(JsonOptions);

		var confirmResponse = await Client.PostAsJsonAsync($"{BaseUrl}/confirm", new AttendanceImportConfirmRequestDto
		{
			PreviewToken = preview!.PreviewToken!
		}, JsonOptions);

		confirmResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var confirm = await confirmResponse.Content.ReadFromJsonAsync<AttendanceImportConfirmResponseDto>(JsonOptions);
		confirm.ShouldNotBeNull();
		confirm.ProcessedRows.ShouldBe(1);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Preview_Should_Reject_Timestamp_AttendanceDate_String()
	{
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, $"ImportTimestamp{Guid.NewGuid():N}");
		using var content = BuildCsvUpload($"2026-04-16T22:15:34-07:00,{employee.EmployeeCode},09:00 AM,05:30 PM,,Timestamp date,{employee.FullName},{employee.MobileNo}");

		var response = await Client.PostAsync($"{BaseUrl}/preview", content);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<AttendanceImportPreviewResponseDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.CanConfirm.ShouldBeFalse();
		body.Rows.Single().Errors.ShouldContain(x => x.Contains("date-only", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Download_Template()
	{
		var response = await Client.GetAsync($"{BaseUrl}/template");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		response.Content.Headers.ContentType.ShouldNotBeNull();
		response.Content.Headers.ContentType.MediaType.ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Forbidden_For_Preview_When_Anonymous()
	{
		using var anonymousClient = CreateAnonymousClient();
		using var content = BuildCsvUpload("11-04-2026,EMP0001,09:00 AM,05:30 PM,,Regular shift,John Doe,9876543210");

		var response = await anonymousClient.PostAsync($"{BaseUrl}/preview", content);

		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Error_For_Malformed_File()
	{
		using var content = new MultipartFormDataContent();
		var file = new ByteArrayContent(Encoding.UTF8.GetBytes("not-an-excel-file"));
		file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
		content.Add(file, "File", "broken.xlsx");

		var response = await Client.PostAsync($"{BaseUrl}/preview", content);

		response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError);
	}

	private static MultipartFormDataContent BuildCsvUpload(string row)
	{
		var content = new MultipartFormDataContent();
		var csv = string.Join(Environment.NewLine,
			"Attendance Date,Employee Code,Time In,Time Out,Status,Remarks,Employee Name,Contact No",
			row);
		var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
		file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
		content.Add(file, "File", $"attendance-{Guid.NewGuid():N}.csv");
		return content;
	}

	private async Task AssignCaptureModePolicyAsync(
		Guid employeeId,
		DateOnly effectiveDate,
		AttendanceCaptureMode captureMode)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var academicYearRepository = scope.ServiceProvider.GetRequiredService<IAcademicYearRepository>();
		var attendancePolicyRepository = scope.ServiceProvider.GetRequiredService<IRepository<AttendancePolicy, Guid>>();
		var payrollPolicyRepository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollPolicy, Guid>>();
		var assignmentRepository = scope.ServiceProvider.GetRequiredService<IRepository<HrPolicyAssignment, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var targetDate = effectiveDate.ToDateTime(TimeOnly.MinValue);
			var academicYearQuery = await academicYearRepository.GetQueryableAsync();
			var academicYear = academicYearQuery.Single(x => x.StartDate <= targetDate && x.EndDate >= targetDate);
			var attendanceQuery = await attendancePolicyRepository.GetQueryableAsync();
			var defaultAttendance = attendanceQuery.Single(x =>
				x.AcademicYearId == academicYear.Id &&
				x.IsDefault &&
				x.Status == HrPolicyStatus.Active);
			var payrollQuery = await payrollPolicyRepository.GetQueryableAsync();
			var defaultPayroll = payrollQuery.Single(x =>
				x.AcademicYearId == academicYear.Id &&
				x.IsDefault &&
				x.Status == HrPolicyStatus.Active);
			var policy = new AttendancePolicy(
				Guid.NewGuid(),
				tenantId,
				academicYear.Id,
				defaultAttendance.WorkCalendarPolicyId,
				$"Test {captureMode} {Guid.NewGuid():N}",
				HrPolicyStatus.Active,
				false,
				captureMode,
				75,
				50,
				0,
				AttendanceMissingPunchBehavior.RequiresReview,
				true,
				0,
				0,
				AttendancePenaltyType.None,
				0,
				false,
				0,
				false,
				null,
				null,
				AttendanceHolidayWorkBehavior.CompOff,
				false,
				true);
			await attendancePolicyRepository.InsertAsync(policy, autoSave: false);
			await assignmentRepository.InsertAsync(new HrPolicyAssignment(
				Guid.NewGuid(),
				tenantId,
				academicYear.Id,
				HrPolicyAssignmentTargetType.Employee,
				employeeId.ToString("D"),
				null,
				policy.Id,
				defaultPayroll.Id,
				targetDate.Date,
				null,
				true,
				priority: 100,
				assignmentReason: "Capture mode regression test"), autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private async Task<EmployeeAttendance> GetAttendanceAsync(Guid employeeId, DateOnly attendanceDate)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var attendanceRepository = scope.ServiceProvider.GetRequiredService<IRepository<EmployeeAttendance, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var query = await attendanceRepository.GetQueryableAsync();
			var attendance = query.Single(x => x.EmployeeId == employeeId && x.AttendanceDate == attendanceDate);
			await uow.CompleteAsync();
			return attendance;
		}
	}
}

