using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Timetable.RoomAvailability;

public class RoomRecurringConfig : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public string RepeatType { get; private set; } // "daily", "weekly", "monthly", "custom"
    public string? RepeatDays { get; private set; } // JSON array of day numbers: e.g. [1,3,5] (Mon, Wed, Fri)
    public DateTime UntilDate { get; private set; }
    public int? Occurrences { get; private set; }
    public bool ExcludeHolidays { get; private set; }
    public bool ExcludeExamDays { get; private set; }
    public bool SkipConflictDates { get; private set; }

    private RoomRecurringConfig() { }

    public RoomRecurringConfig(
        Guid id,
        Guid? tenantId,
        string repeatType,
        DateTime untilDate,
        string? repeatDays = null,
        int? occurrences = null,
        bool excludeHolidays = true,
        bool excludeExamDays = true,
        bool skipConflictDates = false) : base(id)
    {
        TenantId = tenantId;
        RepeatType = repeatType;
        UntilDate = untilDate.Date;
        RepeatDays = repeatDays;
        Occurrences = occurrences;
        ExcludeHolidays = excludeHolidays;
        ExcludeExamDays = excludeExamDays;
        SkipConflictDates = skipConflictDates;
    }
}
