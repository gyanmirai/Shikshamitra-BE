using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;

[RemoteService(IsEnabled = false)]
[Authorize]
public class WeeklyLectureSlotAppService(
    IWeeklyLectureSlotRepository weeklyLectureSlotRepository,
    IRepository<Standard, Guid> standardRepository,
    IRepository<Subject, Guid> subjectRepository,
    IRepository<ElectiveGroup, Guid> electiveGroupRepository
) : ApplicationService, IWeeklyLectureSlotAppService
{
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_SettingsWeeklyLectures.View)]
    public async Task<PagedResultDto<WeeklyLectureSlotListDto>> GetListAsync(GetWeeklyLectureSlotsInput input)
    {
        // Load all standards under the requested medium for name lookup
        var standardQueryable = await standardRepository.GetQueryableAsync();
        var allStandards = await AsyncExecuter.ToListAsync(
            standardQueryable.Where(s => s.EducationMediumStage.EducationMediumId == input.AcademicMediumId));

        var standardIds = allStandards.Select(s => s.Id).ToList();

        // Query only slots that exist for those standards
        var queryable = await weeklyLectureSlotRepository.WithDetailsAsync();
        var allSlots = await AsyncExecuter.ToListAsync(
            queryable.Where(w => standardIds.Contains(w.StandardId)));

        // Apply optional search filter on standard name
        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var matchingStandardIds = allStandards
                .Where(s => s.Name.Contains(input.Search, StringComparison.OrdinalIgnoreCase))
                .Select(s => s.Id)
                .ToHashSet();
            allSlots = allSlots.Where(w => matchingStandardIds.Contains(w.StandardId)).ToList();
        }

        var totalCount = allSlots.Count;
        var standardsDict = allStandards.ToDictionary(s => s.Id, s => s.Name);

        var pagedSlots = allSlots
            .OrderBy(w => standardsDict.GetValueOrDefault(w.StandardId, string.Empty))
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        var subjects = await subjectRepository.GetListAsync();
        var electiveGroups = await electiveGroupRepository.GetListAsync();

        var items = pagedSlots.Select(slot => new WeeklyLectureSlotListDto
        {
            Id = slot.Id,
            StandardId = slot.StandardId,
            StandardName = standardsDict.GetValueOrDefault(slot.StandardId, string.Empty),
            TotalLectures = slot.GetTotalLectures(),
            Subjects = slot.Subjects.Select(sub => new WeeklyLectureSubjectSummaryDto
            {
                SubjectId = sub.SubjectId,
                SubjectName = sub.SubjectId.HasValue
                    ? subjects.FirstOrDefault(s => s.Id == sub.SubjectId.Value)?.Name ?? string.Empty
                    : string.Empty,
                ElectiveGroupId = sub.ElectiveGroupId,
                ElectiveGroupName = sub.ElectiveGroupId.HasValue
                    ? electiveGroups.FirstOrDefault(eg => eg.Id == sub.ElectiveGroupId.Value)?.Name
                    : null,
                WeeklyLectures = sub.WeeklyLectures
            }).ToList()
        }).ToList();

        return new PagedResultDto<WeeklyLectureSlotListDto>(totalCount, items);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_SettingsWeeklyLectures.View)]
    public async Task<WeeklyLectureSlotDto> GetAsync(Guid id)
    {
        var slot = await weeklyLectureSlotRepository.GetAsync(id);
        var standard = await standardRepository.GetAsync(slot.StandardId);
        var subjects = await subjectRepository.GetListAsync();
        var electiveGroups = await electiveGroupRepository.GetListAsync();

        return new WeeklyLectureSlotDto
        {
            Id = slot.Id,
            StandardId = slot.StandardId,
            StandardName = standard.Name,
            TotalLectures = slot.GetTotalLectures(),
            Subjects = slot.Subjects.Select(sub =>
            {
                var subject = sub.SubjectId.HasValue
                    ? subjects.FirstOrDefault(s => s.Id == sub.SubjectId.Value) : null;
                var electiveGroup = sub.ElectiveGroupId.HasValue
                    ? electiveGroups.FirstOrDefault(eg => eg.Id == sub.ElectiveGroupId.Value) : null;
                return new WeeklyLectureSubjectDetailDto
                {
                    SubjectId = sub.SubjectId,
                    SubjectName = subject?.Name ?? string.Empty,
                    SubjectCode = null,
                    SubjectColor = subject?.Color,
                    ElectiveGroupId = sub.ElectiveGroupId,
                    ElectiveGroupName = electiveGroup?.Name,
                    WeeklyLectures = sub.WeeklyLectures
                };
            }).ToList()
        };
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_SettingsWeeklyLectures.Add)]
    public async Task<WeeklyLectureSlotDto> CreateAsync(Guid academicMediumId, CreateWeeklyLectureSlotDto input)
    {
        var existing = await weeklyLectureSlotRepository.FindByStandardIdAsync(input.StandardId);
        if (existing is not null)
        {
            throw new BusinessException(TimetableErrorCodes.WeeklyLectureSlotAlreadyExists);
        }

        var slot = new WeeklyLectureSlot(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            input.StandardId);

        foreach (var subject in input.Subjects)
        {
            slot.AddSubject(
                GuidGenerator.Create(),
                subject.SubjectId,
                subject.ElectiveGroupId,
                subject.WeeklyLectures);
        }

        await weeklyLectureSlotRepository.InsertAsync(slot, autoSave: true);

        return await GetAsync(slot.Id);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_SettingsWeeklyLectures.Add)]
    public async Task<WeeklyLectureSlotDto> UpdateAsync(Guid id, UpdateWeeklyLectureSlotDto input)
    {
        var slot = await weeklyLectureSlotRepository.GetAsync(id);

        slot.ReplaceSubjects(input.Subjects.Select(s =>
            (GuidGenerator.Create(), s.SubjectId, s.ElectiveGroupId, s.WeeklyLectures)));

        await weeklyLectureSlotRepository.UpdateAsync(slot);

        return await GetAsync(slot.Id);
    }
}
