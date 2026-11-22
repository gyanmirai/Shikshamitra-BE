using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Reports;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Academic.Reports;

public partial class AcademicReportAppService
{
    private const string ExpiredExport = "Expired";
    private const string MissingExportFile = "Unavailable";

    public virtual async Task<AcademicReportRecoveryDto> GetExportRecoveryAsync(Guid jobId)
    {
        var job = await GetOwnedAcademicExportAsync(jobId);
        var status = await GetRecoveryStatusAsync(job);
        var canRegenerate = CanStartRecovery(job, status) &&
            await AuthorizationService.IsGrantedAsync(RegenerationPermission(job));
        return new(job.Id, job.ReportType, job.Format, status, job.ExpiresAt, canRegenerate);
    }

    [UnitOfWork(true)]
    public virtual async Task<AcademicReportExportCreateResponseDto> RegenerateExportAsync(Guid jobId)
    {
        var job = await GetOwnedAcademicExportAsync(jobId);
        if (!await AuthorizationService.IsGrantedAsync(RegenerationPermission(job)))
            throw new AbpAuthorizationException();
        var status = await GetRecoveryStatusAsync(job);
        if (!CanStartRecovery(job, status))
            return new() { JobId = job.Id, Status = status };

        var args = await BuildRecoveryArgsAsync(job);
        args.GenerationId = guidGenerator.Create();
        AcademicReportExportStateService.QueueRegeneration(job, args.GenerationId.Value, DateTime.UtcNow.AddDays(7));
        // Persist the new generation fence with the concurrency stamp before enqueueing.
        await exportJobRepository.UpdateAsync(job, autoSave: true);
        await backgroundJobManager.EnqueueAsync(args);
        return new() { JobId = job.Id, Status = HrReportExportStatus.Queued };
    }

    private async Task<HrReportExportJob> GetOwnedAcademicExportAsync(Guid jobId)
    {
        if (CurrentTenant.Id is not Guid school || CurrentUser.Id is not Guid requester || CurrentUser.TenantId != school ||
            await exportJobRepository.FindAsync(jobId, includeDetails: false) is not { } job ||
            job.IsDeleted || job.TenantId != school || job.RequestedByUserId != requester ||
            AcademicReportExportStateService.ReadRunId(job) == null)
            throw new AbpAuthorizationException();
        return job;
    }

    private async Task<string> GetRecoveryStatusAsync(HrReportExportJob job)
    {
        if (job.Status != HrReportExportStatus.Completed) return job.Status;
        if (job.ExpiresAt <= DateTime.UtcNow) return ExpiredExport;
        return !string.IsNullOrWhiteSpace(job.BlobName) && await blobContainer.ExistsAsync(job.BlobName)
            ? HrReportExportStatus.Completed : MissingExportFile;
    }

    private static bool CanStartRecovery(HrReportExportJob job, string status) =>
        status is ExpiredExport or MissingExportFile ||
        (status == HrReportExportStatus.Failed && AcademicReportExportStateService.ReadGenerationId(job).HasValue);

    private string RegenerationPermission(HrReportExportJob job) =>
        TryReadDynamicRequest(job.RequestJson) is { } dynamicRequest
            ? reportAgentPermissionService.GetAvailableModule(dynamicRequest.Module).ExportPermission
            : ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_Reports.Add;

    private async Task<AcademicReportExportJobArgs> BuildRecoveryArgsAsync(HrReportExportJob job)
    {
        var args = new AcademicReportExportJobArgs { JobId = job.Id, TenantId = job.TenantId, UserId = job.RequestedByUserId };
        if (TryReadDynamicRequest(job.RequestJson) is { } saved)
        {
            var allowed = await GetAiCatalogScopeAsync(args.UserId, saved.AcademicYearId, saved.EducationBoardId, saved.EducationMediumId);
            var scope = allowed with
            {
                ClassIds = IntersectExportScope(saved.AllowedClassIds, allowed.ClassIds),
                DivisionIds = IntersectExportScope(saved.AllowedDivisionIds, allowed.DivisionIds)
            };
            var snapshot = await dynamicReportCatalog.BuildSnapshotAsync(scope, saved.Module);
            args.DynamicSpec = dynamicReportGuard.ValidatePlan(saved.Plan, saved.Module, snapshot);
            args.AcademicYearId = scope.AcademicYearId;
            args.EducationBoardId = scope.EducationBoardId;
            args.EducationMediumId = scope.EducationMediumId;
            args.AllowedClassIds = scope.ClassIds?.ToList();
            args.AllowedDivisionIds = scope.DivisionIds?.ToList();
            return args;
        }

        AcademicReportRequestDto? request;
        try { request = JsonSerializer.Deserialize<AcademicReportRequestDto>(job.RequestJson); }
        catch (JsonException) { throw new BusinessException("Academic:Reports:RequestRequired"); }
        if (request == null || !string.Equals(request.ReportType, job.ReportType, StringComparison.OrdinalIgnoreCase))
            throw new BusinessException("Academic:Reports:RequestRequired");
        args.Request = reportGenerator.NormalizeAndValidateRequest(request, exportAllRows: true);
        await ApplyClassTeacherScopeAsync(args.Request, args.UserId);
        return args;
    }

    internal static IReadOnlyCollection<Guid>? IntersectExportScope(IReadOnlyCollection<Guid>? saved, IReadOnlyCollection<Guid>? current)
    {
        var allowed = saved == null ? current : current == null ? saved : saved.Intersect(current).ToArray();
        // An empty list must not reach a generator where it denotes an unfiltered request.
        if (allowed?.Count == 0) throw new BusinessException(UserScopeErrorCodes.AccessDenied);
        return allowed;
    }
}
