using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Common.Files;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Threading;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.BackgroundWorkers;

/// <summary>
/// Removes expired, unattached concession evidence in bounded tenant batches.
/// Attached evidence is retained with the request and is never selected.
/// </summary>
public class ConcessionDocumentUploadCleanupWorker : AsyncPeriodicBackgroundWorkerBase
{
	private const int BatchSizePerTenant = 100;
	private const string FileUrlPrefix = "/api/files/";

	public ConcessionDocumentUploadCleanupWorker(
		AbpAsyncTimer timer,
		IServiceScopeFactory serviceScopeFactory)
		: base(timer, serviceScopeFactory)
	{
		Timer.Period = 60 * 60 * 1000;
	}

	protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
	{
		var logger = workerContext.ServiceProvider
			.GetRequiredService<ILogger<ConcessionDocumentUploadCleanupWorker>>();
		var tenantRepository = workerContext.ServiceProvider.GetRequiredService<ITenantRepository>();
		var currentTenant = workerContext.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var unitOfWorkManager = workerContext.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var clock = workerContext.ServiceProvider.GetRequiredService<IClock>();

		try
		{
			var tenants = await tenantRepository.GetListAsync();
			var tenantIds = new List<Guid?> { null };
			tenantIds.AddRange(tenants.Select(x => (Guid?)x.Id));
			var removed = 0;

			foreach (var tenantId in tenantIds)
			{
				using (currentTenant.Change(tenantId))
				using (var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
				{
					var uploadRepository = workerContext.ServiceProvider
						.GetRequiredService<IRepository<ConcessionDocumentUpload, Guid>>();
					var asyncExecuter = workerContext.ServiceProvider.GetRequiredService<IAsyncQueryableExecuter>();
					var fileStorage = workerContext.ServiceProvider.GetRequiredService<IFileStorageAppService>();
					var query = await uploadRepository.GetQueryableAsync();
					var expired = await asyncExecuter.ToListAsync(query
						.Where(x => x.ConcessionRequestId == null && x.ExpiresOn <= clock.Now)
						.OrderBy(x => x.ExpiresOn)
						.Take(BatchSizePerTenant));

					foreach (var upload in expired)
					{
						if (!upload.Url.StartsWith(FileUrlPrefix, StringComparison.Ordinal) ||
							upload.Url.Length == FileUrlPrefix.Length)
						{
							logger.LogWarning(
								"Concession staged upload {UploadId} has an invalid storage URL and was retained for investigation.",
								upload.Id);
							continue;
						}

						try
						{
							var storedFileName = Uri.UnescapeDataString(upload.Url[FileUrlPrefix.Length..]);
							await fileStorage.DeleteAsync(storedFileName);
							await uploadRepository.DeleteAsync(upload, autoSave: false);
							removed++;
						}
						catch (Exception ex)
						{
							logger.LogError(
								ex,
								"Failed to remove expired concession staged upload {UploadId}.",
								upload.Id);
						}
					}

					await uow.CompleteAsync();
				}
			}

			if (removed > 0)
			{
				logger.LogInformation(
					"Concession document cleanup removed {Count} expired unattached upload(s).",
					removed);
			}
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Concession document cleanup failed.");
		}
	}
}
