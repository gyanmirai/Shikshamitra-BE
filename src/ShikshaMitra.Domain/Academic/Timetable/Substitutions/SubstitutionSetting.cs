using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Timetable.Substitutions;

public class SubstitutionSetting : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid? AcademicYearId { get; private set; }
    public int MaxSubstitutionsPerTeacherPerDay { get; private set; }
    public int MaxSubstitutionsPerTeacherPerWeek { get; private set; }
    public bool AcknowledgementRequiredByDefault { get; private set; }
    public bool AutoReminderEnabled { get; private set; }
    public int ReminderBeforeMinutes { get; private set; }
    public bool NotifyLeadershipForUrgent { get; private set; }
    public bool AllowTeacherReject { get; private set; }
    public bool StudentVisibleDailySheet { get; private set; }

    private SubstitutionSetting()
    {
    }

    public SubstitutionSetting(Guid id, Guid? tenantId, Guid? academicYearId = null)
        : base(id)
    {
        TenantId = tenantId;
        AcademicYearId = academicYearId;
        Update(3, 8, true, true, 15, true, true, false);
    }

    public void Update(
        int maxSubstitutionsPerTeacherPerDay,
        int maxSubstitutionsPerTeacherPerWeek,
        bool acknowledgementRequiredByDefault,
        bool autoReminderEnabled,
        int reminderBeforeMinutes,
        bool notifyLeadershipForUrgent,
        bool allowTeacherReject,
        bool studentVisibleDailySheet)
    {
        MaxSubstitutionsPerTeacherPerDay = Math.Max(1, maxSubstitutionsPerTeacherPerDay);
        MaxSubstitutionsPerTeacherPerWeek = Math.Max(MaxSubstitutionsPerTeacherPerDay, maxSubstitutionsPerTeacherPerWeek);
        AcknowledgementRequiredByDefault = acknowledgementRequiredByDefault;
        AutoReminderEnabled = autoReminderEnabled;
        ReminderBeforeMinutes = Math.Clamp(reminderBeforeMinutes, 0, 240);
        NotifyLeadershipForUrgent = notifyLeadershipForUrgent;
        AllowTeacherReject = allowTeacherReject;
        StudentVisibleDailySheet = studentVisibleDailySheet;
    }
}
