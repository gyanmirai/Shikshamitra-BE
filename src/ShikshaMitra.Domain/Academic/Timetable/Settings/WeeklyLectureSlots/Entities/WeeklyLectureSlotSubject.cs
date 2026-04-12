using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;

public class WeeklyLectureSlotSubject : FullAuditedEntity<Guid>, IMultiTenant
{
    #region Properties
    public Guid? TenantId { get; private set; }
    public Guid WeeklyLectureSlotId { get; private set; }
    public Guid? SubjectId { get; private set; }
    public Guid? ElectiveGroupId { get; private set; }
    public int WeeklyLectures { get; private set; }
    #endregion

    #region Constructors
    private WeeklyLectureSlotSubject()
    {
        /* This constructor is for deserialization / ORM purpose */
    }

    internal WeeklyLectureSlotSubject(
        Guid id,
        Guid weeklyLectureSlotId,
        Guid? subjectId,
        Guid? electiveGroupId,
        int weeklyLectures)
        : base(id)
    {
        WeeklyLectureSlotId = weeklyLectureSlotId;
        SubjectId = subjectId;
        ElectiveGroupId = electiveGroupId;
        WeeklyLectures = weeklyLectures;
    }
    #endregion
}
