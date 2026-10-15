using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ShikshaMitra.HR.Reports;
using Shouldly;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Academic.Reports;

public class AcademicReportExportLifecycle_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
    [Fact]
    public async Task Fresh_State_Checkpoints_Should_Observe_Cancellation_And_Reject_Stale_Runs()
    {
        using (GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid()))
        {
            var job = await SeedJobAsync();
            var state = GetRequiredService<AcademicReportExportStateService>();
            var firstRun = (await state.TryStartAsync(job.Id))!.Value;
            await Should.ThrowAsync<InvalidOperationException>(() => state.TryStartAsync(job.Id));
            await MutateJobAsync(job.Id, current => current.MarkCancelled(DateTime.UtcNow));
            (await state.IsRunningAsync(job.Id, firstRun)).ShouldBeFalse();
            (await state.TryCompleteAsync(job.Id, firstRun, "cancelled.xlsx", 10, 50)).ShouldBeFalse();
            await state.TryFailAsync(job.Id, firstRun, "Do not overwrite cancellation");
            (await ReadJobAsync(job.Id)).Status.ShouldBe(HrReportExportStatus.Cancelled);

            await MutateJobAsync(job.Id, current => current.MarkQueued(DateTime.UtcNow.AddDays(7)));
            var retry = (await state.TryStartAsync(job.Id))!.Value;
            retry.ShouldNotBe(firstRun);
            (await state.TryCompleteAsync(job.Id, firstRun, "stale.xlsx", 10, 50)).ShouldBeFalse();
            await state.TryFailAsync(job.Id, firstRun, "Stale failure");
            (await state.IsRunningAsync(job.Id, retry)).ShouldBeTrue();
            (await state.TryCompleteAsync(job.Id, retry, "successful.xlsx", 17, 1000)).ShouldBeTrue();
            (await state.IsCompletedBlobAsync(job.Id, retry, "successful.xlsx")).ShouldBeTrue();
            var completed = await ReadJobAsync(job.Id);
            completed.RowCount.ShouldBe(17);
            completed.FileSizeBytes.ShouldBe(1000);
        }
    }

    [Fact]
    public async Task Redelivery_Should_Recover_An_Expired_Run_And_Reject_Its_Stale_Owner()
    {
        using (GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid()))
        {
            var job = await SeedJobAsync();
            var state = GetRequiredService<AcademicReportExportStateService>();
            var interrupted = (await state.TryStartAsync(job.Id, DateTime.UtcNow.AddMinutes(-10)))!.Value;
            var recovered = (await state.TryStartAsync(job.Id))!.Value;
            recovered.ShouldNotBe(interrupted);
            (await state.RenewLeaseAsync(job.Id, interrupted)).ShouldBeFalse();
            (await state.TryCompleteAsync(job.Id, interrupted, "stale.xlsx", 1, 1)).ShouldBeFalse();
            await state.TryFailAsync(job.Id, interrupted, "Interrupted owner");
            await state.TryQueueInterruptedAsync(job.Id, interrupted);
            (await state.RenewLeaseAsync(job.Id, recovered)).ShouldBeTrue();
            await Should.ThrowAsync<InvalidOperationException>(() => state.TryStartAsync(job.Id));
            (await state.TryCompleteAsync(job.Id, recovered, "recovered.xlsx", 17, 1000)).ShouldBeTrue();
            (await ReadJobAsync(job.Id)).BlobName.ShouldBe("recovered.xlsx");
        }
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("failure")]
    [InlineData("shutdown")]
    public async Task Worker_Should_Clean_Partial_Output_And_Record_Final_State(string outcome)
    {
        using (GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid()))
        {
            var job = await SeedJobAsync();
            var blobs = Substitute.For<IBlobContainer>();
            string? outputPath = null;
            string? blobName = null;
            using var cancellation = new CancellationTokenSource();
            blobs.SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(async call =>
                {
                    blobName = call.ArgAt<string>(0);
                    var content = call.ArgAt<Stream>(1);
                    outputPath = ((FileStream)content).Name;
                    content.Length.ShouldBeGreaterThan(0);
                    if (outcome == "cancel") await MutateJobAsync(job.Id, current => current.MarkCancelled(DateTime.UtcNow));
                    else if (outcome == "shutdown")
                    {
                        cancellation.Cancel();
                        throw new OperationCanceledException(cancellation.Token);
                    }
                    else throw new IOException("Storage failed after receiving the output stream.");
                });
            var tokens = GetRequiredService<ICancellationTokenProvider>();
            using (tokens.Use(cancellation.Token))
            {
                var execution = CreateWorker(blobs).ExecuteAsync(new AcademicReportExportJobArgs
                {
                    JobId = job.Id, UserId = job.RequestedByUserId, TenantId = job.TenantId,
                    Request = new AcademicReportRequestDto { ReportType = AcademicReportTypes.ChapterDescriptionSummary }
                });
                if (outcome == "shutdown") await Should.ThrowAsync<OperationCanceledException>(() => execution);
                else await execution;
            }
            blobName.ShouldNotBeNull();
            await blobs.Received(1).DeleteAsync(blobName!, Arg.Any<CancellationToken>());
            outputPath.ShouldNotBeNull();
            File.Exists(outputPath).ShouldBeFalse();
            var current = await ReadJobAsync(job.Id);
            current.Status.ShouldBe(outcome switch
            {
                "cancel" => HrReportExportStatus.Cancelled,
                "shutdown" => HrReportExportStatus.Queued,
                _ => HrReportExportStatus.Failed
            });
            current.BlobName.ShouldBeNull();
        }
    }

    [Fact]
    public async Task Worker_Should_Save_Database_Blob_In_A_Fresh_Unit_Of_Work()
    {
        using (GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid()))
        {
            var job = await SeedJobAsync();
            await GetRequiredService<AcademicReportExportBackgroundJob>().ExecuteAsync(new AcademicReportExportJobArgs
            {
                JobId = job.Id, UserId = job.RequestedByUserId, TenantId = job.TenantId,
                Request = new AcademicReportRequestDto { ReportType = AcademicReportTypes.ChapterDescriptionSummary }
            });
            var current = await ReadJobAsync(job.Id);
            current.Status.ShouldBe(HrReportExportStatus.Completed, current.ErrorMessage);
            current.RowCount.ShouldBe(0);
            await WithUnitOfWorkAsync(async () =>
            {
                var bytes = await GetRequiredService<IBlobContainer>().GetAllBytesAsync(current.BlobName!);
                bytes.LongLength.ShouldBe(current.FileSizeBytes!.Value);
            });
        }
    }

    private AcademicReportExportBackgroundJob CreateWorker(IBlobContainer blobs) => new(
        GetRequiredService<AcademicReportGenerator>(), GetRequiredService<AcademicPdfService>(),
        GetRequiredService<AcademicDynamicReportQueryService>(), GetRequiredService<AcademicDynamicReportPdfService>(),
        GetRequiredService<TimetableReportsPdfService>(), GetRequiredService<AcademicExcelService>(),
        GetRequiredService<AcademicCsvService>(), GetRequiredService<AcademicReportExportStateService>(),
        GetRequiredService<AcademicReportIdentityService>(),
        blobs, GetRequiredService<ICurrentTenant>(), GetRequiredService<IUnitOfWorkManager>(),
        GetRequiredService<ICancellationTokenProvider>(), NullLogger<AcademicReportExportBackgroundJob>.Instance);

    private Task<HrReportExportJob> SeedJobAsync() => WithUnitOfWorkAsync(async () =>
    {
        var job = new HrReportExportJob(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id, Guid.NewGuid(),
            AcademicReportTypes.ChapterDescriptionSummary, AcademicReportFormat.Excel, "{}", "{}", DateTime.UtcNow.AddDays(7));
        return await GetRequiredService<IRepository<HrReportExportJob, Guid>>().InsertAsync(job, autoSave: true);
    });

    private Task<HrReportExportJob> ReadJobAsync(Guid id) => WithUnitOfWorkAsync(() =>
        GetRequiredService<IRepository<HrReportExportJob, Guid>>().GetAsync(id));

    private async Task MutateJobAsync(Guid id, Action<HrReportExportJob> mutation)
    {
        using var uow = GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
        var repository = GetRequiredService<IRepository<HrReportExportJob, Guid>>();
        var job = await repository.GetAsync(id);
        mutation(job);
        await repository.UpdateAsync(job, autoSave: true);
        await uow.CompleteAsync();
    }
}
