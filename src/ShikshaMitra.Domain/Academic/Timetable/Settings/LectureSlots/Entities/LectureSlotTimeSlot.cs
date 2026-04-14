using System;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Academic.Timetable.Settings.LectureSlots;

public class LectureSlotTimeSlot : Entity<Guid>
{
    #region Properties
    public Guid LectureSlotGroupId { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public SlotType SlotType { get; private set; }
    public BreakType? BreakType { get; private set; }
    #endregion

    #region Constructors
    private LectureSlotTimeSlot()
    {
        /* This constructor is for deserialization / ORM purpose */
    }

    public LectureSlotTimeSlot(
        Guid id,
        Guid lectureSlotGroupId,
        TimeOnly startTime,
        TimeOnly endTime,
        SlotType slotType,
        BreakType? breakType = null)
        : base(id)
    {
        LectureSlotGroupId = lectureSlotGroupId;
        StartTime = startTime;
        EndTime = endTime;
        SlotType = slotType;
        BreakType = breakType;
    }
    #endregion
}
