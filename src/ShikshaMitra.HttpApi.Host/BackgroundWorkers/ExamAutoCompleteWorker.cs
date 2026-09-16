using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Academic.Examination.Exams;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

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
        var completionService = workerContext.ServiceProvider.GetRequiredService<ExamAutoCompletionService>();

        try
        {
            var completedCount = await completionService.CompleteDueExamsAsync(
                DateTime.UtcNow, workerContext.CancellationToken);

            if (completedCount > 0)
            {
                logger.LogInformation("ExamAutoCompleteWorker: marked {Count} exam(s) as Completed.", completedCount);
            }
        }
        catch (OperationCanceledException) when (workerContext.CancellationToken.IsCancellationRequested)
        {
            // Host shutdown leaves unprocessed exams eligible for the next run.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "ExamAutoCompleteWorker: failed to auto-complete exams.");
        }
    }

}
