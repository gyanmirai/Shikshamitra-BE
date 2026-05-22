using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Timetable.Settings.LectureSlots;

[RemoteService(IsEnabled = false)]
[Authorize]
public class LectureSlotAppService(
    ILectureSlotConfigRepository lectureSlotConfigRepository,
    ITimetableRepository timetableRepository,
    IRepository<Standard, Guid> standardRepository
) : ApplicationService, ILectureSlotAppService
{
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_SettingsLectureSlots.View)]
    public async Task<PagedResultDto<LectureSlotListDto>> GetByMediumAsync(GetLectureSlotsByMediumInput input)
    {
        // Load all standards under the requested medium for name lookup
        var standardQueryable = await standardRepository.GetQueryableAsync();
        var allStandards = await AsyncExecuter.ToListAsync(
            standardQueryable.Where(s => s.EducationMediumStage.EducationMediumId == input.AcademicMediumId));

        var standardIds = allStandards.Select(s => s.Id).ToList();

        // Query configs (which represent "created lecture slots") for those standards
        var configsQueryable = await lectureSlotConfigRepository.WithDetailsAsync();
        var allConfigs = await AsyncExecuter.ToListAsync(
            configsQueryable.Where(c => standardIds.Contains(c.StandardId)));

        // Apply optional search filter on standard name
        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var matchingStandardIds = allStandards
                .Where(s => s.Name.Contains(input.Search, StringComparison.OrdinalIgnoreCase))
                .Select(s => s.Id)
                .ToHashSet();
            allConfigs = allConfigs.Where(c => matchingStandardIds.Contains(c.StandardId)).ToList();
        }

        var totalCount = allConfigs.Count;
        var pagedConfigs = allConfigs
            .OrderBy(c => allStandards.First(s => s.Id == c.StandardId).Name)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        var standardsDict = allStandards.ToDictionary(s => s.Id, s => s.Name);

        var items = pagedConfigs.Select(config => new LectureSlotListDto
        {
            Id = config.Id,
            StandardId = config.StandardId,
            StandardName = standardsDict.GetValueOrDefault(config.StandardId, string.Empty),
            SlotConfigurations = config.SlotGroups
                .OrderBy(g => g.TimeSlots.Count > 0 ? g.TimeSlots.Min(ts => ts.StartTime) : TimeOnly.MaxValue)
                .Select(g => new SlotConfigurationSummaryDto
                {
                    FromDay = g.WeekRangeStart,
                    ToDay = g.WeekRangeEnd,
                    TotalSlots = g.TotalSlots,
                    TimeSlots = g.TimeSlots
                        .OrderBy(ts => ts.StartTime)
                        .Select(ts => new TimeSlotDetailDto
                        {
                            StartTime = ts.StartTime,
                            EndTime = ts.EndTime,
                            SlotType = ts.SlotType,
                            BreakType = ts.BreakType
                        }).ToList()
                }).ToList()
        }).ToList();

        return new PagedResultDto<LectureSlotListDto>(totalCount, items);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_SettingsLectureSlots.View)]
    public async Task<LectureSlotDto> GetAsync(Guid id)
    {
        var config = await lectureSlotConfigRepository.GetAsync(id);
        var standard = await standardRepository.GetAsync(config.StandardId);

        return MapToDto(config, standard.Name);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_SettingsLectureSlots.Add)]
    public async Task<LectureSlotDto> CreateAsync(CreateLectureSlotDto input)
    {
        var existing = await lectureSlotConfigRepository.FindByStandardIdAsync(input.StandardId);
        if (existing is not null)
        {
            throw new BusinessException(TimetableErrorCodes.LectureSlotConfigAlreadyExists);
        }

        var config = new LectureSlotConfig(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            input.StandardId,
            input.SchoolHours,
            input.PreviousStandardId);

        BuildSlotGroups(config, input.SlotConfigurations);

        await lectureSlotConfigRepository.InsertAsync(config);

        var standard = await standardRepository.GetAsync(config.StandardId);
        return MapToDto(config, standard.Name);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_SettingsLectureSlots.Add)]
    public async Task<LectureSlotDto> UpdateAsync(Guid id, UpdateLectureSlotDto input)
    {
        var config = await lectureSlotConfigRepository.GetAsync(id);

        // Block update if a timetable has already been created for this standard
        var existingTimetables = await timetableRepository.GetByStandardIdAsync(config.StandardId);
        if (existingTimetables.Count > 0)
        {
            throw new BusinessException(TimetableErrorCodes.LectureSlotConfigHasTimetable)
                .WithData("StandardId", config.StandardId);
        }

        config.SetSchoolHours(input.SchoolHours);
        config.SetPreviousStandardId(input.PreviousStandardId);
        config.ClearSlotGroups();
        BuildSlotGroups(config, input.SlotConfigurations);

        await lectureSlotConfigRepository.UpdateAsync(config);

        var standard = await standardRepository.GetAsync(config.StandardId);
        return MapToDto(config, standard.Name);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_SettingsLectureSlots.Add)]
    public async Task AssignAsync(Guid id, AssignLectureSlotDto input)
    {
        var source = await lectureSlotConfigRepository.GetAsync(id);

        foreach (var standardId in input.SelectedStandardIds)
        {
            var existing = await lectureSlotConfigRepository.FindByStandardIdAsync(standardId);
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
                        group.WeekRangeEnd);

                    foreach (var timeSlot in group.TimeSlots)
                    {
                        newGroup.AddTimeSlot(new LectureSlotTimeSlot(
                            GuidGenerator.Create(),
                            newGroup.Id,
                            timeSlot.StartTime,
                            timeSlot.EndTime,
                            timeSlot.SlotType,
                            timeSlot.BreakType));
                    }

                    existing.AddSlotGroup(newGroup);
                }

                await lectureSlotConfigRepository.UpdateAsync(existing);
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
                        group.WeekRangeEnd);

                    foreach (var timeSlot in group.TimeSlots)
                    {
                        newGroup.AddTimeSlot(new LectureSlotTimeSlot(
                            GuidGenerator.Create(),
                            newGroup.Id,
                            timeSlot.StartTime,
                            timeSlot.EndTime,
                            timeSlot.SlotType,
                            timeSlot.BreakType));
                    }

                    clone.AddSlotGroup(newGroup);
                }

                await lectureSlotConfigRepository.InsertAsync(clone);
            }
        }
    }

    private void BuildSlotGroups(LectureSlotConfig config, List<SlotConfigurationDto> configurations)
    {
        foreach (var slotConfig in configurations)
        {
            var group = new LectureSlotGroup(
                GuidGenerator.Create(),
                config.Id,
                slotConfig.TotalSlots,
                slotConfig.WeekRange.Count > 0 ? slotConfig.WeekRange[0] : 0,
                slotConfig.WeekRange.Count > 1 ? slotConfig.WeekRange[1] : 5);

            foreach (var ts in slotConfig.TimeSlots)
            {
                group.AddTimeSlot(new LectureSlotTimeSlot(
                    GuidGenerator.Create(),
                    group.Id,
                    ts.StartTime,
                    ts.EndTime,
                    ts.SlotType,
                    ts.BreakType));
            }

            config.AddSlotGroup(group);
        }
    }

    private static LectureSlotDto MapToDto(LectureSlotConfig config, string standardName) => new()
    {
        Id = config.Id,
        StandardId = config.StandardId,
        StandardName = standardName,
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
                TimeSlots = g.TimeSlots
                    .OrderBy(ts => ts.StartTime)
                    .Select(ts => new TimeSlotDetailDto
                    {
                        StartTime = ts.StartTime,
                        EndTime = ts.EndTime,
                        SlotType = ts.SlotType,
                        BreakType = ts.BreakType
                    }).ToList()
            }).ToList()
    };
}
