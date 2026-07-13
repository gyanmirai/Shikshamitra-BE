using System;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.SeatAllocation.Dtos;

namespace ShikshaMitra.Academic.Examination.SeatAllocation;

/// <summary>
/// Extension seam for the "publish to teachers/students/parents" notification options in spec §19.2.
/// No real email/SMS/in-app provider is wired up in this codebase yet, so the default
/// implementation only logs intent — swap in a real implementation once a notification
/// provider exists, without touching <c>ExamSeatAllocationAppService.PublishSeatingPlanAsync</c>.
/// </summary>
public interface ISeatingPlanNotifier
{
    Task NotifyAsync(Guid examId, PublishSeatingPlanInputDto options);
}
