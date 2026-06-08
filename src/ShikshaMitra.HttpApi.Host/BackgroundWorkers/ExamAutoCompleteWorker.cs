using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Academic.Examination.Exams;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Data;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace ShikshaMitra.BackgroundWorkers;

/// <summary>
/// Runs every hour and auto-transitions Published/Pending exams to Completed
/// once the last scheduled exam subject date has passed.
/// </summary>
public class ExamAutoCompleteWorker : AsyncPeriodicBackgroundWorkerBase
{
    public ExamAutoCompleteWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
        : base(timer, serviceScopeFactory)
    {
        Timer.Period = 60 * 60 * 1000; // 1 hour
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var logger = workerContext.ServiceProvider.GetRequiredService<ILogger<ExamAutoCompleteWorker>>();
        var examRepository = workerContext.ServiceProvider.GetRequiredService<IExamRepository>();
        var dataFilter = workerContext.ServiceProvider.GetRequiredService<IDataFilter>();
        var unitOfWorkManager = workerContext.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();

        var now = DateTime.UtcNow;
        var completedCount = 0;

        using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);

        using (dataFilter.Disable<IMultiTenant>())
        {
            var exams = await examRepository.GetListAsync(
                e => e.Status == ExamStatus.Pending || e.Status == ExamStatus.Published,
                includeDetails: true);

            foreach (var exam in exams)
            {
                // Use EndTime when available (most accurate); fall back to ExamDate when EndTime
                // was not set so exams without a time window are still auto-completed.
                var subjectsWithEndTime = exam.ExamSubjects
                    .Where(s => s.EndTime.HasValue)
                    .ToList();

                if (subjectsWithEndTime.Count > 0)
                {
                    // All scheduled subjects must have finished before we mark Completed
                    if (subjectsWithEndTime.Max(s => s.EndTime!.Value) > now)
                        continue;
                }
                else
                {
                    // Fallback: use ExamDate (date-only) when no EndTime is stored
                    var scheduledDates = exam.ExamSubjects
                        .Where(s => s.ExamDate.HasValue)
                        .Select(s => s.ExamDate!.Value.Date)
                        .ToList();

                    if (scheduledDates.Count == 0 || scheduledDates.Max() >= now.Date)
                        continue;
                }

                exam.ChangeStatus(ExamStatus.Completed);
                await examRepository.UpdateAsync(exam);
                completedCount++;
            }
        }

        await uow.CompleteAsync();

        if (completedCount > 0)
        {
            logger.LogInformation("ExamAutoCompleteWorker: marked {Count} exam(s) as Completed.", completedCount);
        }
    }
}
