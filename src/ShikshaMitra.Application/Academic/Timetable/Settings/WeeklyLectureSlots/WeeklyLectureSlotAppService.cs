using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Timetable.Settings.LectureSlots;
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
    IRepository<ElectiveGroup, Guid> electiveGroupRepository,
    ILectureSlotConfigRepository lectureSlotConfigRepository,
    IRepository<AssignedSubject, Guid> assignedSubjectRepository
) : ApplicationService, IWeeklyLectureSlotAppService
{
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_SettingsWeeklyLectures.View)]
    public async Task<PagedResultDto<WeeklyLectureSlotListDto>> GetListAsync(GetWeeklyLectureSlotsInput input)
    {
        var standardQueryable = await standardRepository.GetQueryableAsync();
        var standardNames = await AsyncExecuter.ToListAsync(standardQueryable
            .Where(s => s.EducationMediumStage.EducationMediumId == input.AcademicMediumId).Select(s => new { s.Id, s.Name }));
        if (!string.IsNullOrWhiteSpace(input.Search))
            standardNames = standardNames.Where(s => s.Name.Contains(input.Search, StringComparison.OrdinalIgnoreCase)).ToList();
        var standardsDict = standardNames.ToDictionary(s => s.Id, s => s.Name);
        var standardIds = standardsDict.Keys.ToArray();
        var queryable = (await weeklyLectureSlotRepository.GetQueryableAsync())
            .Where(slot => Enumerable.Contains(standardIds, slot.StandardId));
        var totalCount = await AsyncExecuter.CountAsync(queryable);
        var pageIds = await AsyncExecuter.ToListAsync(TimetableSettingPageOrder.ByStandardName(queryable, standardsDict,
            slot => slot.StandardId).ThenBy(slot => slot.Id).Skip(input.SkipCount).Take(input.MaxResultCount).Select(slot => slot.Id));
        var pageOrder = pageIds.Select((id, index) => new { id, index }).ToDictionary(item => item.id, item => item.index);
        var pageGraphs = await AsyncExecuter.ToListAsync((await weeklyLectureSlotRepository.WithDetailsAsync())
            .Where(slot => pageIds.Contains(slot.Id)));
        var pagedSlots = pageGraphs.OrderBy(slot => pageOrder[slot.Id]).ToList();
        var pageStandardIds = pagedSlots.Select(slot => slot.StandardId).Distinct().ToList();
        var capacityDict = await LoadAvailableCapacityAsync(pageStandardIds);
        var publishedScopes = await LoadPublishedSubjectAssignmentScopesAsync(pageStandardIds);
        var visibleSubjects = pagedSlots.SelectMany(slot => GetPublishedWeeklyLectureSubjects(slot, publishedScopes)).ToList();
        var groupIds = visibleSubjects.Where(subject => subject.ElectiveGroupId.HasValue)
            .Select(subject => subject.ElectiveGroupId!.Value).Distinct().ToArray();
        var electiveGroupQueryable = await electiveGroupRepository.WithDetailsAsync(eg => eg.Subjects);
        var electiveGroups = await AsyncExecuter.ToListAsync(electiveGroupQueryable.Where(group => Enumerable.Contains(groupIds, group.Id)));
        var subjectIds = visibleSubjects.Where(subject => subject.SubjectId.HasValue).Select(subject => subject.SubjectId!.Value)
            .Concat(electiveGroups.SelectMany(group => group.Subjects).Select(subject => subject.SubjectId)).Distinct().ToArray();
        var subjects = await AsyncExecuter.ToListAsync((await subjectRepository.GetQueryableAsync())
            .Where(subject => Enumerable.Contains(subjectIds, subject.Id)).Select(subject => new { subject.Id, subject.Name, subject.Color }));

        var items = pagedSlots.Select(slot =>
        {
            var publishedSlotSubjects = GetPublishedWeeklyLectureSubjects(slot, publishedScopes);
            var totalLectures = publishedSlotSubjects.Sum(subject => subject.WeeklyLectures);
            var hasCapacity = capacityDict.TryGetValue(slot.StandardId, out var availableCapacity);
            var difference = TimetableCapacityCalculator.GetCapacityDifference(availableCapacity, totalLectures);
            var status = TimetableCapacityCalculator.GetStatus(
                hasSlots: hasCapacity,
                hasWeeklyLectures: totalLectures > 0,
                requiredWeeklyLectures: totalLectures,
                availableWeeklyCapacity: availableCapacity);

            return new WeeklyLectureSlotListDto
            {
                Id = slot.Id,
                StandardId = slot.StandardId,
                StandardName = standardsDict.GetValueOrDefault(slot.StandardId, string.Empty),
                TotalLectures = totalLectures,
                AvailableTeachingCapacity = availableCapacity,
                Difference = difference,
                Status = status,
                Subjects = publishedSlotSubjects.Select(sub =>
                {
                    var eg = sub.ElectiveGroupId.HasValue
                        ? electiveGroups.FirstOrDefault(x => x.Id == sub.ElectiveGroupId.Value) : null;
                    return new WeeklyLectureSubjectSummaryDto
                    {
                        SubjectId = sub.SubjectId,
                        SubjectName = sub.SubjectId.HasValue
                            ? subjects.FirstOrDefault(s => s.Id == sub.SubjectId.Value)?.Name ?? string.Empty
                            : string.Empty,
                        ElectiveGroupId = sub.ElectiveGroupId,
                        ElectiveGroupName = eg?.Name,
                        ElectiveGroupColor = eg?.Color,
                        ElectiveGroupSubjects = eg?.Subjects.Select(c => new ElectiveGroupSubjectDto
                        {
                            Id = c.SubjectId,
                            Name = subjects.FirstOrDefault(s => s.Id == c.SubjectId)?.Name ?? string.Empty,
                            Color = subjects.FirstOrDefault(s => s.Id == c.SubjectId)?.Color
                        }).ToList() ?? [],
                        WeeklyLectures = sub.WeeklyLectures
                    };
                }).ToList()
            };
        }).ToList();

        return new PagedResultDto<WeeklyLectureSlotListDto>(totalCount, items);
    }

    private async Task<Dictionary<Guid, int>> LoadAvailableCapacityAsync(List<Guid> standardIds)
    {
        if (standardIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var queryable = await lectureSlotConfigRepository.WithDetailsAsync();
        var configs = await AsyncExecuter.ToListAsync(queryable.Where(c => standardIds.Contains(c.StandardId)));

        return configs.ToDictionary(
            c => c.StandardId,
            c => TimetableCapacityCalculator.GetWeeklyTeachingCapacity(
                c.SlotGroups.Select(g => (g.TimeSlots.Count(ts => ts.SlotType == SlotType.Period), g.ActiveDays.Count))));
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_SettingsWeeklyLectures.View)]
    public async Task<WeeklyLectureSlotDto> GetAsync(Guid id)
    {
        var slot = await weeklyLectureSlotRepository.GetAsync(id);
        var standard = await standardRepository.GetAsync(slot.StandardId);
        var subjects = await subjectRepository.GetListAsync();
        var electiveGroupQueryable = await electiveGroupRepository.WithDetailsAsync(eg => eg.Subjects);
        var electiveGroups = await AsyncExecuter.ToListAsync(electiveGroupQueryable);
        var capacityDict = await LoadAvailableCapacityAsync([slot.StandardId]);
        var publishedScopes = await LoadPublishedSubjectAssignmentScopesAsync([slot.StandardId]);
        var publishedSlotSubjects = GetPublishedWeeklyLectureSubjects(slot, publishedScopes);

        var totalLectures = publishedSlotSubjects.Sum(subject => subject.WeeklyLectures);
        var hasCapacity = capacityDict.TryGetValue(slot.StandardId, out var availableCapacity);
        var difference = TimetableCapacityCalculator.GetCapacityDifference(availableCapacity, totalLectures);
        var status = TimetableCapacityCalculator.GetStatus(
            hasSlots: hasCapacity,
            hasWeeklyLectures: totalLectures > 0,
            requiredWeeklyLectures: totalLectures,
            availableWeeklyCapacity: availableCapacity);

        return new WeeklyLectureSlotDto
        {
            Id = slot.Id,
            StandardId = slot.StandardId,
            StandardName = standard.Name,
            TotalLectures = totalLectures,
            AvailableTeachingCapacity = availableCapacity,
            Difference = difference,
            Status = status,
            Subjects = publishedSlotSubjects.Select(sub =>
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
                    ElectiveGroupColor = electiveGroup?.Color,
                    ElectiveGroupSubjects = electiveGroup?.Subjects.Select(c => new ElectiveGroupSubjectDto
                    {
                        Id = c.SubjectId,
                        Name = subjects.FirstOrDefault(s => s.Id == c.SubjectId)?.Name ?? string.Empty,
                        Color = subjects.FirstOrDefault(s => s.Id == c.SubjectId)?.Color
                    }).ToList() ?? [],
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

    private async Task<Dictionary<Guid, PublishedSubjectAssignmentScope>> LoadPublishedSubjectAssignmentScopesAsync(
        IEnumerable<Guid> standardIds)
    {
        var distinctStandardIds = standardIds.Distinct().ToList();
        if (distinctStandardIds.Count == 0)
        {
            return new Dictionary<Guid, PublishedSubjectAssignmentScope>();
        }

        var assignments = await assignedSubjectRepository.GetListAsync(a =>
            distinctStandardIds.Contains(a.StandardId) &&
            a.Status == SubjectStatus.Published);

        return distinctStandardIds.ToDictionary(
            standardId => standardId,
            standardId =>
            {
                var standardAssignments = assignments
                    .Where(a => a.StandardId == standardId)
                    .ToList();

                return new PublishedSubjectAssignmentScope(
                    standardAssignments
                        .Where(a => !a.ElectiveGroupId.HasValue)
                        .Select(a => a.SubjectId)
                        .ToHashSet(),
                    standardAssignments
                        .Where(a => a.ElectiveGroupId.HasValue)
                        .Select(a => a.ElectiveGroupId!.Value)
                        .ToHashSet());
            });
    }

    private static List<WeeklyLectureSlotSubject> GetPublishedWeeklyLectureSubjects(
        WeeklyLectureSlot slot,
        IReadOnlyDictionary<Guid, PublishedSubjectAssignmentScope> publishedScopes)
    {
        if (!publishedScopes.TryGetValue(slot.StandardId, out var scope))
        {
            return [];
        }

        return slot.Subjects
            .Where(subject =>
                (subject.SubjectId.HasValue && scope.SubjectIds.Contains(subject.SubjectId.Value)) ||
                (subject.ElectiveGroupId.HasValue && scope.ElectiveGroupIds.Contains(subject.ElectiveGroupId.Value)))
            .ToList();
    }

    private sealed record PublishedSubjectAssignmentScope(
        HashSet<Guid> SubjectIds,
        HashSet<Guid> ElectiveGroupIds);
}
