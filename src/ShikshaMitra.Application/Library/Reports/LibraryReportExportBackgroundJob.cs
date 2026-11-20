using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Reports;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Data;
using Volo.Abp.Security.Claims;

namespace ShikshaMitra.Library.Reports;

public class LibraryReportExportBackgroundJob(
	LibraryReportGenerator reportGenerator,
	LibraryPdfService pdfService,
	LibraryExcelService excelService,
	IHrReportExportJobRepository jobRepository,
	IBlobContainer blobContainer,
	ICurrentTenant currentTenant,
	LibraryReportScopeService scopeService,
	ICurrentPrincipalAccessor principalAccessor,
	ILogger<LibraryReportExportBackgroundJob> logger)
	: AsyncBackgroundJob<LibraryReportExportJobArgs>, ITransientDependency
{
	[UnitOfWork]
	public override async Task ExecuteAsync(LibraryReportExportJobArgs args)
	{
		using (currentTenant.Change(args.TenantId))
		{
			var job = await jobRepository.GetAsync(args.JobId);
			if (job.Status != HrReportExportStatus.Queued) return;

			try
			{
				if (job.TenantId != args.TenantId || job.RequestedByUserId != args.UserId) throw new AbpAuthorizationException();
				EnsureNotExpired(job);
				var authority = await scopeService.ResolveAuthorityAsync(job.RequestedByUserId, requireExport: true, includeMemberPopulation: true);
				using var principal = principalAccessor.Change(authority.Principal);
				scopeService.EnsureStoredScope(job, authority.Scope, authority.MemberSignature);
				var request = JsonSerializer.Deserialize<LibraryReportRequestDto>(job.RequestJson)
					?? throw new BusinessException(LibraryErrorCodes.ReportRequestInvalid);
				scopeService.ValidateAndNormalizeForScope(request, authority.Scope);
				if (request.ReportType != job.ReportType || !string.Equals(request.Format, job.Format, StringComparison.OrdinalIgnoreCase))
					throw new BusinessException(LibraryErrorCodes.ReportRequestInvalid);
				job.MarkRunning(DateTime.UtcNow);
				await jobRepository.UpdateAsync(job, autoSave: true);
				var report = await reportGenerator.GenerateAsync(request, authority.Scope, exportAllRows: true);
				var format = string.Equals(request.Format, LibraryReportFormat.Pdf, StringComparison.OrdinalIgnoreCase)
					? LibraryReportFormat.Pdf
					: LibraryReportFormat.Excel;

				var bytes = format == LibraryReportFormat.Pdf
					? await pdfService.GenerateReportPdfAsync(report)
					: excelService.GenerateReportExcel(report);

				await EnsureCurrentAuthorityAsync(job);
				var blobName = BuildBlobName(job, format);
				await blobContainer.SaveAsync(blobName, bytes, overrideExisting: true);
				await EnsureCurrentAuthorityAsync(job);

				job.MarkCompleted(blobName, DateTime.UtcNow, report.TotalCount, bytes.LongLength);
				await jobRepository.UpdateAsync(job, autoSave: true);
			}
			catch (AbpDbConcurrencyException)
			{
				throw;
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Failed to execute Library report export {JobId}", args.JobId);
				job.MarkFailed(ex is AbpAuthorizationException || ex is BusinessException { Code: LibraryErrorCodes.ReportScopeAccessDenied }
					? "Report access changed. Generate a new report with your current access."
					: "The Library export could not be generated. Review the request and generate a new report.", DateTime.UtcNow);
				await jobRepository.UpdateAsync(job, autoSave: true);
			}
		}
	}

	private async Task EnsureCurrentAuthorityAsync(HrReportExportJob job)
	{
		EnsureNotExpired(job);
		var authority = await scopeService.ResolveAuthorityAsync(job.RequestedByUserId, requireExport: true, includeMemberPopulation: true);
		scopeService.EnsureStoredScope(job, authority.Scope, authority.MemberSignature);
	}

	private static void EnsureNotExpired(HrReportExportJob job)
	{
		if (DateTime.UtcNow > job.ExpiresAt) throw new BusinessException(LibraryErrorCodes.ReportExportExpired);
	}

	private static string BuildBlobName(HrReportExportJob job, string format)
	{
		var extension = format == LibraryReportFormat.Pdf ? "pdf" : "xlsx";
		return $"library-exports/{job.TenantId}/{job.RequestedByUserId}/{job.Id}.{extension}";
	}
}
