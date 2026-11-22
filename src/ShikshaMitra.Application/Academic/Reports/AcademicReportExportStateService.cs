using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.HR.Reports;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Academic.Reports;

// Read each checkpoint in its own UOW: an export's data-reading context may hold a stale tracked job.
public class AcademicReportExportStateService(
    IRepository<HrReportExportJob, Guid> repository,
    IUnitOfWorkManager unitOfWorkManager,
    ICurrentTenant tenant,
    IRepository<NotificationRule, Guid> rules,
    IAsyncQueryableExecuter queries,
    INotificationIntentWriter notifications) : ITransientDependency
{
    private const string RunProperty = "AcademicExportRunId";
    private const string GenerationProperty = "AcademicExportGenerationId";
    private const string LeaseProperty = "AcademicExportLeaseUntilUtc";
    internal static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);

    public Task<Guid?> TryStartAsync(Guid jobId, Guid? generationId = null) => TryStartAsync(jobId, DateTime.UtcNow, generationId);

    internal async Task<Guid?> TryStartAsync(Guid jobId, DateTime now, Guid? generationId = null)
    {
        using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        var job = await repository.GetAsync(jobId);
        if (ReadGenerationId(job) != generationId)
        {
            await uow.CompleteAsync();
            return null;
        }
        if (job.Status == HrReportExportStatus.Running && LeaseUntil(job) > now)
            throw new InvalidOperationException("Academic report export already has an active worker lease; retry this delivery later.");
        if (job.Status != HrReportExportStatus.Queued && job.Status != HrReportExportStatus.Running)
        {
            await uow.CompleteAsync();
            return null;
        }
        var runId = Guid.NewGuid();
        job.SetProperty(RunProperty, runId.ToString("N"));
        job.SetProperty(LeaseProperty, (now + LeaseDuration).ToString("O", CultureInfo.InvariantCulture));
        job.MarkRunning(now);
        await repository.UpdateAsync(job);
        await uow.CompleteAsync();
        return runId;
    }

    public async Task<bool> RenewLeaseAsync(Guid jobId, Guid runId)
    {
        using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        var job = await repository.GetAsync(jobId);
        if (!IsCurrentRun(job, runId))
        {
            await uow.CompleteAsync();
            return false;
        }
        job.SetProperty(LeaseProperty, (DateTime.UtcNow + LeaseDuration).ToString("O", CultureInfo.InvariantCulture));
        await repository.UpdateAsync(job);
        await uow.CompleteAsync();
        return true;
    }

    public async Task TryQueueInterruptedAsync(Guid jobId, Guid runId)
    {
        using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        var job = await repository.GetAsync(jobId);
        if (IsCurrentRun(job, runId))
        {
            job.MarkQueued(job.ExpiresAt);
            await repository.UpdateAsync(job);
        }
        await uow.CompleteAsync();
    }

    private static DateTime LeaseUntil(HrReportExportJob job) =>
        DateTime.TryParse(job.GetProperty<string>(LeaseProperty), CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var lease) ? lease : (job.StartedAt ?? DateTime.MinValue) + LeaseDuration;

    public async Task<bool> IsRunningAsync(Guid jobId, Guid runId)
    {
        using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
        var job = await repository.GetAsync(jobId);
        var running = IsCurrentRun(job, runId);
        await uow.CompleteAsync();
        return running;
    }

    public async Task<bool> TryCompleteAsync(Guid jobId, Guid runId, string blobName, int rowCount, long fileSize)
    {
        using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        var job = await repository.GetAsync(jobId);
        if (!IsCurrentRun(job, runId))
        {
            await uow.CompleteAsync();
            return false;
        }
        job.MarkCompleted(blobName, DateTime.UtcNow, rowCount, fileSize);
        await repository.UpdateAsync(job);
        await CaptureTerminalNoticeAsync(job, runId);
        await uow.CompleteAsync();
        return true;
    }

    public async Task<bool> IsCompletedBlobAsync(Guid jobId, Guid runId, string blobName)
    {
        using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
        var job = await repository.GetAsync(jobId);
        var completed = job.Status == HrReportExportStatus.Completed && job.BlobName == blobName &&
            job.GetProperty<string>(RunProperty) == runId.ToString("N");
        await uow.CompleteAsync();
        return completed;
    }

    public async Task TryFailAsync(Guid jobId, Guid runId, string errorMessage)
    {
        using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        var job = await repository.GetAsync(jobId);
        if (IsCurrentRun(job, runId))
        {
            job.MarkFailed(errorMessage, DateTime.UtcNow);
            await repository.UpdateAsync(job);
            await CaptureTerminalNoticeAsync(job, runId);
        }
        await uow.CompleteAsync();
    }

    private async Task CaptureTerminalNoticeAsync(HrReportExportJob job, Guid runId)
    {
        if (tenant.Id is not Guid school || school == Guid.Empty || job.TenantId != school ||
            job.RequestedByUserId == Guid.Empty || job.CompletedAt is not { } completed ||
            job.ExpiresAt <= completed || AcademicReportNotificationSource.TerminalEvent(job) is not { } eventKey)
            return;

        var enabled = (await rules.GetQueryableAsync()).Where(x =>
            x.TenantId == school && !x.IsDeleted && x.IsEnabled && x.EventKey == eventKey && x.AcademicYearId == null);
        if (!await queries.AnyAsync(enabled)) return;

        await notifications.WriteAsync(new(eventKey, AcademicReportNotificationSource.Type, job.Id, runId.ToString("N"),
            AcademicReportNotificationSource.Utc(completed), [new("Requester", UserId: job.RequestedByUserId)]),
            CancellationToken.None);
    }

    internal static void QueueRegeneration(HrReportExportJob job, Guid generationId, DateTime expiresAt)
    {
        job.SetProperty(GenerationProperty, generationId.ToString("N"));
        job.MarkQueued(expiresAt);
    }

    internal static Guid? ReadGenerationId(HrReportExportJob job) => ReadId(job, GenerationProperty);

    internal static Guid? ReadRunId(HrReportExportJob job) => ReadId(job, RunProperty);

    private static Guid? ReadId(HrReportExportJob job, string property) =>
        Guid.TryParseExact(job.GetProperty<string>(property), "N", out var id) && id != Guid.Empty ? id : null;

    private static bool IsCurrentRun(HrReportExportJob job, Guid runId) =>
        job.Status == HrReportExportStatus.Running && ReadRunId(job) == runId;
}
