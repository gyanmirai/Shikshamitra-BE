using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
    public string ActiveDaysJson { get; private set; }
    public IReadOnlyList<int> ActiveDays => DeserializeActiveDays();
    public virtual ICollection<LectureSlotTimeSlot> TimeSlots { get; private set; }
    #endregion

    #region Constructors
    private LectureSlotGroup()
    {
        /* This constructor is for deserialization / ORM purpose */
        TimeSlots = new List<LectureSlotTimeSlot>();
        ActiveDaysJson = "[]";
    }

    public LectureSlotGroup(
        Guid id,
        Guid lectureSlotConfigId,
        int totalSlots,
        int weekRangeStart,
        int weekRangeEnd,
        IEnumerable<int>? activeDays = null)
        : base(id)
    {
        LectureSlotConfigId = lectureSlotConfigId;
        TotalSlots = totalSlots;
        WeekRangeStart = weekRangeStart;
        WeekRangeEnd = weekRangeEnd;
        ActiveDaysJson = JsonSerializer.Serialize(NormalizeActiveDays(activeDays, weekRangeStart, weekRangeEnd));
        TimeSlots = new List<LectureSlotTimeSlot>();
    }
    #endregion

    #region Methods
    public void AddTimeSlot(LectureSlotTimeSlot timeSlot)
    {
        TimeSlots.Add(timeSlot);
    }

    public bool AppliesToDay(int day) => ActiveDays.Contains(day);

    private IReadOnlyList<int> DeserializeActiveDays()
    {
        try
        {
            var activeDays = JsonSerializer.Deserialize<List<int>>(ActiveDaysJson);
            if (activeDays is { Count: > 0 })
            {
                return activeDays;
            }
        }
        catch (JsonException)
        {
            // Legacy/malformed values fall back to the persisted range below.
        }

        return Enumerable.Range(WeekRangeStart, WeekRangeEnd - WeekRangeStart + 1).ToList();
    }

    private static List<int> NormalizeActiveDays(
        IEnumerable<int>? activeDays,
        int weekRangeStart,
        int weekRangeEnd)
    {
        var normalized = activeDays?
            .Where(day => day >= TimetableConsts.MinWeekRangeValue && day <= TimetableConsts.MaxWeekRangeValue)
            .Distinct()
            .OrderBy(day => day)
            .ToList();

        return normalized is { Count: > 0 }
            ? normalized
            : Enumerable.Range(weekRangeStart, weekRangeEnd - weekRangeStart + 1).ToList();
    }
    #endregion
}
