using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Academic.Timetable;

/// <summary>
/// Shared capacity/status math for Lecture Slots and Weekly Lectures settings.
/// Both app services must use this so capacity and status stay identical across screens.
/// </summary>
public static class TimetableCapacityCalculator
{
    public static int GetActiveDaysCount(IEnumerable<int> groupDayCounts) => groupDayCounts.Sum();

    public static int GetWeeklyTeachingCapacity(IEnumerable<(int TeachingSlotsPerDay, int DaysCount)> groups) =>
        groups.Sum(g => g.TeachingSlotsPerDay * g.DaysCount);

    public static int GetCapacityDifference(int availableCapacity, int requiredLectures) =>
        availableCapacity - requiredLectures;

    public static TimetableSettingStatus GetStatus(
        bool hasSlots,
        bool hasWeeklyLectures,
        int requiredWeeklyLectures,
        int availableWeeklyCapacity)
    {
        if (!hasSlots)
        {
            return TimetableSettingStatus.PendingSlotSetup;
        }

        if (!hasWeeklyLectures || requiredWeeklyLectures == 0)
        {
            return TimetableSettingStatus.PendingWeeklyLectures;
        }

        if (requiredWeeklyLectures > availableWeeklyCapacity)
        {
            return TimetableSettingStatus.OverCapacity;
        }

        if (requiredWeeklyLectures < availableWeeklyCapacity)
        {
            return TimetableSettingStatus.FreeSlotsAvailable;
        }

        return TimetableSettingStatus.Balanced;
    }
}
