using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;

public class WeeklyLectureSlot : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    #region Properties
    public Guid? TenantId { get; private set; }
    public Guid StandardId { get; private set; }
    public virtual ICollection<WeeklyLectureSlotSubject> Subjects { get; private set; }
    #endregion

    #region Constructors
    private WeeklyLectureSlot()
    {
        /* This constructor is for deserialization / ORM purpose */
        Subjects = new HashSet<WeeklyLectureSlotSubject>();
    }

    public WeeklyLectureSlot(Guid id, Guid? tenantId, Guid standardId)
        : base(id)
    {
        TenantId = tenantId;
        StandardId = standardId;
        Subjects = new HashSet<WeeklyLectureSlotSubject>();
    }
    #endregion

    #region Methods
    public int GetTotalLectures() => Subjects.Sum(s => s.WeeklyLectures);

    public void AddSubject(Guid subjectEntryId, Guid? subjectId, Guid? electiveGroupId, int weeklyLectures)
    {
        Check.Range(weeklyLectures, nameof(weeklyLectures),
            TimetableConsts.MinWeeklyLectures, TimetableConsts.MaxWeeklyLectures);

        Subjects.Add(new WeeklyLectureSlotSubject(
            subjectEntryId, Id, subjectId, electiveGroupId, weeklyLectures));
    }

    public void ReplaceSubjects(IEnumerable<(Guid Id, Guid? SubjectId, Guid? ElectiveGroupId, int WeeklyLectures)> subjects)
    {
        Subjects.Clear();
        foreach (var (id, subjectId, electiveGroupId, weeklyLectures) in subjects)
        {
            AddSubject(id, subjectId, electiveGroupId, weeklyLectures);
        }
    }
    #endregion
}
