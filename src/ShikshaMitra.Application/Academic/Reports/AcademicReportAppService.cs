using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Reports;
using ShikshaMitra.Reporting.Agent;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.BlobStoring;

namespace ShikshaMitra.Academic.Reports;

[RemoteService(false)]
[Authorize]
public class AcademicReportAppService(
    AcademicReportGenerator reportGenerator,
    IAcademicAiReportPlanner aiReportPlanner,
    AcademicDynamicReportCatalog dynamicReportCatalog,
    AcademicDynamicReportGuard dynamicReportGuard,
    AcademicDynamicReportQueryService dynamicReportQueryService,
    AcademicAiReportApprovalService aiReportApprovalService,
    ReportAgentPermissionService reportAgentPermissionService,
    IRepository<ShikshaMitra.HR.Reports.HrReportExportJob, Guid> exportJobRepository,
    IBackgroundJobManager backgroundJobManager,
    IBlobContainer blobContainer,
    IGuidGenerator guidGenerator,
    IUserScopeService userScopeService,
    IClassTeacherAccessService classTeacherAccessService,
    IRepository<Division, Guid> divisionRepository,
    IRepository<Standard, Guid> standardRepository,
    IRepository<EducationBoard, Guid> educationBoardRepository,
    IRepository<EducationMedium, Guid> educationMediumRepository) : ApplicationService, IAcademicReportAppService
{
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_Reports.View)]
    public async Task<AcademicReportOptionsDto> GetOptionsAsync()
    {
        var userId = CurrentUser.Id ?? throw new BusinessException("Academic:Reports:050003");
        return await reportGenerator.GetOptionsAsync(userId, CurrentTenant.Id);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_Reports.View)]
    public async Task<AcademicReportPreviewDto> PreviewAsync(AcademicReportRequestDto input)
    {
        var userId = CurrentUser.Id ?? throw new BusinessException("Academic:Reports:050003");
        input = reportGenerator.NormalizeAndValidateRequest(input, exportAllRows: false);
        await ApplyClassTeacherScopeAsync(input, userId);
        return await reportGenerator.GenerateAsync(input, userId, CurrentTenant.Id, exportAllRows: false);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_Reports.Add)]
    public async Task<AcademicReportExportCreateResponseDto> CreateExportAsync(AcademicReportRequestDto input)
    {
        var userId = CurrentUser.Id ?? throw new BusinessException("Academic:Reports:050003");
        return await CreateExportJobAsync(input, userId);
    }

    public async Task<AcademicAiReportPlanResponseDto> CreateAiPlanAsync(AcademicAiReportPlanRequestDto input)
    {
        var userId = CurrentUser.Id ?? throw new BusinessException("Academic:Reports:050003");
        await reportAgentPermissionService.EnsureCanBuildAsync(input.Module);
        var catalogScope = await GetAiCatalogScopeAsync(
            userId, input.AcademicYearId, input.EducationBoardId, input.EducationMediumId);
        var plan = await aiReportPlanner.CreatePlanAsync(
            input.Prompt, userId, input.Module, catalogScope, input.ClarificationAnswers);
        return new AcademicAiReportPlanResponseDto
        {
            Status = plan.Status,
            Plan = plan.Spec,
            ClarificationQuestions = plan.ClarificationQuestions.ToList(),
            Assumptions = plan.Assumptions.ToList(),
            Confidence = plan.Confidence,
            PermissionSummary = BuildPermissionSummary(catalogScope, input.Module),
            ApprovalToken = plan.Spec == null
                ? null
                : aiReportApprovalService.CreateToken(userId, CurrentTenant.Id, catalogScope, plan.Spec)
        };
    }

    public async Task<AcademicAiReportPreviewResponseDto> CreateAiPreviewAsync(
        AcademicAiReportPreviewRequestDto input)
    {
        var userId = CurrentUser.Id ?? throw new BusinessException("Academic:Reports:050003");
        await reportAgentPermissionService.EnsureCanBuildAsync(input.Module);
        var catalogScope = await GetAiCatalogScopeAsync(
            userId, input.AcademicYearId, input.EducationBoardId, input.EducationMediumId);
        var plan = await ResolveAiPlanAsync(input.Plan, input.Module, catalogScope);
        var preview = await dynamicReportQueryService.GenerateAsync(plan, catalogScope);
        const int previewRowLimit = 12;
        preview.Rows = preview.Rows.Take(previewRowLimit).ToList();
        preview.PageSize = preview.Rows.Count;

        return new AcademicAiReportPreviewResponseDto
        {
            Plan = plan,
            Preview = preview,
            PermissionSummary = BuildPermissionSummary(catalogScope, input.Module),
            ApprovalToken = aiReportApprovalService.CreateToken(
                userId, CurrentTenant.Id, catalogScope, plan)
        };
    }

    public async Task<AcademicAiReportExportResponseDto> CreateAiExportAsync(AcademicAiReportExportRequestDto input)
    {
        var userId = CurrentUser.Id ?? throw new BusinessException("Academic:Reports:050003");
        await reportAgentPermissionService.EnsureCanExportAsync(input.Module);
        var catalogScope = await GetAiCatalogScopeAsync(
            userId, input.AcademicYearId, input.EducationBoardId, input.EducationMediumId);
        dynamicReportGuard.EnsureRequestAllowed(input.Prompt, input.Module);
        var requestedPlan = input.Plan ?? throw new BusinessException("Academic:Reports:AiPlanRequired");
        var plan = await ResolveAiPlanAsync(requestedPlan, input.Module, catalogScope, input.Prompt);
        aiReportApprovalService.ValidateToken(
            input.ApprovalToken, userId, CurrentTenant.Id, catalogScope, plan);
        var export = await CreateDynamicExportJobAsync(plan, catalogScope, userId);

        return new AcademicAiReportExportResponseDto
        {
            JobId = export.JobId,
            Status = export.Status,
            ReportType = plan.Title,
            Summary = plan.Summary,
            Plan = plan
        };
    }

    private async Task<AcademicReportExportCreateResponseDto> CreateDynamicExportJobAsync(
        AcademicDynamicReportSpecDto plan,
        AcademicAiReportCatalogScope scope,
        Guid userId)
    {
        var jobId = guidGenerator.Create();
        var storedRequest = new AcademicDynamicStoredReportRequest
        {
            Module = plan.Module,
            AcademicYearId = scope.AcademicYearId,
            EducationBoardId = scope.EducationBoardId,
            EducationMediumId = scope.EducationMediumId,
            Plan = plan
        };
        var job = new ShikshaMitra.HR.Reports.HrReportExportJob(
            jobId,
            CurrentTenant.Id,
            userId,
            plan.Title,
            plan.Format,
            JsonSerializer.Serialize(plan.Filters),
            JsonSerializer.Serialize(storedRequest),
            DateTime.UtcNow.AddDays(7));
        await exportJobRepository.InsertAsync(job, autoSave: true);
        await backgroundJobManager.EnqueueAsync(new AcademicReportExportJobArgs
        {
            JobId = jobId,
            TenantId = CurrentTenant.Id,
            UserId = userId,
            DynamicSpec = plan,
            AcademicYearId = scope.AcademicYearId,
            EducationBoardId = scope.EducationBoardId,
            EducationMediumId = scope.EducationMediumId,
            AllowedClassIds = scope.ClassIds?.ToList(),
            AllowedDivisionIds = scope.DivisionIds?.ToList()
        });
        return new AcademicReportExportCreateResponseDto { JobId = jobId, Status = "Queued" };
    }

    private async Task<AcademicDynamicReportSpecDto> ResolveAiPlanAsync(
        AcademicDynamicReportSpecDto requestedPlan,
        string module,
        AcademicAiReportCatalogScope scope,
        string prompt = "Approved AI report plan")
    {
        var snapshot = await dynamicReportCatalog.BuildSnapshotAsync(scope);
        var plan = AcademicDynamicReportFilterResolver.Resolve(prompt, requestedPlan, snapshot);
        plan = AcademicAiReportPlanNormalizer.Normalize(
            $"{prompt}\n{plan.InterpretedRequest}", module, plan,
            dynamicReportCatalog.GetDatasets(module));
        return dynamicReportGuard.ValidatePlan(plan, module, snapshot);
    }

    private string BuildPermissionSummary(AcademicAiReportCatalogScope scope, string module)
    {
        var classScope = scope.ClassIds is { Count: > 0 }
            ? $"{scope.ClassIds.Count} accessible class{(scope.ClassIds.Count == 1 ? string.Empty : "es")}"
            : "all classes permitted in the selected medium";
        var divisionScope = scope.DivisionIds is { Count: > 0 }
            ? $"{scope.DivisionIds.Count} accessible division{(scope.DivisionIds.Count == 1 ? string.Empty : "s")}"
            : "permitted divisions";
        var definition = reportAgentPermissionService.GetAvailableModule(module);
        return $"{definition.Label} Reports permission verified · {classScope} · {divisionScope}";
    }

    private async Task<AcademicAiReportCatalogScope> GetAiCatalogScopeAsync(
        Guid userId,
        Guid? academicYearId,
        Guid? educationBoardId,
        Guid? educationMediumId)
    {
        if (!academicYearId.HasValue || !educationBoardId.HasValue || !educationMediumId.HasValue)
        {
            throw new BusinessException("Academic:Reports:AiAcademicContextRequired");
        }

        var board = await educationBoardRepository.FindAsync(educationBoardId.Value);
        var medium = await educationMediumRepository.FindAsync(educationMediumId.Value);
        if (board == null || medium == null ||
            board.AcademicYearId != academicYearId.Value ||
            medium.EducationBoardId != educationBoardId.Value)
        {
            throw new BusinessException("Academic:Reports:AiAcademicContextInvalid");
        }

        var hasClassTeacherAssignment = await classTeacherAccessService.HasAssignmentAsync(userId);
        var scope = await userScopeService.GetUserScopeAsync(userId);
        var restrictToClassTeacherDivisions = ShouldApplyClassTeacherRestriction(
            hasClassTeacherAssignment, scope);
        var hasSelectedMediumAccess = scope.CanAccessMediumStage(
            null, medium.Id, board.Id, board.AcademicYearId);
        if (hasSelectedMediumAccess && !restrictToClassTeacherDivisions)
        {
            return new AcademicAiReportCatalogScope(
                academicYearId.Value, board.Id, medium.Id);
        }

        var standardQuery = await standardRepository.GetQueryableAsync();
        var selectedStandards = await AsyncExecuter.ToListAsync(standardQuery.Where(standard =>
            standard.EducationMediumStage.EducationMediumId == medium.Id));
        var selectedStandardIds = selectedStandards.Select(standard => standard.Id).ToHashSet();

        var scopedDivisions = scope.DivisionIds.Count == 0
            ? []
            : await divisionRepository.GetListAsync(division => scope.DivisionIds.Contains(division.Id));
        var allowedDivisionIds = scopedDivisions
            .Where(division => selectedStandardIds.Contains(division.StandardId))
            .Select(division => division.Id)
            .Distinct()
            .ToList();
        var allowedClassIds = scopedDivisions
            .Select(division => division.StandardId)
            .Concat(restrictToClassTeacherDivisions ? [] : scope.StandardIds)
            .Concat(restrictToClassTeacherDivisions ? [] : scope.WildcardDivisionStandardIds)
            .Where(selectedStandardIds.Contains)
            .Distinct()
            .ToList();

        if (!restrictToClassTeacherDivisions)
        {
            var allowedStageIds = scope.MediumStageIds
                .Concat(scope.WildcardStandardMediumStageIds)
                .ToHashSet();
            allowedClassIds.AddRange(selectedStandards
                .Where(standard => allowedStageIds.Contains(standard.EducationMediumStageId))
                .Select(standard => standard.Id)
                .Where(id => !allowedClassIds.Contains(id)));
        }

        if (allowedClassIds.Count == 0)
        {
            throw new BusinessException(UserScopeErrorCodes.AccessDenied);
        }

        return new AcademicAiReportCatalogScope(
            academicYearId.Value, board.Id, medium.Id, allowedClassIds, allowedDivisionIds);
    }

    private async Task<AcademicReportExportCreateResponseDto> CreateExportJobAsync(
        AcademicReportRequestDto input,
        Guid userId)
    {
        var tenantId = CurrentTenant.Id;
        input = reportGenerator.NormalizeAndValidateRequest(input, exportAllRows: true);
        await ApplyClassTeacherScopeAsync(input, userId);
        var jobId = guidGenerator.Create();
        var expiresAt = DateTime.UtcNow.AddDays(7);

        var filterJson = JsonSerializer.Serialize(input.Filters);
        var requestJson = JsonSerializer.Serialize(input);

        var job = new ShikshaMitra.HR.Reports.HrReportExportJob(
            jobId,
            tenantId,
            userId,
            input.ReportType,
            input.Format,
            filterJson,
            requestJson,
            expiresAt
        );

        await exportJobRepository.InsertAsync(job, autoSave: true);

        await backgroundJobManager.EnqueueAsync(new AcademicReportExportJobArgs
        {
            JobId = jobId,
            TenantId = tenantId,
            UserId = userId,
            Request = input
        });

        return new AcademicReportExportCreateResponseDto
        {
            JobId = jobId,
            Status = "Queued"
        };
    }

    public async Task<List<AcademicReportExportJobDto>> GetExportsAsync()
    {
        var userId = CurrentUser.Id ?? throw new BusinessException("Academic:Reports:050003");

        var jobs = await exportJobRepository.GetListAsync(x => x.RequestedByUserId == userId);

        return jobs
            .OrderByDescending(x => x.CreationTime)
            .Take(100)
            .Select(job => MapJob(job, includeRequestJson: false))
            .ToList();
    }

    public async Task<AcademicReportExportJobDto> GetExportAsync(Guid jobId)
    {
        var job = await GetAuthorizedJobAsync(jobId);
        return MapJob(job, includeRequestJson: true);
    }

    public async Task<AcademicReportDownloadDto> DownloadExportAsync(Guid jobId)
    {
        var job = await GetAuthorizedJobAsync(jobId);

        if (job.Status != HrReportExportStatus.Completed || string.IsNullOrEmpty(job.BlobName))
        {
            throw new BusinessException("Academic:Reports:050001").WithData("Status", job.Status); // Not Ready
        }

        if (DateTime.UtcNow > job.ExpiresAt)
        {
            throw new BusinessException("Academic:Reports:050002"); // Expired
        }

        var bytes = await blobContainer.GetAllBytesOrNullAsync(job.BlobName);
        if (bytes == null)
        {
            throw new BusinessException("Academic:Reports:050004"); // File not found in blob
        }

        var (contentType, extension) = GetDownloadFormat(job.Format);
        var fileName = BuildDownloadFileName(job.ReportType, job.CreationTime, extension);

        return new AcademicReportDownloadDto
        {
            Bytes = bytes,
            ContentType = contentType,
            FileName = fileName
        };
    }

    private static (string ContentType, string Extension) GetDownloadFormat(string? format) =>
        format?.ToLowerInvariant() switch
        {
            AcademicReportFormat.Pdf => ("application/pdf", "pdf"),
            AcademicReportFormat.Csv => ("text/csv; charset=utf-8", "csv"),
            _ => ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx")
        };

    private async Task<ShikshaMitra.HR.Reports.HrReportExportJob> GetAuthorizedJobAsync(Guid jobId)
    {
        var userId = CurrentUser.Id ?? throw new BusinessException("Academic:Reports:050003");
        var job = await exportJobRepository.GetAsync(jobId);

        if (job.RequestedByUserId != userId)
        {
            throw new UnauthorizedAccessException("You are not authorized to access this export job.");
        }

        return job;
    }

    private async Task ApplyClassTeacherScopeAsync(AcademicReportRequestDto input, Guid userId)
    {
        var hasClassTeacherAssignment = await classTeacherAccessService.HasAssignmentAsync(userId);
        if (!hasClassTeacherAssignment)
        {
            return;
        }

        var scope = await userScopeService.GetUserScopeAsync(userId);
        if (!ShouldApplyClassTeacherRestriction(hasClassTeacherAssignment, scope))
        {
            return;
        }

        var allowedDivisionIds = scope.DivisionIds.Distinct().ToList();
        if (allowedDivisionIds.Count == 0)
        {
            throw new BusinessException(UserScopeErrorCodes.AccessDenied);
        }

        var requestedDivisionIds = input.Filters.DivisionIds;
        var visibleDivisionIds = requestedDivisionIds is { Count: > 0 }
            ? requestedDivisionIds.Where(allowedDivisionIds.Contains).Distinct().ToList()
            : allowedDivisionIds;
        if (visibleDivisionIds.Count == 0)
        {
            throw new BusinessException(UserScopeErrorCodes.AccessDenied);
        }

        var visibleDivisions = await divisionRepository.GetListAsync(
            division => visibleDivisionIds.Contains(division.Id));
        var allowedClassIds = visibleDivisions.Select(division => division.StandardId).Distinct().ToList();
        if (allowedClassIds.Count == 0)
        {
            throw new BusinessException(UserScopeErrorCodes.AccessDenied);
        }

        var requestedClassIds = input.Filters.ClassIds;
        var visibleClassIds = requestedClassIds is { Count: > 0 }
            ? requestedClassIds.Where(allowedClassIds.Contains).Distinct().ToList()
            : allowedClassIds;
        if (visibleClassIds.Count == 0)
        {
            throw new BusinessException(UserScopeErrorCodes.AccessDenied);
        }

        input.Filters.DivisionIds = visibleDivisionIds;
        input.Filters.ClassIds = visibleClassIds;
    }

    internal static bool ShouldApplyClassTeacherRestriction(
        bool hasClassTeacherAssignment,
        UserScopeContext scope) =>
        hasClassTeacherAssignment && !scope.HasTenantWideScope();

    private static AcademicReportExportJobDto MapJob(ShikshaMitra.HR.Reports.HrReportExportJob job, bool includeRequestJson = true)
    {
        var format = job.Format ?? "excel";
        var (_, extension) = GetDownloadFormat(format);
        var dynamicRequest = TryReadDynamicRequest(job.RequestJson);

        return new AcademicReportExportJobDto
        {
            Id = job.Id,
            ReportType = job.ReportType,
            Format = job.Format ?? "N/A",
            Status = job.Status,
            FileName = job.Status == HrReportExportStatus.Completed
                       ? BuildDownloadFileName(job.ReportType, job.CreationTime, extension)
                       : null,
            ErrorMessage = job.ErrorMessage,
            CreationTime = job.CreationTime,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            ExpiresAt = job.ExpiresAt,
            RequestJson = includeRequestJson ? job.RequestJson : null,
            IsDynamic = dynamicRequest != null,
            Module = dynamicRequest?.Module
        };
    }

    internal static AcademicDynamicStoredReportRequest? TryReadDynamicRequest(string? requestJson)
    {
        if (string.IsNullOrWhiteSpace(requestJson))
        {
            return null;
        }
        try
        {
            using var document = JsonDocument.Parse(requestJson);
            var kind = document.RootElement
                .EnumerateObject()
                .FirstOrDefault(property => property.Name.Equals("kind", StringComparison.OrdinalIgnoreCase));
            if (kind.Value.ValueKind != JsonValueKind.String ||
                !string.Equals(kind.Value.GetString(), "dynamic", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return JsonSerializer.Deserialize<AcademicDynamicStoredReportRequest>(
                requestJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string BuildDownloadFileName(string reportType, DateTime creationTime, string extension)
    {
        var safeName = new string((reportType ?? "Academic_Report")
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray());
        safeName = string.Join("_", safeName.Split('_', StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "Academic_Report";
        }

        return $"{safeName}_{creationTime:yyyyMMddHHmmss}.{extension}";
    }
}

public class AcademicReportExportJobArgs
{
    public Guid JobId { get; set; }
    public Guid? TenantId { get; set; }
    public Guid UserId { get; set; }
    public AcademicReportRequestDto? Request { get; set; }
    public AcademicDynamicReportSpecDto? DynamicSpec { get; set; }
    public Guid AcademicYearId { get; set; }
    public Guid EducationBoardId { get; set; }
    public Guid EducationMediumId { get; set; }
    public List<Guid>? AllowedClassIds { get; set; }
    public List<Guid>? AllowedDivisionIds { get; set; }
}

public class AcademicDynamicStoredReportRequest
{
    public string Kind { get; set; } = "dynamic";
    public string Module { get; set; } = AcademicReportModules.Curriculum;
    public Guid AcademicYearId { get; set; }
    public Guid EducationBoardId { get; set; }
    public Guid EducationMediumId { get; set; }
    public AcademicDynamicReportSpecDto Plan { get; set; } = new();
}
