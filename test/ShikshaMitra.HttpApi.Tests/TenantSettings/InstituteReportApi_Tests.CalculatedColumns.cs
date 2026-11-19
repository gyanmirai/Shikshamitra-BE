using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Reports;
using ShikshaMitra.TenantSettings.Reports;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public partial class InstituteReportApi_Tests
{
	[Theory]
	[InlineData("pdf")]
	[InlineData("excel")]
	[InlineData("csv")]
	public async Task Requested_shift_hours_are_calculated_in_preview_and_generated_files(string format)
	{
		var (client, tenant, _) = await ClientAsync(); using var http = client;
		using (var scope = factory.Services.CreateScope())
		{
			using var school = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenant);
			using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
			var shifts = scope.ServiceProvider.GetRequiredService<IRepository<ShiftManagement, Guid>>();
			var morning = (await shifts.GetListAsync()).Single(); morning.StartTime = new(7, 0); morning.EndTime = new(12, 0);
			await shifts.UpdateAsync(morning);
			var night = new ShiftManagement { TenantId = tenant, ShiftName = "Night", StartTime = new(22, 0), EndTime = new(6, 0) };
			typeof(ShiftManagement).GetProperty(nameof(ShiftManagement.Id))!.SetValue(night, Guid.NewGuid());
			await shifts.InsertAsync(night); await unit.CompleteAsync();
		}
		factory.AiResult = new() { Summary = "List shifts with calculated total hours.", Plan = new() {
			ReportType = "shift-register", Format = format, Columns = ["shift", "start", "end", "totalHours"], SortField = "totalHours"
		} };
		var response = await client.PostAsJsonAsync(Url + "/ai/plan", new InstituteAiPlanInput { Prompt = $"List all shifts with start time, end time and one extra total hours column in {format}" });
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var plan = (await response.Content.ReadFromJsonAsync<InstituteAiPlanResult>())!;
		factory.LastAiRequest!.ShouldContain("totalHours");
		var approved = new InstituteAiApprovedRequest { Plan = plan.Plan!, ApprovalToken = plan.ApprovalToken! };
		var previewResponse = await client.PostAsJsonAsync(Url + "/ai/preview", approved);
		previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await previewResponse.Content.ReadAsStringAsync());
		var preview = (await previewResponse.Content.ReadFromJsonAsync<AcademicReportPreviewDto>())!;
		preview.Columns.Last().Id.ShouldBe("totalHours");
		preview.Rows.Select(x => x["totalHours"]).ShouldBe(["5", "8"]);
		preview.FilterSummary.ShouldContain("next day");
		var queued = await client.PostAsJsonAsync(Url + "/ai/exports", approved);
		queued.StatusCode.ShouldBe(HttpStatusCode.OK, await queued.Content.ReadAsStringAsync());
		var job = (await queued.Content.ReadFromJsonAsync<InstituteReportJobDto>())!;
		using (var scope = factory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<InstituteReportExportJob>().ExecuteAsync(new() { JobId = job.Id, TenantId = tenant });
		var bytes = await client.GetByteArrayAsync($"{Url}/exports/{job.Id}/download");
		if (format == "csv") {
			var csv = Encoding.UTF8.GetString(bytes); csv.ShouldContain("Shift,Start time,End time,Total hours");
			csv.ShouldContain("Morning,07:00,12:00,5"); csv.ShouldContain("Night,22:00,06:00,8");
		}
		if (format == "excel") {
			using var workbook = new ClosedXML.Excel.XLWorkbook(new MemoryStream(bytes)); var sheet = workbook.Worksheets.First();
			sheet.Cell(1, 4).GetString().ShouldBe("Total hours"); sheet.Cell(2, 4).GetString().ShouldBe("5"); sheet.Cell(3, 4).GetString().ShouldBe("8");
		}
		if (format == "pdf") Encoding.ASCII.GetString(bytes[..5]).ShouldBe("%PDF-");
		approved.Plan.Conditions = [new() { Field = "totalHours", Operator = "gt", Value = "5" }];
		var filtered = await client.PostAsJsonAsync(Url + "/preview", approved.Plan);
		(await filtered.Content.ReadFromJsonAsync<AcademicReportPreviewDto>())!.Rows.Single()["shift"].ShouldBe("Night");
	}
}
