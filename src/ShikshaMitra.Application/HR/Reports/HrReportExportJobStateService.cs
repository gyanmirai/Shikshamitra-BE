using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Reports;

public class HrReportExportJobStateService(
	IRepository<HrReportExportJob, Guid> exportJobRepository,
	HrReportExportScopeAuthorizationService scopeAuthorizationService,
	IClock clock,
	IUnitOfWorkManager unitOfWorkManager) : ITransientDependency
{
	internal async Task<HrReportGenerationContext?> LoadGenerationContextAsync(
		HrReportExportJobArgs args)
	{
		using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
		var job = await exportJobRepository.GetAsync(args.JobId);
		if (job.Status != HrReportExportStatus.Running ||
			!DoesGenerationMatch(job, args.GenerationId))
		{
			await uow.CompleteAsync();
			return null;
		}

		var request = HrReportStoredExportRequestSerializer.DeserializeRequest(job.RequestJson)
			?? args.Request;
		var scopeSnapshot = HrReportStoredExportRequestSerializer.DeserializeScope(job.RequestJson);
		var generationId = HrReportStoredExportRequestSerializer.DeserializeGenerationId(job.RequestJson);
		if (scopeSnapshot == null || !generationId.HasValue)
		{
			scopeSnapshot ??= await scopeAuthorizationService.CaptureAsync(
				job.RequestedByUserId,
				job.TenantId);
			generationId ??= args.GenerationId != Guid.Empty
				? args.GenerationId
				: Guid.NewGuid();
			job.UpdateRequest(
				job.FilterJson,
				HrReportStoredExportRequestSerializer.Serialize(
					request,
					scopeSnapshot,
					generationId.Value));
			await exportJobRepository.UpdateAsync(job, autoSave: false);
		}

		await uow.CompleteAsync();
		return new HrReportGenerationContext(
			request,
			scopeSnapshot,
			generationId.Value,
			job.TenantId,
			job.RequestedByUserId,
			job.Format);
	}

	internal async Task<bool> TryMarkRunningAsync(HrReportExportJobArgs args)
	{
		using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
		var snapshot = await exportJobRepository.GetAsync(args.JobId);
		if (snapshot.Status != HrReportExportStatus.Queued ||
			!DoesGenerationMatch(snapshot, args.GenerationId))
		{
			await uow.CompleteAsync();
			return false;
		}

		snapshot.MarkRunning(clock.Now);
		await exportJobRepository.UpdateAsync(snapshot, autoSave: false);
		await uow.CompleteAsync();
		return true;
	}

	internal async Task<bool> IsCurrentGenerationRunningAsync(Guid jobId, Guid generationId)
	{
		using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
		var snapshot = await exportJobRepository.GetAsync(jobId);
		await uow.CompleteAsync();
		return snapshot.Status == HrReportExportStatus.Running &&
			DoesGenerationMatch(snapshot, generationId);
	}

	internal async Task<bool> IsCompletedBlobAsync(
		Guid jobId,
		Guid generationId,
		string blobName)
	{
		using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
		var snapshot = await exportJobRepository.GetAsync(jobId);
		await uow.CompleteAsync();
		return snapshot.Status == HrReportExportStatus.Completed &&
			DoesGenerationMatch(snapshot, generationId) &&
			string.Equals(snapshot.BlobName, blobName, StringComparison.Ordinal);
	}

	internal async Task<bool> TryMarkCompletedAsync(
		Guid jobId,
		Guid generationId,
		string blobName,
		int rowCount,
		long fileSizeBytes)
	{
		using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
		var snapshot = await exportJobRepository.GetAsync(jobId);
		if (snapshot.Status != HrReportExportStatus.Running ||
			!DoesGenerationMatch(snapshot, generationId))
		{
			await uow.CompleteAsync();
			return false;
		}

		snapshot.MarkCompleted(blobName, clock.Now, rowCount, fileSizeBytes);
		await exportJobRepository.UpdateAsync(snapshot, autoSave: false);
		await uow.CompleteAsync();
		return true;
	}

	internal async Task<bool> TryMarkFailedAsync(
		Guid jobId,
		Guid generationId,
		string errorMessage)
	{
		using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
		var snapshot = await exportJobRepository.GetAsync(jobId);
		if (snapshot.Status != HrReportExportStatus.Running ||
			!DoesGenerationMatch(snapshot, generationId))
		{
			await uow.CompleteAsync();
			return false;
		}

		snapshot.MarkFailed(errorMessage, clock.Now);
		await exportJobRepository.UpdateAsync(snapshot, autoSave: false);
		await uow.CompleteAsync();
		return true;
	}

	private static bool DoesGenerationMatch(HrReportExportJob job, Guid generationId)
	{
		var storedGenerationId = HrReportStoredExportRequestSerializer.DeserializeGenerationId(job.RequestJson);
		return storedGenerationId.HasValue
			? storedGenerationId.Value == generationId
			: generationId == Guid.Empty;
	}
}

internal sealed record HrReportGenerationContext(
	HrReportRequestDto Request,
	HrReportExportScopeSnapshot ScopeSnapshot,
	Guid GenerationId,
	Guid? TenantId,
	Guid UserId,
	string Format);
