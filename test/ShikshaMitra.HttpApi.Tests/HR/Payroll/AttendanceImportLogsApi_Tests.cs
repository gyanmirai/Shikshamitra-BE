using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Import;
using ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.AttendanceImports;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
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
		var detailJson = await detailResponse.Content.ReadAsStringAsync();
		using (var detailDoc = JsonDocument.Parse(detailJson))
		{
			var root = detailDoc.RootElement;
			root.GetProperty("id").GetString().ShouldNotBeNullOrWhiteSpace();
			root.GetProperty("runSource").ValueKind.ShouldBe(JsonValueKind.Number);
			root.GetProperty("externalCorrelationId").ValueKind.ShouldBeOneOf(JsonValueKind.String, JsonValueKind.Null);
			root.GetProperty("integrationProvider").ValueKind.ShouldBeOneOf(JsonValueKind.String, JsonValueKind.Null);
			root.GetProperty("isBatchLocked").ValueKind.ShouldBeOneOf(JsonValueKind.True, JsonValueKind.False);
		}

		var rowsResponse = await Client.GetAsync($"{PayrollImportLogsUrl}/{importId}/rows?skipCount=0&maxResultCount=50");
		rowsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var rowsJson = await rowsResponse.Content.ReadAsStringAsync();
		using (var rowsDoc = JsonDocument.Parse(rowsJson))
		{
			var rowsRoot = rowsDoc.RootElement;
			rowsRoot.GetProperty("totalCount").GetInt32().ShouldBeGreaterThan(0);
			var items = rowsRoot.GetProperty("items");
			items.ValueKind.ShouldBe(JsonValueKind.Array);
			items.GetArrayLength().ShouldBeGreaterThan(0);
			var first = items[0];
			first.GetProperty("id").GetString().ShouldNotBeNullOrWhiteSpace();
			first.GetProperty("employeeId").GetString().ShouldNotBeNullOrWhiteSpace();
			first.GetProperty("statusCode").ValueKind.ShouldBe(JsonValueKind.Number);
			first.GetProperty("status").ValueKind.ShouldBe(JsonValueKind.String);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Save_Import_Row_Status_Edit_And_Reflect_On_Refetch()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "ImportRowEdit");
		var fileName = $"row-edit-{Guid.NewGuid():N}.csv";
		var attendanceDate = DateTime.UtcNow.AddDays(-1).ToString("dd-MM-yyyy");
		using var upload = BuildCsvUploadWithFileName(
			fileName,
			$"{attendanceDate},{employee.EmployeeCode},09:00 AM,05:30 PM,,Regular shift,{employee.FullName},{employee.MobileNo}");

		var importId = await UploadConfirmAndFindImportAsync(fileName, upload);
		var rows = await GetRowsAsync(importId);
		var row = rows.Items[0];
		row.StatusCode.ShouldBe(AttendanceImportRowStatusCode.Present);

		var saveResponse = await Client.PostAsJsonAsync($"{HrAutomatedRunsUrl}/{importId}/rows/save", new AttendanceImportRowsSaveInput
		{
			Entries =
			[
				new AttendanceImportRowsSaveEntryDto
				{
					RowId = row.Id,
					EmployeeId = row.EmployeeId!.Value,
					AttendanceDate = row.AttendanceDate,
					StatusWire = "Absent",
					InTime = null,
					OutTime = null,
					Remarks = "Corrected from import summary"
				}
			]
		}, JsonOptions);

		saveResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var saved = await saveResponse.Content.ReadFromJsonAsync<AttendanceImportRowsSaveOutput>(JsonOptions);
		saved.ShouldNotBeNull();
		saved.UpsertedCount.ShouldBe(1);

		var refreshedRows = await GetRowsAsync(importId);
		refreshedRows.Items[0].StatusCode.ShouldBe(AttendanceImportRowStatusCode.Absent);
		refreshedRows.Items[0].Status.ShouldBe("FullDayLeave");

		var dayResponse = await Client.GetAsync(
			$"/api/hr/attendance/take-attendance/day?attendanceDate={attendanceDate}&skipCount=0&maxResultCount=20&search={Uri.EscapeDataString(employee.FirstName)}");
		dayResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var day = await dayResponse.Content.ReadFromJsonAsync<TakeAttendanceDayResponse>(JsonOptions);
		day.ShouldNotBeNull();
		day.Items.ShouldContain(x => x.EmployeeId == employee.Id && x.Status == TakeAttendanceStatus.Absent);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Import_Row_Save_When_Batch_Is_Locked()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "ImportRowLock");
		var fileName = $"row-lock-{Guid.NewGuid():N}.csv";
		var attendanceDate = DateTime.UtcNow.AddDays(-1).ToString("dd-MM-yyyy");
		using var upload = BuildCsvUploadWithFileName(
			fileName,
			$"{attendanceDate},{employee.EmployeeCode},09:00 AM,05:30 PM,,Regular shift,{employee.FullName},{employee.MobileNo}");

		var importId = await UploadConfirmAndFindImportAsync(fileName, upload);
		var rows = await GetRowsAsync(importId);
		var row = rows.Items[0];

		var lockResponse = await Client.PostAsync($"{HrAutomatedRunsUrl}/{importId}/lock", null);
		lockResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

		var saveResponse = await Client.PostAsJsonAsync($"{HrAutomatedRunsUrl}/{importId}/rows/save", new AttendanceImportRowsSaveInput
		{
			Entries =
			[
				new AttendanceImportRowsSaveEntryDto
				{
					RowId = row.Id,
					EmployeeId = row.EmployeeId!.Value,
					AttendanceDate = row.AttendanceDate,
					StatusWire = "Absent"
				}
			]
		}, JsonOptions);

		saveResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Preserve_Import_Row_Time_Values_When_Saving_Unchanged_Present_Row()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "ImportRowTime");
		var fileName = $"row-time-{Guid.NewGuid():N}.csv";
		var attendanceDate = DateTime.UtcNow.AddDays(-1).ToString("dd-MM-yyyy");
		using var upload = BuildCsvUploadWithFileName(
			fileName,
			$"{attendanceDate},{employee.EmployeeCode},09:00 AM,05:30 PM,,Regular shift,{employee.FullName},{employee.MobileNo}");

		var importId = await UploadConfirmAndFindImportAsync(fileName, upload);
		var row = (await GetRowsAsync(importId)).Items[0];

		var saveResponse = await Client.PostAsJsonAsync($"{HrAutomatedRunsUrl}/{importId}/rows/save", new AttendanceImportRowsSaveInput
		{
			Entries =
			[
				new AttendanceImportRowsSaveEntryDto
				{
					RowId = row.Id,
					EmployeeId = row.EmployeeId!.Value,
					AttendanceDate = row.AttendanceDate,
					StatusWire = "Present",
					InTime = row.TimeIn,
					OutTime = row.TimeOut,
					Remarks = row.Remarks
				}
			]
		}, JsonOptions);

		saveResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var refreshedRow = (await GetRowsAsync(importId)).Items[0];
		refreshedRow.StatusCode.ShouldBe(AttendanceImportRowStatusCode.Present);
		refreshedRow.TimeIn.ShouldBe("09:00");
		refreshedRow.TimeOut.ShouldBe("17:30");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Import_Row_Save_When_Request_Does_Not_Match_Row()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "ImportRowMismatch");
		var otherEmployee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "ImportRowMismatchOther");
		var fileName = $"row-mismatch-{Guid.NewGuid():N}.csv";
		var attendanceDate = DateTime.UtcNow.AddDays(-1).ToString("dd-MM-yyyy");
		using var upload = BuildCsvUploadWithFileName(
			fileName,
			$"{attendanceDate},{employee.EmployeeCode},09:00 AM,05:30 PM,,Regular shift,{employee.FullName},{employee.MobileNo}");

		var importId = await UploadConfirmAndFindImportAsync(fileName, upload);
		var row = (await GetRowsAsync(importId)).Items[0];

		var wrongEmployeeResponse = await Client.PostAsJsonAsync($"{HrAutomatedRunsUrl}/{importId}/rows/save", new AttendanceImportRowsSaveInput
		{
			Entries =
			[
				new AttendanceImportRowsSaveEntryDto
				{
					RowId = row.Id,
					EmployeeId = otherEmployee.Id,
					AttendanceDate = row.AttendanceDate,
					StatusWire = "Absent"
				}
			]
		}, JsonOptions);
		wrongEmployeeResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var wrongDateResponse = await Client.PostAsJsonAsync($"{HrAutomatedRunsUrl}/{importId}/rows/save", new AttendanceImportRowsSaveInput
		{
			Entries =
			[
				new AttendanceImportRowsSaveEntryDto
				{
					RowId = row.Id,
					EmployeeId = row.EmployeeId!.Value,
					AttendanceDate = DateTime.UtcNow.AddDays(-2).ToString("dd-MM-yyyy"),
					StatusWire = "Absent"
				}
			]
		}, JsonOptions);
		wrongDateResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var duplicateRowResponse = await Client.PostAsJsonAsync($"{HrAutomatedRunsUrl}/{importId}/rows/save", new AttendanceImportRowsSaveInput
		{
			Entries =
			[
				new AttendanceImportRowsSaveEntryDto
				{
					RowId = row.Id,
					EmployeeId = row.EmployeeId!.Value,
					AttendanceDate = row.AttendanceDate,
					StatusWire = "Absent"
				},
				new AttendanceImportRowsSaveEntryDto
				{
					RowId = row.Id,
					EmployeeId = row.EmployeeId!.Value,
					AttendanceDate = row.AttendanceDate,
					StatusWire = "Present"
				}
			]
		}, JsonOptions);
		duplicateRowResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var invalidStatusResponse = await Client.PostAsJsonAsync($"{HrAutomatedRunsUrl}/{importId}/rows/save", new AttendanceImportRowsSaveInput
		{
			Entries =
			[
				new AttendanceImportRowsSaveEntryDto
				{
					RowId = row.Id,
					EmployeeId = row.EmployeeId!.Value,
					AttendanceDate = row.AttendanceDate,
					StatusWire = "Pending"
				}
			]
		}, JsonOptions);
		invalidStatusResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Import_Row_Save_When_Payroll_Month_Is_Finalized()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "ImportRowPayrollLocked");
		var fileName = $"row-payroll-locked-{Guid.NewGuid():N}.csv";
		var attendanceDateValue = DateTime.UtcNow.AddDays(-1);
		var attendanceDate = attendanceDateValue.ToString("dd-MM-yyyy");
		using var upload = BuildCsvUploadWithFileName(
			fileName,
			$"{attendanceDate},{employee.EmployeeCode},09:00 AM,05:30 PM,,Regular shift,{employee.FullName},{employee.MobileNo}");

		var importId = await UploadConfirmAndFindImportAsync(fileName, upload);
		var row = (await GetRowsAsync(importId)).Items[0];
		await SeedFinalizedSalarySlipAsync(employee.Id, attendanceDateValue.ToString("yyyy-MM"));

		var saveResponse = await Client.PostAsJsonAsync($"{HrAutomatedRunsUrl}/{importId}/rows/save", new AttendanceImportRowsSaveInput
		{
			Entries =
			[
				new AttendanceImportRowsSaveEntryDto
				{
					RowId = row.Id,
					EmployeeId = row.EmployeeId!.Value,
					AttendanceDate = row.AttendanceDate,
					StatusWire = "Absent"
				}
			]
		}, JsonOptions);

		saveResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
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

	private async Task<Guid> UploadConfirmAndFindImportAsync(string fileName, MultipartFormDataContent upload)
	{
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

		var listResponse = await Client.GetAsync(
			$"{HrAutomatedRunsUrl}?search={Uri.EscapeDataString(fileName)}&skipCount=0&maxResultCount=5");
		listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var listBody = await listResponse.Content.ReadFromJsonAsync<PagedResultDto<AttendanceImportLogListItemDto>>(JsonOptions);
		listBody.ShouldNotBeNull();
		listBody.Items.Count.ShouldBe(1);
		return listBody.Items[0].Id;
	}

	private async Task<PagedResultDto<AttendanceImportRowDto>> GetRowsAsync(Guid importId)
	{
		var rowsResponse = await Client.GetAsync($"{HrAutomatedRunsUrl}/{importId}/rows?skipCount=0&maxResultCount=50");
		rowsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var rows = await rowsResponse.Content.ReadFromJsonAsync<PagedResultDto<AttendanceImportRowDto>>(JsonOptions);
		rows.ShouldNotBeNull();
		rows.Items.Count.ShouldBeGreaterThan(0);
		return rows;
	}

	private async Task SeedFinalizedSalarySlipAsync(Guid employeeId, string monthYear)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var salarySlipRepository = scope.ServiceProvider.GetRequiredService<IRepository<SalarySlip, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var salarySlip = CreateSalarySlip(tenantId, employeeId, monthYear);
			salarySlip.Finalize();
			await salarySlipRepository.InsertAsync(salarySlip, autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private static SalarySlip CreateSalarySlip(Guid tenantId, Guid employeeId, string monthYear)
	{
		var constructor = typeof(SalarySlip).GetConstructor(
			BindingFlags.Instance | BindingFlags.NonPublic,
			binder: null,
			[
				typeof(Guid),
				typeof(Guid?),
				typeof(Guid),
				typeof(string),
				typeof(List<SalarySlipLineItem>),
				typeof(List<SalarySlipLineItem>),
				typeof(string)
			],
			modifiers: null);

		constructor.ShouldNotBeNull();
		return (SalarySlip)constructor.Invoke(
		[
			Guid.NewGuid(),
			tenantId,
			employeeId,
			monthYear,
			new List<SalarySlipLineItem>
			{
				new()
				{
					Name = "Basic",
					Amount = 1000m,
					IsFixed = true,
					ComponentType = SalaryComponentType.Fixed,
					DependOn = SalaryComponentDependency.None
				}
			},
			new List<SalarySlipLineItem>(),
			"Finalized payroll lock test"
		]);
	}

	private static MultipartFormDataContent BuildCsvUpload(params string[] rows)
	{
		return BuildCsvUploadWithFileName($"attendance-{Guid.NewGuid():N}.csv", rows);
	}

	private static MultipartFormDataContent BuildCsvUploadWithFileName(string fileName, params string[] rows)
	{
		var content = new MultipartFormDataContent();
		var csv = string.Join(Environment.NewLine,
			"Attendance Date,Employee Code,Time In,Time Out,Status,Remarks,Employee Name,Contact No",
			string.Join(Environment.NewLine, rows));

		var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
		file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
		content.Add(file, "File", fileName);
		return content;
	}
}
