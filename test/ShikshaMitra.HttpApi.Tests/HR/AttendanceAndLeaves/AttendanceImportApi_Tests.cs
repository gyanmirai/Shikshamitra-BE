using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.Import;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class AttendanceImportApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/attendance-import";

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

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
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
		content.Add(file, "File", "attendance.csv");
		return content;
	}
}

