using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
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
    IRepository<Standard, Guid> standardRepository
) : ApplicationService, ILectureSlotAppService
{
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_SettingsLectureSlots.View)]
    public async Task<PagedResultDto<LectureSlotListDto>> GetByMediumAsync(GetLectureSlotsByMediumInput input)
    {
        var standardQueryable = await standardRepository.GetQueryableAsync();
        var standards = await AsyncExecuter.ToListAsync(
            standardQueryable.Where(s => s.EducationMediumStage.EducationMediumId == input.AcademicMediumId));

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            standards = standards.Where(s => s.Name.Contains(input.Search, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var totalCount = standards.Count;
        var pagedStandards = standards
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        var queryable = await lectureSlotConfigRepository.WithDetailsAsync();
        var configs = await AsyncExecuter.ToListAsync(
            queryable.Where(c => pagedStandards.Select(s => s.Id).Contains(c.StandardId)));

        var items = configs.Select(config =>
        {
            var standard = pagedStandards.First(s => s.Id == config.StandardId);
            return new LectureSlotListDto
            {
                Id = config.Id,
                StandardId = config.StandardId,
                StandardName = standard.Name,
                SlotConfigurations = config.SlotGroups.Select(g => new SlotConfigurationSummaryDto
                {
                    FromDay = g.WeekRangeStart,
                    ToDay = g.WeekRangeEnd,
                    TotalSlots = g.TotalSlots
                }).ToList()
            };
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
                throw new BusinessException(TimetableErrorCodes.LectureSlotAlreadyAssigned);
            }

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
                    TimeOnly.FromDateTime(ts.StartTime.DateTime),
                    TimeOnly.FromDateTime(ts.EndTime.DateTime),
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
        SlotConfigurations = config.SlotGroups.Select(g => new SlotConfigurationDetailDto
        {
            TotalSlots = g.TotalSlots,
            WeekRange = [g.WeekRangeStart, g.WeekRangeEnd],
            TimeSlots = g.TimeSlots.Select(ts => new TimeSlotDetailDto
            {
                StartTime = new DateTimeOffset(DateOnly.MinValue.ToDateTime(ts.StartTime), TimeSpan.Zero),
                EndTime = new DateTimeOffset(DateOnly.MinValue.ToDateTime(ts.EndTime), TimeSpan.Zero),
                SlotType = ts.SlotType,
                BreakType = ts.BreakType
            }).ToList()
        }).ToList()
    };
}
