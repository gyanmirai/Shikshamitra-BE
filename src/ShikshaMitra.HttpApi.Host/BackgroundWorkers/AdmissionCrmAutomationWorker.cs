using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace ShikshaMitra.BackgroundWorkers;

public class AdmissionCrmAutomationWorker : AsyncPeriodicBackgroundWorkerBase
{
	public AdmissionCrmAutomationWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
		: base(timer, serviceScopeFactory)
	{
		Timer.Period = 60 * 60 * 1000;
	}

	protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
	{
		var logger = workerContext.ServiceProvider.GetRequiredService<ILogger<AdmissionCrmAutomationWorker>>();

		try
		{
			var tenantRepository = workerContext.ServiceProvider.GetRequiredService<ITenantRepository>();
			var currentTenant = workerContext.ServiceProvider.GetRequiredService<ICurrentTenant>();
			var unitOfWorkManager = workerContext.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
			var cycleRepository = workerContext.ServiceProvider
				.GetRequiredService<IRepository<AdmissionCrmCycleConfiguration, Guid>>();
			var leadAppService = workerContext.ServiceProvider.GetRequiredService<IAdmissionCrmLeadAppService>();
			var lifecycleAutomationAppService = workerContext.ServiceProvider.GetRequiredService<IAdmissionCrmLifecycleAutomationAppService>();
			var governanceCoordinator = workerContext.ServiceProvider.GetRequiredService<AdmissionCrmGovernanceAutomationCoordinator>();

			var tenants = await tenantRepository.GetListAsync();
			var processedCycleCount = 0;
			var markedStaleCount = 0;
			var archivedCount = 0;
			var createdSlaTaskCount = 0;
			var lifecycleTaskCount = 0;
			var lifecycleMessageCount = 0;
			var expiredOfferCount = 0;
			var offerExpiryFailureCount = 0;
			var generatedReportCount = 0;
			var failedReportCount = 0;

			foreach (var tenant in tenants)
			{
				using (currentTenant.Change(tenant.Id))
				{
					var activeCycles = await GetActiveCyclesAsync(cycleRepository, unitOfWorkManager, tenant.Id);
					foreach (var cycle in activeCycles)
					{
						try
						{
							using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
							var staleResult = await leadAppService.RunStaleArchiveRulesFromAutomationAsync(cycle.AcademicYearId);
							var slaResult = await leadAppService.RunSlaReminderRulesFromAutomationAsync(cycle.AcademicYearId);
							var lifecycleResult = await lifecycleAutomationAppService.RunFromAutomationAsync(cycle.AcademicYearId);
							var reportResult = await governanceCoordinator.RunDueReportSchedulesAsync(cycle.AcademicYearId);
							await uow.CompleteAsync();

							processedCycleCount++;
							markedStaleCount += staleResult.MarkedStaleCount;
							archivedCount += staleResult.ArchivedCount;
							createdSlaTaskCount += slaResult.CreatedTaskCount;
							lifecycleTaskCount += lifecycleResult.CreatedTaskCount;
							lifecycleMessageCount += lifecycleResult.SentMessageCount;
							expiredOfferCount += lifecycleResult.ExpiredOfferCount;
							offerExpiryFailureCount += lifecycleResult.OfferExpiryFailureCount;
							generatedReportCount += reportResult.GeneratedCount;
							failedReportCount += reportResult.FailedCount;
						}
						catch (Exception ex)
						{
							logger.LogError(
								ex,
								"AdmissionCrmAutomationWorker: failed for tenant {TenantId} academic year {AcademicYearId}.",
								tenant.Id,
								cycle.AcademicYearId);
						}
					}
				}
			}

			logger.LogInformation(
				"AdmissionCrmAutomationWorker: processed {CycleCount} active cycle(s), marked {MarkedStaleCount} stale lead(s), archived {ArchivedCount} lead(s), created {CreatedSlaTaskCount} SLA task(s), {LifecycleTaskCount} lifecycle task(s), {LifecycleMessageCount} lifecycle message(s), expired {ExpiredOfferCount} offer(s), recorded {OfferExpiryFailureCount} expiry failure(s), generated {GeneratedReportCount} scheduled report(s), and recorded {FailedReportCount} report failure(s).",
				processedCycleCount,
				markedStaleCount,
				archivedCount,
				createdSlaTaskCount,
				lifecycleTaskCount,
				lifecycleMessageCount,
				expiredOfferCount,
				offerExpiryFailureCount,
				generatedReportCount,
				failedReportCount);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "AdmissionCrmAutomationWorker: failed to process CRM automation.");
		}
	}

	private static async Task<IReadOnlyList<AdmissionCrmCycleConfiguration>> GetActiveCyclesAsync(
		IRepository<AdmissionCrmCycleConfiguration, Guid> cycleRepository,
		IUnitOfWorkManager unitOfWorkManager,
		Guid tenantId)
	{
		using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
		var cycles = await cycleRepository.GetListAsync(x => x.TenantId == tenantId && x.IsActive);
		await uow.CompleteAsync();
		return cycles;
	}
}
