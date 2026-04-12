using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Timetable.Settings.LectureSlots;

public class LectureSlotGroup : FullAuditedEntity<Guid>, IMultiTenant
{
    #region Properties
    public Guid? TenantId { get; private set; }
    public Guid LectureSlotConfigId { get; private set; }
    public int TotalSlots { get; private set; }
    public int WeekRangeStart { get; private set; }
    public int WeekRangeEnd { get; private set; }
    public virtual ICollection<LectureSlotTimeSlot> TimeSlots { get; private set; }
    #endregion

    #region Constructors
    private LectureSlotGroup()
    {
        /* This constructor is for deserialization / ORM purpose */
        TimeSlots = new List<LectureSlotTimeSlot>();
    }

    public LectureSlotGroup(
        Guid id,
        Guid lectureSlotConfigId,
        int totalSlots,
        int weekRangeStart,
        int weekRangeEnd)
        : base(id)
    {
        LectureSlotConfigId = lectureSlotConfigId;
        TotalSlots = totalSlots;
        WeekRangeStart = weekRangeStart;
        WeekRangeEnd = weekRangeEnd;
        TimeSlots = new List<LectureSlotTimeSlot>();
    }
    #endregion

    #region Methods
    public void AddTimeSlot(LectureSlotTimeSlot timeSlot)
    {
        TimeSlots.Add(timeSlot);
    }
    #endregion
}
