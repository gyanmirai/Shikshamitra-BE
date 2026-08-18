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
/// Runs periodically and auto-transitions Published/Pending exams to Completed
/// once the last scheduled exam subject date has passed.
/// </summary>
public class ExamAutoCompleteWorker : AsyncPeriodicBackgroundWorkerBase
{
    public ExamAutoCompleteWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
        : base(timer, serviceScopeFactory)
    {
        Timer.Period = 5 * 60 * 1000; // 5 minutes
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var logger = workerContext.ServiceProvider.GetRequiredService<ILogger<ExamAutoCompleteWorker>>();
        var examRepository = workerContext.ServiceProvider.GetRequiredService<IExamRepository>();
        var dataFilter = workerContext.ServiceProvider.GetRequiredService<IDataFilter>();
        var unitOfWorkManager = workerContext.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();

        var nowUtc = DateTime.UtcNow;
        var institutionTimeZone = GetInstitutionTimeZone();
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, institutionTimeZone);
        var completedCount = 0;

        try
        {
            using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);

            using (dataFilter.Disable<IMultiTenant>())
            {
                var exams = await examRepository.GetListAsync(
                    e => e.Status == ExamStatus.Pending || e.Status == ExamStatus.Published,
                    includeDetails: true);

                foreach (var exam in exams)
                {
                    // Exam schedule values are institution-local date/time values.
                    // Convert them to UTC before comparing with the server clock.
                    var subjectsWithEndTime = exam.ExamSubjects
                        .Where(s => s.ExamDate.HasValue && s.EndTime.HasValue)
                        .ToList();

                    if (subjectsWithEndTime.Count > 0)
                    {
                        var finalEndAtUtc = subjectsWithEndTime
                            .Max(s => ToInstitutionUtc(s.ExamDate!.Value.Date.Add(s.EndTime!.Value.TimeOfDay), institutionTimeZone));

                        if (finalEndAtUtc > nowUtc)
                            continue;
                    }
                    else
                    {
                        // Fallback: use the institution-local exam date when no end time is stored.
                        var scheduledDates = exam.ExamSubjects
                            .Where(s => s.ExamDate.HasValue)
                            .Select(s => s.ExamDate!.Value.Date)
                            .ToList();

                        if (scheduledDates.Count == 0 || scheduledDates.Max() >= nowLocal.Date)
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
        catch (Exception exception)
        {
            logger.LogError(exception, "ExamAutoCompleteWorker: failed to auto-complete exams.");
        }
    }

    private static DateTime ToInstitutionUtc(DateTime localDateTime, TimeZoneInfo institutionTimeZone)
    {
        return TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified),
            institutionTimeZone);
    }

    private static TimeZoneInfo GetInstitutionTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
        }
    }
}
