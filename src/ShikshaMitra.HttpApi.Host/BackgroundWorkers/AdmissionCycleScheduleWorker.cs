using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace ShikshaMitra.BackgroundWorkers;

public class AdmissionCycleScheduleWorker : AsyncPeriodicBackgroundWorkerBase
{
	public AdmissionCycleScheduleWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
		: base(timer, serviceScopeFactory)
	{
		Timer.Period = 60 * 1000;
	}

	protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
	{
		var logger = workerContext.ServiceProvider.GetRequiredService<ILogger<AdmissionCycleScheduleWorker>>();
		var cycles = workerContext.ServiceProvider.GetRequiredService<IRepository<AdmissionCycle, Guid>>();
		var histories = workerContext.ServiceProvider.GetRequiredService<IRepository<AdmissionCycleHistory, Guid>>();
		var academicYears = workerContext.ServiceProvider.GetRequiredService<IRepository<AcademicYear, Guid>>();
		var dataFilter = workerContext.ServiceProvider.GetRequiredService<IDataFilter>();
		var unitOfWorkManager = workerContext.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var now = DateTime.UtcNow;

		try
		{
			using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
			using (dataFilter.Disable<IMultiTenant>())
			{
				var due = await cycles.GetListAsync(x =>
					(x.Status == AdmissionCycleStatus.Draft && x.ScheduledPublishAt.HasValue && x.ScheduledPublishAt <= now) ||
					(x.Status == AdmissionCycleStatus.Published && x.ScheduledCloseAt.HasValue && x.ScheduledCloseAt <= now));
				var academicYearIds = due.Where(x => x.Status == AdmissionCycleStatus.Draft)
					.Select(x => x.AcademicYearId).Distinct().ToList();
				var availableAcademicYearIds = academicYearIds.Count == 0
					? []
					: (await academicYears.GetListAsync(x => academicYearIds.Contains(x.Id))).Select(x => x.Id).ToHashSet();
				var institutionDate = AdmissionCycleWindowEvaluator.GetInstitutionDate(now);

				foreach (var cycle in due.OrderBy(x => x.CreationTime))
				{
					var previous = cycle.Status;
					var action = AdmissionCycleHistoryAction.Published;
					var description = "Admission cycle published automatically by its schedule.";
					if (cycle.Status == AdmissionCycleStatus.Draft)
					{
						if (!availableAcademicYearIds.Contains(cycle.AcademicYearId) || cycle.EndDate.Date < institutionDate)
						{
							cycle.SetSchedule(null, null);
							await cycles.UpdateAsync(cycle, autoSave: false);
							await histories.InsertAsync(new AdmissionCycleHistory(Guid.NewGuid(), cycle.TenantId, cycle.Id,
								AdmissionCycleHistoryAction.ScheduleChanged, previous, previous,
								"Scheduled publication was cancelled because the cycle was no longer publish-ready.",
								"Academic Year unavailable or application window ended", now), autoSave: false);
							logger.LogWarning("Cancelled scheduled publication for admission cycle {AdmissionCycleId} because it was not publish-ready.", cycle.Id);
							continue;
						}
						cycle.Publish(null, now);
					}
					else
					{
						cycle.Close(null, now);
						action = AdmissionCycleHistoryAction.Closed;
						description = "Admission cycle closed automatically by its schedule.";
					}

					await cycles.UpdateAsync(cycle, autoSave: false);
					await histories.InsertAsync(new AdmissionCycleHistory(Guid.NewGuid(), cycle.TenantId, cycle.Id,
						action, previous, cycle.Status, description, "Scheduled lifecycle action", now), autoSave: false);
				}
			}
			await uow.CompleteAsync();
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "AdmissionCycleScheduleWorker failed to process due admission cycles.");
		}
	}
}
