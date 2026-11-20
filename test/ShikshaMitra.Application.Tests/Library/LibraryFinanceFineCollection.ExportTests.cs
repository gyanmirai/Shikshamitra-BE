using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClosedXML.Excel;
using QuestPDF.Infrastructure;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.HR.Reports;
using ShikshaMitra.Library.Reports;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Fine_Collection_Should_Export_The_Same_Canonical_Text_And_Recheck_Current_Report_Authority()
	{
		QuestPDF.Settings.License = LicenseType.Community;
		var fixture = await SeedTypedRefundAsync(FinanceExternalPayerKind.Employee);
		var owner = fixture.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var sourceBefore = await CaptureDashboardClockSourceAsync(owner);
		var denied = await RestrictFineReportReadAsync(owner, allowExport: true);
		await AssertFineReportAuthorityAsync(owner, denied, allowExport: true);
		var actual = await FineReportPositionAsync(owner);
		actual.Evidence.ShouldBe(FinanceExternalPositionEvidence.Verified);
		actual.Settled.ShouldBe(40.25m); actual.FinancialOutstanding.ShouldBe(60.50m);
		var services = FineReportServices(owner);
		var request = FineReportRequest(owner);
		request.Format = LibraryReportFormat.Excel;
		var preview = await WithUnitOfWorkAsync(() => services.Reports.PreviewAsync(request));
		preview.TotalCount.ShouldBe(1); preview.Rows.ShouldHaveSingleItem()["amount"].ShouldBe("100.75");

		var created = await WithUnitOfWorkAsync(() => services.Reports.CreateExportAsync(request));
		var queued = await CaptureFineCollectionExportEvidenceAsync(created.JobId);
		queued.Status.ShouldBe(HrReportExportStatus.Queued);
		await ExecuteStoredReportAsUnprivilegedReviewerAsync(created.JobId);
		var completed = await CaptureFineCollectionExportEvidenceAsync(created.JobId);
		completed.Status.ShouldBe(HrReportExportStatus.Completed);
		completed.RowCount.ShouldBe(preview.TotalCount);
		completed.RequestJson.ShouldBe(queued.RequestJson); completed.FilterJson.ShouldBe(queued.FilterJson);
		completed.PropertiesJson.ShouldBe(queued.PropertiesJson);
		var download = await WithUnitOfWorkAsync(() => services.Reports.DownloadExportAsync(created.JobId));
		download.ContentType.ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
		download.Bytes.ShouldBe(completed.Bytes);
		using var workbook = new XLWorkbook(new MemoryStream(download.Bytes));
		var sheet = workbook.Worksheet(1);
		sheet.LastRowUsed()!.RowNumber().ShouldBe(2);
		// Verify the real renderer and every selected preview cell first. Missing
		// canonical fields are asserted only after all artifact/authority controls.
		for (var index = 0; index < preview.Columns.Count; index++)
		{
			var column = preview.Columns[index];
			sheet.Cell(1, index + 1).GetString().ShouldBe(column.Label);
			sheet.Cell(2, index + 1).GetString().ShouldBe(preview.Rows[0].GetValueOrDefault(column.Id) ?? "");
		}

		var pdfRequest = FineReportRequest(owner);
		pdfRequest.Format = LibraryReportFormat.Pdf;
		var pdfCreated = await WithUnitOfWorkAsync(() => services.Reports.CreateExportAsync(pdfRequest));
		await ExecuteStoredReportAsUnprivilegedReviewerAsync(pdfCreated.JobId);
		var pdfCompleted = await CaptureFineCollectionExportEvidenceAsync(pdfCreated.JobId);
		pdfCompleted.Status.ShouldBe(HrReportExportStatus.Completed); pdfCompleted.RowCount.ShouldBe(1);
		var pdf = await WithUnitOfWorkAsync(() => services.Reports.DownloadExportAsync(pdfCreated.JobId));
		pdf.ContentType.ShouldBe("application/pdf");
		Encoding.ASCII.GetString(pdf.Bytes.Take(5).ToArray()).ShouldBe("%PDF-");
		pdf.Bytes.ShouldBe(pdfCompleted.Bytes);
		// This proves actual PDF generation and authorized artifact delivery, not
		// extracted semantic text. Unicode/text proof stays in the HTTP project.

		await WithUnitOfWorkAsync(() => GetRequiredService<IPermissionManager>().SetAsync(
			ShikshaMitraSubmodulePermissions.Library.Reports.View, "R", $"external-finance-{owner.ActorId:N}", false));
		await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(() =>
			services.Reports.PreviewAsync(FineReportRequest(owner))));
		await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(() =>
			services.Reports.DownloadExportAsync(created.JobId)));
		await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(() =>
			services.Reports.DownloadExportAsync(pdfCreated.JobId)));
		await AssertFineCollectionExportRetainedAsync(created.JobId, completed);
		await AssertFineCollectionExportRetainedAsync(pdfCreated.JobId, pdfCompleted);
		(await CaptureDashboardClockSourceAsync(owner)).ShouldBe(sourceBefore);

		// The behavioral RED is missing canonical report text after proven
		// generation, unchanged evidence, own-action scope and revocation controls.
		var settledColumn = preview.Columns.FindIndex(column => column.Id == "settledAmountText");
		var outstandingColumn = preview.Columns.FindIndex(column => column.Id == "financialOutstandingAmountText");
		settledColumn.ShouldBeGreaterThanOrEqualTo(0);
		outstandingColumn.ShouldBeGreaterThanOrEqualTo(0);
		sheet.Cell(1, settledColumn + 1).GetString().ShouldBe(preview.Columns[settledColumn].Label);
		sheet.Cell(2, settledColumn + 1).GetString().ShouldBe("40.25");
		sheet.Cell(1, outstandingColumn + 1).GetString().ShouldBe(preview.Columns[outstandingColumn].Label);
		sheet.Cell(2, outstandingColumn + 1).GetString().ShouldBe("60.50");

		async Task ExecuteStoredReportAsUnprivilegedReviewerAsync(Guid jobId)
		{
			using var reviewer = SignIn(fixture.Settlement.Reviewer);
			await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(() =>
				services.Scope.ResolveAuthorityAsync(fixture.Settlement.Reviewer.ActorId, requireExport: true)));
			await WithUnitOfWorkAsync(() => services.Worker.ExecuteAsync(new LibraryReportExportJobArgs
			{
				JobId = jobId, TenantId = owner.TenantId, UserId = owner.ActorId,
				Request = new() { ReportType = "Untrusted queued report", Format = "Untrusted queued format" },
				ExecutionScope = new() { TenantId = Guid.NewGuid(), UserScope = new() { IsAdmin = true } }
			}));
		}
	}
}

