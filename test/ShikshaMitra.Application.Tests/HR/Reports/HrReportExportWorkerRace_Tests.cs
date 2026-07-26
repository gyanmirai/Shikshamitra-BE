using System;
using System.IO;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.HR.Reports;

public class HrReportExportWorkerRace_Tests
	: ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly Guid TestTenantId =
		Guid.Parse("11111111-1111-1111-1111-111111111111");

	private readonly IRepository<HrReportExportJob, Guid> _jobRepository;
	private readonly HrReportExportJobStateService _jobStateService;
	private readonly HrReportExportBackgroundJob _backgroundJob;
	private readonly IBlobContainer _blobContainer;
	private readonly ICurrentTenant _currentTenant;

	public HrReportExportWorkerRace_Tests()
	{
		_jobRepository = GetRequiredService<IRepository<HrReportExportJob, Guid>>();
		_jobStateService = GetRequiredService<HrReportExportJobStateService>();
		_backgroundJob = GetRequiredService<HrReportExportBackgroundJob>();
		_blobContainer = GetRequiredService<IBlobContainer>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
	}

	[Fact]
	public async Task LoadGenerationContextAsync_Should_Reject_Cancellation_After_MarkRunning()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var request = CreateRequest("cancel-between-mark-and-load");
			var generationId = Guid.NewGuid();
			var job = await CreateQueuedJobAsync(request, generationId);
			var args = CreateArgs(job, request, generationId);

			(await _jobStateService.TryMarkRunningAsync(args)).ShouldBeTrue();
			await WithUnitOfWorkAsync(async () =>
			{
				var current = await _jobRepository.GetAsync(job.Id);
				current.MarkCancelled(DateTime.UtcNow);
				await _jobRepository.UpdateAsync(current, autoSave: true);
			});

			(await _jobStateService.LoadGenerationContextAsync(args)).ShouldBeNull();
		}
	}

	[Fact]
	public async Task LoadGenerationContextAsync_Should_Reject_Stale_Generation_After_Cancel_And_Retry()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var request = CreateRequest("retry-between-mark-and-load");
			var originalGenerationId = Guid.NewGuid();
			var job = await CreateQueuedJobAsync(request, originalGenerationId);
			var originalArgs = CreateArgs(job, request, originalGenerationId);

			(await _jobStateService.TryMarkRunningAsync(originalArgs)).ShouldBeTrue();

			var retriedGenerationId = Guid.NewGuid();
			await WithUnitOfWorkAsync(async () =>
			{
				var current = await _jobRepository.GetAsync(job.Id);
				current.MarkCancelled(DateTime.UtcNow);
				current.MarkQueued(DateTime.UtcNow.AddDays(7));
				current.UpdateRequest(
					current.FilterJson,
					HrReportStoredExportRequestSerializer.Serialize(
						request,
						CreateScopeSnapshot(),
						retriedGenerationId));
				await _jobRepository.UpdateAsync(current, autoSave: true);
			});

			var retriedArgs = CreateArgs(job, request, retriedGenerationId);
			(await _jobStateService.TryMarkRunningAsync(retriedArgs)).ShouldBeTrue();

			(await _jobStateService.LoadGenerationContextAsync(originalArgs)).ShouldBeNull();
			var currentContext = await _jobStateService.LoadGenerationContextAsync(retriedArgs);
			currentContext.ShouldNotBeNull();
			currentContext!.GenerationId.ShouldBe(retriedGenerationId);
		}
	}

	[Fact]
	public async Task Cleanup_Should_Preserve_Committed_Blob_And_Delete_An_Old_Generation_Blob()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var request = CreateRequest("completed-blob-cleanup");
			var currentGenerationId = Guid.NewGuid();
			var job = await CreateQueuedJobAsync(request, currentGenerationId);
			var args = CreateArgs(job, request, currentGenerationId);
			(await _jobStateService.TryMarkRunningAsync(args)).ShouldBeTrue();

			var committedBlobName = HrReportExportBackgroundJob.BuildBlobName(
				job.Id,
				currentGenerationId,
				job.TenantId,
				job.RequestedByUserId,
				HrReportFormat.Excel);
			await SaveBlobAsync(committedBlobName);
			(await _jobStateService.TryMarkCompletedAsync(
				job.Id,
				currentGenerationId,
				committedBlobName,
				rowCount: 1,
				fileSizeBytes: 3)).ShouldBeTrue();

			await _backgroundJob.DeleteBlobIfUncommittedBestEffortAsync(
				job.Id,
				currentGenerationId,
				committedBlobName);
			(await _blobContainer.ExistsAsync(committedBlobName)).ShouldBeTrue();

			var staleGenerationId = Guid.NewGuid();
			var staleBlobName = HrReportExportBackgroundJob.BuildBlobName(
				job.Id,
				staleGenerationId,
				job.TenantId,
				job.RequestedByUserId,
				HrReportFormat.Excel);
			staleBlobName.ShouldNotBe(committedBlobName);
			await SaveBlobAsync(staleBlobName);

			await _backgroundJob.DeleteBlobIfUncommittedBestEffortAsync(
				job.Id,
				staleGenerationId,
				staleBlobName);
			(await _blobContainer.ExistsAsync(staleBlobName)).ShouldBeFalse();
			(await _blobContainer.ExistsAsync(committedBlobName)).ShouldBeTrue();
		}
	}

	private async Task<HrReportExportJob> CreateQueuedJobAsync(
		HrReportRequestDto request,
		Guid generationId)
	{
		var job = new HrReportExportJob(
			Guid.NewGuid(),
			TestTenantId,
			Guid.NewGuid(),
			request.ReportType,
			HrReportFormat.Excel,
			"{}",
			HrReportStoredExportRequestSerializer.Serialize(
				request,
				CreateScopeSnapshot(),
				generationId),
			DateTime.UtcNow.AddDays(7));

		await WithUnitOfWorkAsync(() => _jobRepository.InsertAsync(job, autoSave: true));
		return job;
	}

	private static HrReportExportJobArgs CreateArgs(
		HrReportExportJob job,
		HrReportRequestDto request,
		Guid generationId) =>
		new()
		{
			JobId = job.Id,
			TenantId = job.TenantId,
			UserId = job.RequestedByUserId,
			Request = request,
			ScopeSnapshot = CreateScopeSnapshot(),
			GenerationId = generationId
		};

	private static HrReportRequestDto CreateRequest(string marker) =>
		new()
		{
			ReportType = $"Worker race {marker}",
			DataSource = HrReportDataSources.Employee,
			Format = HrReportFormat.Excel,
			Columns = ["employeeCode"],
			Filters = new HrReportFilterDto()
		};

	private static HrReportExportScopeSnapshot CreateScopeSnapshot() =>
		new()
		{
			TenantId = TestTenantId,
			HasTenantWideAccess = true
		};

	private async Task SaveBlobAsync(string blobName)
	{
		await using var stream = new MemoryStream([1, 2, 3]);
		await _blobContainer.SaveAsync(blobName, stream, overrideExisting: true);
	}
}
