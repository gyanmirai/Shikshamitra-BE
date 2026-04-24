using System;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Academic.Timetable.Timetables;

public class TimetableSlotSubject : Entity<Guid>
{
    #region Properties
    public Guid TimetableSlotId { get; private set; }
    public Guid SubjectId { get; private set; }
    public Guid? TeacherId { get; private set; }
    public Guid? RoomId { get; private set; }
    #endregion

    #region Constructors
    private TimetableSlotSubject()
    {
        /* This constructor is for deserialization / ORM purpose */
    }

    public TimetableSlotSubject(
        Guid id,
        Guid timetableSlotId,
        Guid subjectId,
        Guid? teacherId = null,
        Guid? roomId = null)
        : base(id)
    {
        TimetableSlotId = timetableSlotId;
        SubjectId = subjectId;
        TeacherId = teacherId;
        RoomId = roomId;
    }
    #endregion

    #region Methods
    public void AssignTeacher(Guid teacherId)
    {
        TeacherId = teacherId;
    }

    public void AssignRoom(Guid roomId)
    {
        RoomId = roomId;
    }
    #endregion
}
