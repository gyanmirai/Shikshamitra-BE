using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Academic.Curriculum;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Timetable.Timetables;

public class Timetable : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    #region Properties
    public Guid? TenantId { get; private set; }
    public Guid StandardId { get; private set; }
    public Guid DivisionId { get; private set; }
    public TimetableStatus Status { get; private set; }
    public virtual ICollection<TimetableSlot> Slots { get; private set; }
    #endregion

    #region Constructors
    private Timetable()
    {
        /* This constructor is for deserialization / ORM purpose */
        Slots = new List<TimetableSlot>();
    }

    public Timetable(Guid id, Guid? tenantId, Guid standardId, Guid divisionId)
        : base(id)
    {
        TenantId = tenantId;
        StandardId = standardId;
        DivisionId = divisionId;
        Status = TimetableStatus.Draft;
        Slots = new List<TimetableSlot>();
    }
    #endregion

    #region Methods
    public void Publish()
    {
        Status = TimetableStatus.Published;
    }

    public void SaveAsDraft()
    {
        Status = TimetableStatus.Draft;
    }

    public void AddSlot(TimetableSlot slot)
    {
        Slots.Add(slot);
    }

    public void ClearSlots()
    {
        Slots.Clear();
    }

    public void ReplaceSlots(IEnumerable<TimetableSlot> slots)
    {
        Slots.Clear();
        foreach (var slot in slots)
        {
            Slots.Add(slot);
        }
    }

    public TimetableSlot? FindSlot(TimetableDayOfWeek day, TimeOnly startTime, TimeOnly endTime)
    {
        return Slots.FirstOrDefault(s =>
            s.Day == day && s.StartTime == startTime && s.EndTime == endTime);
    }
    #endregion
}
