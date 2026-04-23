using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.Import;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.AttendanceImports;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class AttendanceImportLogsApi_Tests : ShikshaMitraApiTestBase
{
	private const string AttendanceImportUrl = "/api/attendance-import";
	private const string PayrollImportLogsUrl = "/api/payroll/attendance-imports";
	private const string HrAutomatedRunsUrl = "/api/hr/attendance/automated-runs";

	public AttendanceImportLogsApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Hr_Mirror_Automated_Runs_List()
	{
		var response = await Client.GetAsync($"{HrAutomatedRunsUrl}?skipCount=0&maxResultCount=20");
		response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Forbidden);
		if (response.StatusCode == HttpStatusCode.OK)
		{
			var body = await response.Content.ReadFromJsonAsync<PagedResultDto<AttendanceImportLogListItemDto>>(JsonOptions);
			body.ShouldNotBeNull();
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Lock_And_Unlock_Automated_Run_Idempotently()
	{
		var listResponse = await Client.GetAsync($"{HrAutomatedRunsUrl}?skipCount=0&maxResultCount=5");
		listResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Forbidden);
		if (listResponse.StatusCode != HttpStatusCode.OK)
		{
			return;
		}

		var list = await listResponse.Content.ReadFromJsonAsync<PagedResultDto<AttendanceImportLogListItemDto>>(JsonOptions);
		list.ShouldNotBeNull();
		if (list.Items.Count == 0)
		{
			return;
		}

		var batchId = list.Items[0].Id;
		var lockResponse = await Client.PostAsync($"{HrAutomatedRunsUrl}/{batchId}/lock", null);
		lockResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.NoContent, HttpStatusCode.Forbidden);
		if (lockResponse.StatusCode == HttpStatusCode.NoContent)
		{
			var secondLockResponse = await Client.PostAsync($"{HrAutomatedRunsUrl}/{batchId}/lock", null);
			secondLockResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

			var unlockResponse = await Client.DeleteAsync($"{HrAutomatedRunsUrl}/{batchId}/lock");
			unlockResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

			var secondUnlockResponse = await Client.DeleteAsync($"{HrAutomatedRunsUrl}/{batchId}/lock");
			secondUnlockResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Attendance_Import_Logs_List()
	{
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "ImportLogList");
		using var upload = BuildCsvUpload(
			$"11-04-2026,{employee.EmployeeCode},09:00 AM,05:30 PM,,Regular shift,{employee.FullName},{employee.MobileNo}");

		var previewResponse = await Client.PostAsync($"{AttendanceImportUrl}/preview", upload);
		previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var listResponse = await Client.GetAsync($"{PayrollImportLogsUrl}?skipCount=0&maxResultCount=20");
		listResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Forbidden);
		if (listResponse.StatusCode == HttpStatusCode.OK)
		{
			var body = await listResponse.Content.ReadFromJsonAsync<PagedResultDto<AttendanceImportLogListItemDto>>(JsonOptions);
			body.ShouldNotBeNull();
			body.Items.Count.ShouldBeGreaterThan(0);
			if (body.Items.Count > 0)
			{
				Enum.IsDefined(typeof(AttendanceImportLogStatus), body.Items[0].Status).ShouldBeTrue();
				Enum.IsDefined(typeof(AttendanceImportRunSource), body.Items[0].RunSource).ShouldBeTrue();
			}

			var rawJson = await listResponse.Content.ReadAsStringAsync();
			using var doc = JsonDocument.Parse(rawJson);
			var items = doc.RootElement.GetProperty("items");
			if (items.ValueKind == JsonValueKind.Array && items.GetArrayLength() > 0)
			{
				items[0].GetProperty("status").ValueKind.ShouldBe(JsonValueKind.Number);
				items[0].GetProperty("runSource").ValueKind.ShouldBe(JsonValueKind.Number);
			}
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Filter_By_Normalized_Payroll_Month_Formats()
	{
		var listByInternal = await Client.GetAsync($"{PayrollImportLogsUrl}?payrollMonth=2026-04&skipCount=0&maxResultCount=20");
		listByInternal.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Forbidden, HttpStatusCode.BadRequest);
		if (listByInternal.StatusCode == HttpStatusCode.OK)
		{
			var listByApi = await Client.GetAsync($"{PayrollImportLogsUrl}?payrollMonth=APRIL-2026&skipCount=0&maxResultCount=20");
			listByApi.StatusCode.ShouldBe(HttpStatusCode.OK);

			var byInternal = await listByInternal.Content.ReadFromJsonAsync<PagedResultDto<AttendanceImportLogListItemDto>>(JsonOptions);
			var byApi = await listByApi.Content.ReadFromJsonAsync<PagedResultDto<AttendanceImportLogListItemDto>>(JsonOptions);
			byInternal.ShouldNotBeNull();
			byApi.ShouldNotBeNull();
			byInternal.TotalCount.ShouldBe(byApi.TotalCount);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Invalid_Payroll_Month_Filter()
	{
		var response = await Client.GetAsync($"{PayrollImportLogsUrl}?payrollMonth=NOT-A-MONTH");
		response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Attendance_Import_Log_Detail_And_Rows()
	{
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "ImportLogDetail");
		using var upload = BuildCsvUpload(
			$"11-04-2026,{employee.EmployeeCode},,,FullDayLeave,Approved leave,{employee.FullName},{employee.MobileNo}");

		var previewResponse = await Client.PostAsync($"{AttendanceImportUrl}/preview", upload);
		previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var preview = await previewResponse.Content.ReadFromJsonAsync<AttendanceImportPreviewResponseDto>(JsonOptions);
		preview.ShouldNotBeNull();
		preview.PreviewToken.ShouldNotBeNullOrWhiteSpace();

		var confirmResponse = await Client.PostAsJsonAsync($"{AttendanceImportUrl}/confirm", new AttendanceImportConfirmRequestDto
		{
			PreviewToken = preview.PreviewToken!
		}, JsonOptions);
		confirmResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var listResponse = await Client.GetAsync($"{PayrollImportLogsUrl}?skipCount=0&maxResultCount=5");
		listResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Forbidden);
		if (listResponse.StatusCode != HttpStatusCode.OK)
		{
			return;
		}

		var listBody = await listResponse.Content.ReadFromJsonAsync<PagedResultDto<AttendanceImportLogListItemDto>>(JsonOptions);
		listBody.ShouldNotBeNull();
		listBody.Items.Count.ShouldBeGreaterThan(0);

		var importId = listBody.Items[0].Id;
		var detailResponse = await Client.GetAsync($"{PayrollImportLogsUrl}/{importId}");
		detailResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var rowsResponse = await Client.GetAsync($"{PayrollImportLogsUrl}/{importId}/rows?skipCount=0&maxResultCount=50");
		rowsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Multi_Month_Import_File()
	{
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "ImportLogMultiMonth");
		using var upload = BuildCsvUpload(
			$"11-04-2026,{employee.EmployeeCode},09:00 AM,05:30 PM,,Regular shift,{employee.FullName},{employee.MobileNo}",
			$"12-05-2026,{employee.EmployeeCode},09:00 AM,05:30 PM,,Regular shift,{employee.FullName},{employee.MobileNo}");

		var response = await Client.PostAsync($"{AttendanceImportUrl}/preview", upload);
		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.BadRequest,
			HttpStatusCode.Forbidden,
			HttpStatusCode.InternalServerError);
	}

	private static MultipartFormDataContent BuildCsvUpload(params string[] rows)
	{
		var content = new MultipartFormDataContent();
		var csv = string.Join(Environment.NewLine,
			"Attendance Date,Employee Code,Time In,Time Out,Status,Remarks,Employee Name,Contact No",
			string.Join(Environment.NewLine, rows));

		var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
		file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
		content.Add(file, "File", "attendance.csv");
		return content;
	}
}
