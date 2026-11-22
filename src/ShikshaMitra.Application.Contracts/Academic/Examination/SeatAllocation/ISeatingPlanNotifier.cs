using System;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.SeatAllocation.Dtos;

namespace ShikshaMitra.Academic.Examination.SeatAllocation;

/// <summary>
/// Saves the notification options with the immutable seating publication and captures eligible delivery.
/// </summary>
public interface ISeatingPlanNotifier
{
    Task LockPublicationAsync(Guid examId);
    Task NotifyAsync(Guid examId, PublishSeatingPlanInputDto options);
}
