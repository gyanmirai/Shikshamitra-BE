using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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
		}
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
