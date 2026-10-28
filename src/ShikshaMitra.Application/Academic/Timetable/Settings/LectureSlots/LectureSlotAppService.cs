using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Timetable.Settings.LectureSlots;

[RemoteService(IsEnabled = false)]
[Authorize]
public partial class LectureSlotAppService(
    ILectureSlotConfigRepository lectureSlotConfigRepository,
    ITimetableRepository timetableRepository,
    IRepository<Standard, Guid> standardRepository,
    IRepository<Room, Guid> roomRepository,
    IRepository<ShiftManagement, Guid> shiftRepository,
    IWeeklyLectureSlotRepository weeklyLectureSlotRepository
) : ApplicationService, ILectureSlotAppService
{
    private const string LectureSlotConfigHasTimetableMessage =
        "This class already has generated timetable assignments. Remove or update those timetable assignments before changing lecture slot timings.";

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_SettingsLectureSlots.View)]
    public async Task<PagedResultDto<LectureSlotListDto>> GetByMediumAsync(GetLectureSlotsByMediumInput input)
    {
        var standardQueryable = await standardRepository.GetQueryableAsync();
        var standardNames = await AsyncExecuter.ToListAsync(standardQueryable
            .Where(s => s.EducationMediumStage.EducationMediumId == input.AcademicMediumId).Select(s => new { s.Id, s.Name }));
        if (!string.IsNullOrWhiteSpace(input.Search))
            standardNames = standardNames.Where(s => s.Name.Contains(input.Search, StringComparison.OrdinalIgnoreCase)).ToList();
        var standardsDict = standardNames.ToDictionary(s => s.Id, s => s.Name);
        var standardIds = standardsDict.Keys.ToArray();
        var configsQueryable = (await lectureSlotConfigRepository.GetQueryableAsync())
            .Where(config => Enumerable.Contains(standardIds, config.StandardId));
        var totalCount = await AsyncExecuter.CountAsync(configsQueryable);
        var pageIds = await AsyncExecuter.ToListAsync(TimetableSettingPageOrder.ByStandardName(configsQueryable, standardsDict,
            config => config.StandardId).ThenBy(config => config.Id).Skip(input.SkipCount).Take(input.MaxResultCount).Select(config => config.Id));
        var pageOrder = pageIds.Select((id, index) => new { id, index }).ToDictionary(item => item.id, item => item.index);
        var pageGraphs = await AsyncExecuter.ToListAsync((await lectureSlotConfigRepository.WithDetailsAsync())
            .Where(config => pageIds.Contains(config.Id)));
        var pagedConfigs = pageGraphs.OrderBy(config => pageOrder[config.Id]).ToList();
        var pageStandardIds = pagedConfigs.Select(config => config.StandardId).Distinct().ToArray();
        var pagedStandards = await standardRepository.GetListAsync(standard => Enumerable.Contains(pageStandardIds, standard.Id));
        var shiftInfos = await LoadClassShiftInfosAsync(pagedStandards);
        var roomNames = await LoadRoomNamesAsync(pagedConfigs);
        var requiredLecturesDict = await LoadRequiredWeeklyLecturesAsync(pageStandardIds.ToList());

        var items = pagedConfigs.Select(config => MapToListDto(
            config,
            standardsDict.GetValueOrDefault(config.StandardId, string.Empty),
            shiftInfos.GetValueOrDefault(config.StandardId),
            roomNames,
            requiredLecturesDict.GetValueOrDefault(config.StandardId, 0))).ToList();

        return new PagedResultDto<LectureSlotListDto>(totalCount, items);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_SettingsLectureSlots.View)]
    public async Task<LectureSlotDto> GetAsync(Guid id)
    {
        var config = await lectureSlotConfigRepository.GetAsync(id);
        var standard = await standardRepository.GetAsync(config.StandardId);
        var shiftInfo = await LoadClassShiftInfoAsync(standard);
        var roomNames = await LoadRoomNamesAsync([config]);
        var weeklyLectureSlot = await weeklyLectureSlotRepository.FindByStandardIdAsync(config.StandardId);

        return MapToDto(config, standard.Name, shiftInfo, roomNames, weeklyLectureSlot?.GetTotalLectures() ?? 0);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_SettingsLectureSlots.Add)]
    public async Task<LectureSlotDto> CreateAsync(CreateLectureSlotDto input)
    {
        var existing = await lectureSlotConfigRepository.FindByStandardIdAsync(input.StandardId);
        if (existing is not null)
        {
            throw new BusinessException(TimetableErrorCodes.LectureSlotConfigAlreadyExists);
        }

        await EnsureRoomsExistAsync(input.SlotConfigurations);
        var standard = await standardRepository.GetAsync(input.StandardId);
        var shiftInfo = await RequireClassShiftAsync(standard);
        EnsureSlotConfigurationsWithinShift(input.StandardId, input.SlotConfigurations, shiftInfo);

        var config = new LectureSlotConfig(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            input.StandardId,
            input.SchoolHours,
            input.PreviousStandardId);

        BuildSlotGroups(config, input.SlotConfigurations);

        await lectureSlotConfigRepository.InsertAsync(config);

        var roomNames = await LoadRoomNamesAsync([config]);
        var weeklyLectureSlot = await weeklyLectureSlotRepository.FindByStandardIdAsync(config.StandardId);
        return MapToDto(config, standard.Name, shiftInfo, roomNames, weeklyLectureSlot?.GetTotalLectures() ?? 0);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_SettingsLectureSlots.Add)]
    public async Task<LectureSlotDto> UpdateAsync(Guid id, UpdateLectureSlotDto input)
    {
        var config = await lectureSlotConfigRepository.GetAsync(id);

        await ValidateActiveAssignmentsNotModifiedAsync(config.StandardId, input.SlotConfigurations);

        await EnsureRoomsExistAsync(input.SlotConfigurations);
        var standard = await standardRepository.GetAsync(config.StandardId);
        var shiftInfo = await RequireClassShiftAsync(standard);
        EnsureSlotConfigurationsWithinShift(config.StandardId, input.SlotConfigurations, shiftInfo);

        config.SetSchoolHours(input.SchoolHours);
        config.SetPreviousStandardId(input.PreviousStandardId);
        config.ClearSlotGroups();
        BuildSlotGroups(config, input.SlotConfigurations);

        await lectureSlotConfigRepository.UpdateAsync(config);

        var roomNames = await LoadRoomNamesAsync([config]);
        var weeklyLectureSlot = await weeklyLectureSlotRepository.FindByStandardIdAsync(config.StandardId);
        return MapToDto(config, standard.Name, shiftInfo, roomNames, weeklyLectureSlot?.GetTotalLectures() ?? 0);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_SettingsLectureSlots.Add)]
    public async Task AssignAsync(Guid id, AssignLectureSlotDto input)
    {
        var source = await lectureSlotConfigRepository.GetAsync(id);
        var context = await LoadAssignmentReadsAsync(input.SelectedStandardIds);
        var inserted = new List<LectureSlotConfig>();
        var updated = new List<LectureSlotConfig>();

        foreach (var standardId in input.SelectedStandardIds)
        {
            if (!context.Standards.TryGetValue(standardId, out _))
                throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(Standard), standardId);
            var shiftInfo = context.Shifts.GetValueOrDefault(standardId);
            if (shiftInfo == null || !shiftInfo.HasClassShift)
                throw new BusinessException(TimetableErrorCodes.LectureSlotClassShiftNotAssigned)
                    .WithData("StandardId", standardId)
                    .WithData("message", "Please assign the school shift to this class before configuring lecture slots.");
            EnsureSlotConfigWithinShift(standardId, source, shiftInfo);

            ValidateActiveAssignmentsCompatibleWithSource(standardId, source, context.Timings[standardId]);

            var existing = context.Configurations.GetValueOrDefault(standardId);
            if (existing is not null)
            {
                // Overwrite in-place: replace school hours and all slot groups
                existing.SetSchoolHours(source.SchoolHours);
                existing.SetPreviousStandardId(source.PreviousStandardId);
                existing.ClearSlotGroups();

                foreach (var group in source.SlotGroups)
                {
                    var newGroup = new LectureSlotGroup(
                        GuidGenerator.Create(),
                        existing.Id,
                        group.TotalSlots,
                        group.WeekRangeStart,
                        group.WeekRangeEnd,
                        group.ActiveDays);

                    foreach (var timeSlot in group.TimeSlots)
                    {
                        newGroup.AddTimeSlot(new LectureSlotTimeSlot(
                            GuidGenerator.Create(),
                            newGroup.Id,
                            timeSlot.StartTime,
                            timeSlot.EndTime,
                            timeSlot.SlotType,
                            timeSlot.BreakType,
                            timeSlot.RoomId));
                    }

                    existing.AddSlotGroup(newGroup);
                }

                updated.Add(existing);
            }
            else
            {
                var clone = new LectureSlotConfig(
                    GuidGenerator.Create(),
                    CurrentTenant.Id,
                    standardId,
                    source.SchoolHours,
                    source.PreviousStandardId);

                foreach (var group in source.SlotGroups)
                {
                    var newGroup = new LectureSlotGroup(
                        GuidGenerator.Create(),
                        clone.Id,
                        group.TotalSlots,
                        group.WeekRangeStart,
                        group.WeekRangeEnd,
                        group.ActiveDays);

                    foreach (var timeSlot in group.TimeSlots)
                    {
                        newGroup.AddTimeSlot(new LectureSlotTimeSlot(
                            GuidGenerator.Create(),
                            newGroup.Id,
                            timeSlot.StartTime,
                            timeSlot.EndTime,
                            timeSlot.SlotType,
                            timeSlot.BreakType,
                            timeSlot.RoomId));
                    }

                    clone.AddSlotGroup(newGroup);
                }

                inserted.Add(clone);
            }
        }
        if (updated.Count > 0) await lectureSlotConfigRepository.UpdateManyAsync(updated);
        if (inserted.Count > 0) await lectureSlotConfigRepository.InsertManyAsync(inserted);
    }

    private async Task ValidateActiveAssignmentsNotModifiedAsync(Guid standardId, List<SlotConfigurationDto> newConfigurations)
    {
        var existingTimetables = await timetableRepository.GetByStandardIdAsync(standardId);
        if (existingTimetables.Count == 0)
        {
            return;
        }

        var activeSlots = existingTimetables
            .SelectMany(t => t.Slots)
            .Where(s => s.Subjects.Count > 0)
            .Select(s => new { s.Day, s.StartTime, s.EndTime, s.SlotType })
            .Distinct()
            .ToList();

        if (activeSlots.Count == 0)
        {
            return;
        }

        foreach (var activeSlot in activeSlots)
        {
            var dayValue = (int)activeSlot.Day;
            var matchingGroup = newConfigurations.FirstOrDefault(g =>
                ResolveActiveDays(g).Contains(dayValue));

            if (matchingGroup == null)
            {
                throw new BusinessException(TimetableErrorCodes.LectureSlotConfigHasTimetable)
                    .WithData("StandardId", standardId)
                    .WithData("message", LectureSlotConfigHasTimetableMessage);
            }

            var matchingSlot = matchingGroup.TimeSlots.FirstOrDefault(ts =>
                ts.StartTime == activeSlot.StartTime &&
                ts.EndTime == activeSlot.EndTime &&
                ts.SlotType == activeSlot.SlotType);

            if (matchingSlot == null)
            {
                throw new BusinessException(TimetableErrorCodes.LectureSlotConfigHasTimetable)
                    .WithData("StandardId", standardId)
                    .WithData("message", LectureSlotConfigHasTimetableMessage);
            }
        }
    }

    private static void ValidateActiveAssignmentsCompatibleWithSource(Guid standardId, LectureSlotConfig sourceConfig,
        IEnumerable<ActiveAssignmentTiming> activeSlots)
    {
        foreach (var activeSlot in activeSlots)
        {
            var dayValue = (int)activeSlot.Day;
            var matchingGroup = sourceConfig.SlotGroups.FirstOrDefault(g =>
                g.AppliesToDay(dayValue));

            if (matchingGroup == null)
            {
                throw new BusinessException(TimetableErrorCodes.LectureSlotConfigHasTimetable)
                    .WithData("StandardId", standardId)
                    .WithData("message", LectureSlotConfigHasTimetableMessage);
            }

            var matchingSlot = matchingGroup.TimeSlots.FirstOrDefault(ts =>
                ts.StartTime == activeSlot.StartTime &&
                ts.EndTime == activeSlot.EndTime &&
                ts.SlotType == activeSlot.SlotType);

            if (matchingSlot == null)
            {
                throw new BusinessException(TimetableErrorCodes.LectureSlotConfigHasTimetable)
                    .WithData("StandardId", standardId)
                    .WithData("message", LectureSlotConfigHasTimetableMessage);
            }
        }
    }

    private void BuildSlotGroups(LectureSlotConfig config, List<SlotConfigurationDto> configurations)
    {
        foreach (var slotConfig in configurations)
        {
            var activeDays = ResolveActiveDays(slotConfig);
            var group = new LectureSlotGroup(
                GuidGenerator.Create(),
                config.Id,
                slotConfig.TotalSlots,
                activeDays.Min(),
                activeDays.Max(),
                activeDays);

            foreach (var ts in slotConfig.TimeSlots)
            {
                group.AddTimeSlot(new LectureSlotTimeSlot(
                    GuidGenerator.Create(),
                    group.Id,
                    ts.StartTime,
                    ts.EndTime,
                    ts.SlotType,
                    ts.BreakType,
                    ts.RoomId));
            }

            config.AddSlotGroup(group);
        }
    }

    private static List<int> ResolveActiveDays(SlotConfigurationDto slotConfig)
    {
        if (slotConfig.ActiveDays.Count > 0)
        {
            return slotConfig.ActiveDays.Distinct().OrderBy(day => day).ToList();
        }

        var start = slotConfig.WeekRange.Count > 0 ? slotConfig.WeekRange[0] : TimetableConsts.MinWeekRangeValue;
        var end = slotConfig.WeekRange.Count > 1 ? slotConfig.WeekRange[1] : TimetableConsts.MaxWeekRangeValue;
        return Enumerable.Range(start, end - start + 1).ToList();
    }

    private async Task EnsureRoomsExistAsync(List<SlotConfigurationDto> configurations)
    {
        var roomIds = configurations
            .SelectMany(config => config.TimeSlots)
            .Select(timeSlot => timeSlot.RoomId)
            .Where(roomId => roomId.HasValue)
            .Select(roomId => roomId!.Value)
            .Distinct()
            .ToList();

        if (roomIds.Count == 0)
        {
            return;
        }

        var rooms = await AsyncExecuter.ToListAsync(
            (await roomRepository.GetQueryableAsync()).Where(room => roomIds.Contains(room.Id)));
        var foundRoomIds = rooms.Select(room => room.Id).ToHashSet();
        var missingRoomIds = roomIds.Where(roomId => !foundRoomIds.Contains(roomId)).ToList();
        if (missingRoomIds.Count != 0)
        {
            throw new BusinessException(TimetableErrorCodes.LectureSlotRoomNotFound)
                .WithData("RoomIds", string.Join(", ", missingRoomIds));
        }
    }

    private async Task<Dictionary<Guid, ClassShiftInfo>> LoadClassShiftInfosAsync(List<Standard> standards)
    {
        var shiftIds = standards
            .Where(standard => standard.ShiftId.HasValue)
            .Select(standard => standard.ShiftId!.Value)
            .Distinct()
            .ToList();

        var shifts = shiftIds.Count == 0
            ? new Dictionary<Guid, ShiftManagement>()
            : (await AsyncExecuter.ToListAsync(
                (await shiftRepository.GetQueryableAsync()).Where(shift => shiftIds.Contains(shift.Id))))
                .ToDictionary(shift => shift.Id);

        return standards.ToDictionary(
            standard => standard.Id,
            standard => ResolveClassShiftInfo(
                standard,
                shifts.GetValueOrDefault(standard.ShiftId ?? Guid.Empty)));
    }

    private async Task<ClassShiftInfo> LoadClassShiftInfoAsync(Standard standard)
    {
        if (!standard.ShiftId.HasValue)
        {
            return ResolveClassShiftInfo(standard, null);
        }

        var shift = await shiftRepository.FindAsync(standard.ShiftId.Value);
        return ResolveClassShiftInfo(standard, shift);
    }

    private async Task<ClassShiftInfo> RequireClassShiftAsync(Standard standard)
    {
        var shiftInfo = await LoadClassShiftInfoAsync(standard);
        if (!shiftInfo.HasClassShift)
        {
            throw new BusinessException(TimetableErrorCodes.LectureSlotClassShiftNotAssigned)
                .WithData("StandardId", standard.Id)
                .WithData("message", "Please assign the school shift to this class before configuring lecture slots.");
        }

        return shiftInfo;
    }

    private static ClassShiftInfo ResolveClassShiftInfo(Standard standard, ShiftManagement? shift)
    {
        var hasShift = standard.ShiftId.HasValue && shift != null;
        return new ClassShiftInfo(
            standard.ShiftId,
            shift?.ShiftName,
            shift?.StartTime,
            shift?.EndTime,
            hasShift ? $"{shift!.ShiftName} ({shift.StartTime:HH:mm} - {shift.EndTime:HH:mm})" : null,
            hasShift);
    }

    private static void EnsureSlotConfigurationsWithinShift(
        Guid standardId,
        List<SlotConfigurationDto> configurations,
        ClassShiftInfo shiftInfo)
    {
        foreach (var slotConfig in configurations)
        {
            foreach (var slot in slotConfig.TimeSlots)
            {
                EnsureSlotWithinShift(standardId, slot.StartTime, slot.EndTime, shiftInfo);
            }
        }
    }

    private static void EnsureSlotConfigWithinShift(
        Guid standardId,
        LectureSlotConfig config,
        ClassShiftInfo shiftInfo)
    {
        foreach (var group in config.SlotGroups)
        {
            foreach (var slot in group.TimeSlots)
            {
                EnsureSlotWithinShift(standardId, slot.StartTime, slot.EndTime, shiftInfo);
            }
        }
    }

    private static void EnsureSlotWithinShift(
        Guid standardId,
        TimeOnly startTime,
        TimeOnly endTime,
        ClassShiftInfo shiftInfo)
    {
        if (!shiftInfo.ShiftStartTime.HasValue || !shiftInfo.ShiftEndTime.HasValue)
        {
            throw new BusinessException(TimetableErrorCodes.LectureSlotClassShiftNotAssigned)
                .WithData("StandardId", standardId)
                .WithData("message", "Please assign the school shift to this class before configuring lecture slots.");
        }

        if (startTime < shiftInfo.ShiftStartTime.Value || endTime > shiftInfo.ShiftEndTime.Value)
        {
            throw new BusinessException(TimetableErrorCodes.LectureSlotOutsideClassShift)
                .WithData("StandardId", standardId)
                .WithData("StartTime", startTime.ToString("HH:mm"))
                .WithData("EndTime", endTime.ToString("HH:mm"))
                .WithData("ShiftStartTime", shiftInfo.ShiftStartTime.Value.ToString("HH:mm"))
                .WithData("ShiftEndTime", shiftInfo.ShiftEndTime.Value.ToString("HH:mm"))
                .WithData("message", $"Lecture slot timing must be within the class shift timing {shiftInfo.ShiftStartTime.Value:HH:mm} - {shiftInfo.ShiftEndTime.Value:HH:mm}.");
        }
    }

    private async Task<Dictionary<Guid, string>> LoadRoomNamesAsync(IEnumerable<LectureSlotConfig> configs)
    {
        var roomIds = configs
            .SelectMany(config => config.SlotGroups)
            .SelectMany(group => group.TimeSlots)
            .Select(timeSlot => timeSlot.RoomId)
            .Where(roomId => roomId.HasValue)
            .Select(roomId => roomId!.Value)
            .Distinct()
            .ToList();

        if (roomIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var rooms = await AsyncExecuter.ToListAsync(
            (await roomRepository.GetQueryableAsync()).Where(room => roomIds.Contains(room.Id)));
        return rooms.ToDictionary(room => room.Id, room => room.RoomName);
    }

    private async Task<Dictionary<Guid, int>> LoadRequiredWeeklyLecturesAsync(List<Guid> standardIds)
    {
        if (standardIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var queryable = await weeklyLectureSlotRepository.WithDetailsAsync();
        var slots = await AsyncExecuter.ToListAsync(queryable.Where(w => standardIds.Contains(w.StandardId)));
        return slots.ToDictionary(w => w.StandardId, w => w.GetTotalLectures());
    }

    private static (int TotalSlotsPerDay, int TeachingSlotsPerDay, int NonTeachingSlotsPerDay, int ActiveDaysCount, int WeeklyTeachingCapacity, int Difference, TimetableSettingStatus Status)
        ComputeCapacityStats(LectureSlotConfig config, int requiredWeeklyLectures)
    {
        var groups = config.SlotGroups.ToList();
        var primaryGroup = groups.FirstOrDefault();

        var totalSlotsPerDay = primaryGroup?.TotalSlots ?? 0;
        var teachingSlotsPerDay = primaryGroup?.TimeSlots.Count(ts => ts.SlotType == SlotType.Period) ?? 0;
        var nonTeachingSlotsPerDay = totalSlotsPerDay - teachingSlotsPerDay;

        var activeDaysCount = TimetableCapacityCalculator.GetActiveDaysCount(
            groups.Select(g => g.ActiveDays.Count));

        var weeklyTeachingCapacity = TimetableCapacityCalculator.GetWeeklyTeachingCapacity(
            groups.Select(g => (g.TimeSlots.Count(ts => ts.SlotType == SlotType.Period), g.ActiveDays.Count)));

        var difference = TimetableCapacityCalculator.GetCapacityDifference(weeklyTeachingCapacity, requiredWeeklyLectures);

        var status = TimetableCapacityCalculator.GetStatus(
            hasSlots: groups.Count > 0,
            hasWeeklyLectures: requiredWeeklyLectures > 0,
            requiredWeeklyLectures: requiredWeeklyLectures,
            availableWeeklyCapacity: weeklyTeachingCapacity);

        return (totalSlotsPerDay, teachingSlotsPerDay, nonTeachingSlotsPerDay, activeDaysCount, weeklyTeachingCapacity, difference, status);
    }

    private static LectureSlotDto MapToDto(
        LectureSlotConfig config, string standardName, ClassShiftInfo shiftInfo, IReadOnlyDictionary<Guid, string> roomNames, int requiredWeeklyLectures)
    {
        var stats = ComputeCapacityStats(config, requiredWeeklyLectures);

        return new LectureSlotDto
        {
            Id = config.Id,
            StandardId = config.StandardId,
            StandardName = standardName,
            ShiftId = shiftInfo.ShiftId,
            ShiftName = shiftInfo.ShiftName,
            ShiftStartTime = shiftInfo.ShiftStartTime,
            ShiftEndTime = shiftInfo.ShiftEndTime,
            ShiftTimingLabel = shiftInfo.ShiftTimingLabel,
            HasClassShift = shiftInfo.HasClassShift,
            SchoolHours = config.SchoolHours,
            PreviousStandardId = config.PreviousStandardId,
            CreatedAt = config.CreationTime,
            UpdatedAt = config.LastModificationTime,
            SlotConfigurations = config.SlotGroups
                .OrderBy(g => g.TimeSlots.Count > 0 ? g.TimeSlots.Min(ts => ts.StartTime) : TimeOnly.MaxValue)
                .Select(g => new SlotConfigurationDetailDto
                {
                    TotalSlots = g.TotalSlots,
                    WeekRange = [g.WeekRangeStart, g.WeekRangeEnd],
                    ActiveDays = g.ActiveDays.ToList(),
                    TimeSlots = g.TimeSlots
                        .OrderBy(ts => ts.StartTime)
                        .Select(ts => new TimeSlotDetailDto
                        {
                            StartTime = ts.StartTime,
                            EndTime = ts.EndTime,
                            SlotType = ts.SlotType,
                            RoomId = ts.RoomId,
                            RoomName = ts.RoomId.HasValue ? roomNames.GetValueOrDefault(ts.RoomId.Value) : null,
                            BreakType = ts.BreakType
                        }).ToList()
                }).ToList(),
            TotalSlotsPerDay = stats.TotalSlotsPerDay,
            TeachingSlotsPerDay = stats.TeachingSlotsPerDay,
            NonTeachingSlotsPerDay = stats.NonTeachingSlotsPerDay,
            ActiveDaysCount = stats.ActiveDaysCount,
            WeeklyTeachingCapacity = stats.WeeklyTeachingCapacity,
            RequiredWeeklyLectures = requiredWeeklyLectures,
            Difference = stats.Difference,
            Status = stats.Status
        };
    }

    private static LectureSlotListDto MapToListDto(
        LectureSlotConfig config, string standardName, ClassShiftInfo shiftInfo, IReadOnlyDictionary<Guid, string> roomNames, int requiredWeeklyLectures)
    {
        var stats = ComputeCapacityStats(config, requiredWeeklyLectures);

        return new LectureSlotListDto
        {
            Id = config.Id,
            StandardId = config.StandardId,
            StandardName = standardName,
            ShiftId = shiftInfo.ShiftId,
            ShiftName = shiftInfo.ShiftName,
            ShiftStartTime = shiftInfo.ShiftStartTime,
            ShiftEndTime = shiftInfo.ShiftEndTime,
            ShiftTimingLabel = shiftInfo.ShiftTimingLabel,
            HasClassShift = shiftInfo.HasClassShift,
            SlotConfigurations = config.SlotGroups
                .OrderBy(g => g.TimeSlots.Count > 0 ? g.TimeSlots.Min(ts => ts.StartTime) : TimeOnly.MaxValue)
                .Select(g => new SlotConfigurationSummaryDto
                {
                    FromDay = g.WeekRangeStart,
                    ToDay = g.WeekRangeEnd,
                    ActiveDays = g.ActiveDays.ToList(),
                    TotalSlots = g.TotalSlots,
                    TimeSlots = g.TimeSlots
                        .OrderBy(ts => ts.StartTime)
                        .Select(ts => new TimeSlotDetailDto
                        {
                            StartTime = ts.StartTime,
                            EndTime = ts.EndTime,
                            SlotType = ts.SlotType,
                            RoomId = ts.RoomId,
                            RoomName = ts.RoomId.HasValue ? roomNames.GetValueOrDefault(ts.RoomId.Value) : null,
                            BreakType = ts.BreakType
                        }).ToList()
                }).ToList(),
            TotalSlotsPerDay = stats.TotalSlotsPerDay,
            TeachingSlotsPerDay = stats.TeachingSlotsPerDay,
            NonTeachingSlotsPerDay = stats.NonTeachingSlotsPerDay,
            ActiveDaysCount = stats.ActiveDaysCount,
            WeeklyTeachingCapacity = stats.WeeklyTeachingCapacity,
            RequiredWeeklyLectures = requiredWeeklyLectures,
            Difference = stats.Difference,
            Status = stats.Status
        };
    }

    private sealed record ClassShiftInfo(
        Guid? ShiftId,
        string? ShiftName,
        TimeOnly? ShiftStartTime,
        TimeOnly? ShiftEndTime,
        string? ShiftTimingLabel,
        bool HasClassShift);
}
