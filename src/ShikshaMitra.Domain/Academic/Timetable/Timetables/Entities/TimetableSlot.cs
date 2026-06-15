using System;
using System.Collections.Generic;
using ShikshaMitra.Academic.Curriculum;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Academic.Timetable.Timetables;

public class TimetableSlot : Entity<Guid>
{
    #region Properties
    public Guid TimetableId { get; private set; }
    public TimetableDayOfWeek Day { get; private set; }
    public int Sequence { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public SlotType SlotType { get; private set; }
    public BreakType? BreakType { get; private set; }
    public SubjectType? SubjectType { get; private set; }
    public Guid? ElectiveGroupId { get; private set; }
    public virtual ICollection<TimetableSlotSubject> Subjects { get; private set; }
    #endregion

    #region Constructors
    private TimetableSlot()
    {
        /* This constructor is for deserialization / ORM purpose */
        Subjects = new List<TimetableSlotSubject>();
    }

    public TimetableSlot(
        Guid id,
        Guid timetableId,
        TimetableDayOfWeek day,
        int sequence,
        TimeOnly startTime,
        TimeOnly endTime,
        SlotType slotType,
        BreakType? breakType = null,
        SubjectType? subjectType = null,
        Guid? electiveGroupId = null)
        : base(id)
    {
        TimetableId = timetableId;
        Day = day;
        Sequence = sequence;
        StartTime = startTime;
        EndTime = endTime;
        SlotType = slotType;
        BreakType = breakType;
        SubjectType = subjectType;
        ElectiveGroupId = electiveGroupId;
        Subjects = new List<TimetableSlotSubject>();
    }
    #endregion

    #region Methods
    public void AddSubject(TimetableSlotSubject subject)
    {
        Subjects.Add(subject);
    }

    public void ClearSubjects()
    {
        Subjects.Clear();
    }

    public void ReplaceSubjects(IEnumerable<TimetableSlotSubject> subjects)
    {
        Subjects.Clear();
        foreach (var subject in subjects)
        {
            Subjects.Add(subject);
        }
    }

    public void ClearElectiveGroup()
    {
        ElectiveGroupId = null;
    }
    #endregion
}
