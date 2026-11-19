using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.HR.Reports;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Collections;
using Volo.Abp.Application.Services;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.ObjectMapping;
using Volo.Abp;

namespace ShikshaMitra.Finance.Reports;

[Authorize(ShikshaMitraSubModulePermissions.FinanceReports)]
[RemoteService(IsEnabled = false)]
public class FinanceReportAppService(
    FinanceReportGenerator reportGenerator,
    FinanceReportScopeService reportScopeService,
    IFinanceAiReportPlanner aiReportPlanner,
    IBackgroundJobManager backgroundJobManager,
    IHrReportExportJobRepository jobRepository,
    IRepository<FeeHead, Guid> feeHeads,
    IRepository<ReceiptBook, Guid> receiptBooks,
    IBlobContainer blobContainer)
    : ApplicationService, IFinanceReportAppService
{
    internal const string UsesApplicationClock = "FinanceUsesApplicationClock";
    public async Task<FinanceReportOptionsDto> GetOptionsAsync()
    {
        var allowedAcademicYearIds = await reportScopeService.GetAccessibleAcademicYearIdsAsync();
        var options = await reportGenerator.GetOptionsAsync(allowedAcademicYearIds);
        var allowedReports = await reportScopeService.GetAllowedReportTypesAsync();
        options.ReportTypes = options.ReportTypes.Where(report => allowedReports.Contains(report.Id)).ToList();
        if (options.ReportTypes.Count == 0)
            return new FinanceReportOptionsDto();
        var aggregateYears = await reportScopeService.GetAggregateAcademicYearIdsAsync();
        foreach (var year in options.AcademicYears)
            year.YearWideOnly = aggregateYears.Contains(Guid.Parse(year.Id));
        var summaryYearWide = await reportScopeService.SummaryRequiresYearWideScopeAsync();
        foreach (var report in options.ReportTypes)
            report.YearWideOnly = FinanceReportRequestGuard.RequiresAcademicYearWideScope(report.Id) ||
                (report.Id == FinanceReportTypes.FeeDemandSummary && summaryYearWide);

        var hierarchy = await reportScopeService.GetAccessibleHierarchyAsync();
        options.Scope = hierarchy.Select(row => new FinanceReportScopeOptionDto
        {
            AcademicYearId = row.AcademicYearId.ToString(),
            Board = Option(row.BoardId, row.BoardName), Medium = Option(row.MediumId, row.MediumName),
            Stage = Option(row.StageId, row.StageName), Class = Option(row.StandardId, row.StandardName),
            Divisions = row.Divisions.Select(division => Option(division.Id, division.Name)).ToList()
        }).ToList();
        var years = options.AcademicYears.Select(year => Guid.Parse(year.Id)).ToArray();
        var stages = hierarchy.Select(row => row.StageId).Distinct().ToArray();
        options.FeeHeads = (await feeHeads.GetListAsync(head => Enumerable.Contains(years, head.AcademicYearId)))
            .Select(head => Option(head.Id, head.Name, head.AcademicYearId)).OrderBy(head => head.Label).ToList();
        options.ReceiptBooks = (await receiptBooks.GetListAsync(book => Enumerable.Contains(years, book.AcademicYearId) &&
                Enumerable.Contains(stages, book.MediumStageId)))
            .Select(book => Option(book.Id, book.Name, book.AcademicYearId)).OrderBy(book => book.Label).ToList();
        options.PaymentMethods = Enum.GetNames<PaymentMethodEnum>().Select(method => new FinanceReportOptionDto
            { Id = method, Label = method switch { "DemandDraft" => "Demand draft", "BankTransfer" => "Bank transfer",
                "CreditCard" => "Credit card", "DebitCard" => "Debit card", _ => method } }).ToList();
        return options;
    }

    private static FinanceReportOptionDto Option(Guid id, string label, Guid? yearId = null) => new()
        { Id = id.ToString(), Label = label, AcademicYearId = yearId?.ToString() };

    public async Task<FinanceAiReportPlanResponseDto> CreateAiPlanAsync(FinanceAiReportPlanRequestDto input)
    {
        var userId = CurrentUser.Id ?? throw new UnauthorizedAccessException();
        return await aiReportPlanner.CreatePlanAsync(input, userId);
    }

    public async Task<FinanceReportPreviewDto> PreviewAsync(FinanceReportRequestDto input)
    {
        var executionScope = await reportScopeService.AuthorizeAndNormalizeAsync(input);
        return await reportGenerator.GenerateAsync(input, executionScope, exportAllRows: false);
    }

    public async Task<FinanceReportExportCreateResponseDto> CreateExportAsync(FinanceReportRequestDto input)
    {
        var executionScope = await reportScopeService.AuthorizeAndNormalizeAsync(input);
        var tenantId = CurrentTenant.Id;
        var userId = CurrentUser.Id ?? throw new UnauthorizedAccessException();
        var jobId = GuidGenerator.Create();
        var expiresAt = Clock.Now.AddDays(7);

        var filterJson = System.Text.Json.JsonSerializer.Serialize(input.Filters);
        var requestJson = System.Text.Json.JsonSerializer.Serialize(input);

        var job = new HrReportExportJob(
            jobId,
            tenantId,
            userId,
            input.ReportType,
            input.Format,
            filterJson,
            requestJson,
            expiresAt);
        job.ExtraProperties["FinanceScope"] = Fingerprint(executionScope);
        job.ExtraProperties[UsesApplicationClock] = true;

        await jobRepository.InsertAsync(job, autoSave: true);

        await backgroundJobManager.EnqueueAsync(new FinanceReportExportJobArgs
        {
            JobId = jobId,
            TenantId = tenantId,
            UserId = userId,
            Request = input,
            ExecutionScope = executionScope
        }, delay: TimeSpan.FromSeconds(2));

        return new FinanceReportExportCreateResponseDto
        {
            JobId = job.Id,
            Status = job.Status
        };
    }

    public async Task<List<FinanceReportExportDto>> GetExportsAsync()
    {
        var owner = CurrentUser.Id ?? throw new UnauthorizedAccessException();
        var types = (await reportScopeService.GetAllowedReportTypesAsync()).ToArray();
        var tenantId = CurrentTenant.Id;
        var jobs = await AsyncExecuter.ToListAsync((await jobRepository.GetQueryableAsync())
            .Where(job => job.TenantId == tenantId && job.RequestedByUserId == owner &&
                Enumerable.Contains(types, job.ReportType))
            .OrderByDescending(job => job.CreationTime).ThenByDescending(job => job.Id).Take(100));
        return jobs.Select(job => new FinanceReportExportDto
        {
            JobId = job.Id, ReportType = job.ReportType, Format = job.Format,
            Status = IsExpired(job) ? "Expired" : job.Status, CreatedAt = job.CreationTime,
            CompletedAt = job.CompletedAt.HasValue ? DisplayJobTime(job, job.CompletedAt.Value) : null,
            ExpiresAt = DisplayJobTime(job, job.ExpiresAt)
        }).ToList();
    }

    public async Task<FinanceReportDownloadDto> DownloadExportAsync(Guid jobId)
    {
        var job = await jobRepository.GetAsync(jobId);
        if (job.TenantId != CurrentTenant.Id || job.RequestedByUserId != CurrentUser.Id ||
            !FinanceReportRequestGuard.SupportedReportTypes.Contains(job.ReportType))
        {
            throw new Volo.Abp.Authorization.AbpAuthorizationException();
        }
        var request = JsonSerializer.Deserialize<FinanceReportRequestDto>(job.RequestJson);
        if (request?.ReportType != job.ReportType)
            throw new Volo.Abp.Authorization.AbpAuthorizationException();
        var currentScope = await reportScopeService.AuthorizeAndNormalizeAsync(request);
        if (job.ExtraProperties.TryGetValue("FinanceScope", out var fingerprint)
            ? !string.Equals(fingerprint?.ToString(), Fingerprint(currentScope), StringComparison.Ordinal)
            : !currentScope.UserScope.HasTenantWideScope())
            throw new Volo.Abp.Authorization.AbpAuthorizationException("Generate a new report for your current scope.");

        if (IsExpired(job))
        {
            throw new BusinessException(FinanceReportRequestGuard.ExportExpired);
        }

        if (!string.Equals(job.Status, "Completed", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrEmpty(job.BlobName))
        {
            throw new BusinessException(FinanceReportRequestGuard.ExportNotReady)
                .WithData("Status", job.Status);
        }
        if (!job.BlobName.StartsWith($"finance-exports/{job.TenantId}/{job.RequestedByUserId}/", StringComparison.Ordinal))
            throw new Volo.Abp.Authorization.AbpAuthorizationException();

        var bytes = await blobContainer.GetAllBytesOrNullAsync(job.BlobName);
        if (bytes == null)
        {
            throw new BusinessException(FinanceReportRequestGuard.ExportNotFound);
        }

        return new FinanceReportDownloadDto
        {
            Bytes = bytes,
            FileName = $"{job.ReportType} {job.CreationTime:yyyy-MM-dd}.{(job.Format == "pdf" ? "pdf" : "xlsx")}",
            ContentType = string.Equals(job.Format, "pdf", StringComparison.OrdinalIgnoreCase)
                ? "application/pdf" 
                : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };
    }

    private bool IsExpired(HrReportExportJob job) =>
        job.ExpiresAt <= (job.ExtraProperties.ContainsKey(UsesApplicationClock) ? Clock.Now : DateTime.UtcNow);

    private static DateTime DisplayJobTime(HrReportExportJob job, DateTime value) =>
        job.ExtraProperties.ContainsKey(UsesApplicationClock) ? value :
            DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime();

    private static string Fingerprint(FinanceReportExecutionScope execution)
    {
        var scope = execution.UserScope;
        var scopeValues = new[] { scope.AcademicYearIds, scope.BoardIds, scope.MediumIds, scope.MediumStageIds,
            scope.StandardIds, scope.DivisionIds, scope.WildcardBoardAcademicYearIds, scope.WildcardMediumBoardIds,
            scope.WildcardMediumStageMediumIds, scope.WildcardStandardMediumStageIds, scope.WildcardDivisionStandardIds,
            execution.AllowedWholeStandardIds, execution.AllowedDivisionIds };
        return JsonSerializer.Serialize(new
        {
            scope.IsAdmin, scope.HasWildcardAcademicYear, scope.HasWildcardBoard, scope.HasWildcardMedium,
            scope.HasWildcardMediumStage, scope.HasWildcardStandard, scope.HasWildcardDivision,
            Ids = scopeValues.Select(values => values.Order().ToArray())
        });
    }
}

public class FinanceReportExportJobArgs
{
    public Guid JobId { get; set; }
    public Guid? TenantId { get; set; }
    public Guid UserId { get; set; }
    public FinanceReportRequestDto Request { get; set; } = null!;
    public FinanceReportExecutionScope ExecutionScope { get; set; } = null!;
}
