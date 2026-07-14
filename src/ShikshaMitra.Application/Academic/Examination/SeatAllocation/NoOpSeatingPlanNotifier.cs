using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Academic.Examination.SeatAllocation.Dtos;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Academic.Examination.SeatAllocation;

/// <summary>
/// Default <see cref="ISeatingPlanNotifier"/> — no email/SMS/in-app provider exists in this
/// codebase yet, so publish just logs which channels were requested. Replace this registration
/// with a real implementation once a notification provider is available (Phase 4 extension seam).
/// </summary>
public class NoOpSeatingPlanNotifier(ILogger<NoOpSeatingPlanNotifier> logger) : ISeatingPlanNotifier, ITransientDependency
{
    public Task NotifyAsync(Guid examId, PublishSeatingPlanInputDto options)
    {
        logger.LogInformation(
            "Seating plan {ExamId} published. Notification channels requested (not yet sent): staff={Staff}, teachers={Teachers}, students={Students}, parents={Parents}, email={Email}, scheduledAt={ScheduledAt}",
            examId, options.NotifyExamStaff, options.NotifyTeachers, options.NotifyStudents, options.NotifyParents, options.SendEmail, options.ScheduleAt);
        return Task.CompletedTask;
    }
}
