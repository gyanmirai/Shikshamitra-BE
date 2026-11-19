using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.Timetables;

namespace ShikshaMitra.Academic.Reports;

public partial class AcademicReportGenerator
{
    private async Task<AcademicReportPreviewDto?> TryBuildTimetablePreviewAsync(AcademicReportRequestDto request,
        AcademicReportPreviewDto payload)
    {
        var definition = await TryCreateTimetableExportAsync(request, CancellationToken.None, 256);
        if (definition == null) return null;
        payload.Columns = definition.Metadata.Columns;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        return request.ReportType switch
        {
            AcademicReportTypes.RoomUtilizationReport => await BuildRoomPreviewAsync(request, payload),
            AcademicReportTypes.TimetableChangeLog => await BuildTimetableAuditPreviewAsync(request, payload),
            AcademicReportTypes.SubjectwiseTimetableDistribution => await BuildTimetableDistributionPreviewAsync(request, payload),
            AcademicReportTypes.ClasswiseTimetable => await BuildClasswiseTimetablePreviewAsync(request, payload),
            _ => await BuildSortedTimetableSlotPreviewAsync(request, payload)
        };
    }

    private async Task<IQueryable<TimetableExportItem>> GetTimetablePreviewItemsAsync(AcademicReportRequestDto request)
    {
        var roots = await GetTimetableExportQueryAsync(request);
        var query = from timetable in roots
                    from slot in timetable.Slots
                    where slot.SlotType == SlotType.Period
                    from entry in slot.Subjects.DefaultIfEmpty()
                    select new TimetableExportItem
                    {
                        TimetableId = timetable.Id, StandardId = timetable.StandardId, DivisionId = timetable.DivisionId,
                        SlotId = slot.Id, Day = slot.Day, Sequence = slot.Sequence, StartTime = slot.StartTime, EndTime = slot.EndTime,
                        EntryId = entry == null ? Guid.Empty : entry.Id,
                        SubjectId = entry == null ? null : entry.SubjectId,
                        TeacherId = entry == null ? null : entry.TeacherId, RoomId = entry == null ? null : entry.RoomId
                    };
        if (request.ReportType == AcademicReportTypes.FreePeriodsReport) query = query.Where(item => !item.SubjectId.HasValue);
        if (request.ReportType == AcademicReportTypes.TeacherwiseTimetable)
        {
            query = query.Where(item => item.TeacherId.HasValue);
            if (request.Filters.TeacherIds is { Count: > 0 }) query = query.Where(item => request.Filters.TeacherIds.Contains(item.TeacherId!.Value));
        }
        return query;
    }

    private static int PreviewSkip(AcademicReportRequestDto request) => Math.Max(0, unchecked((request.Page - 1) * request.PageSize));

    private async Task<AcademicReportPreviewDto> BuildRoomPreviewAsync(AcademicReportRequestDto request, AcademicReportPreviewDto payload)
    {
        var roots = await GetTimetableExportQueryAsync(request);
        var slots = roots.SelectMany(timetable => timetable.Slots).Where(slot => slot.SlotType == SlotType.Period);
        var rooms = await roomRepository.GetQueryableAsync();
        if (request.Filters.RoomIds is { Count: > 0 }) rooms = rooms.Where(room => request.Filters.RoomIds.Contains(room.Id));
        var keys = await asyncExecuter.ToListAsync(rooms.Select(room => new { room.Id, room.RoomName }));
        payload.TotalCount = keys.Count;
        var ids = keys.OrderBy(room => room.RoomName).Skip(PreviewSkip(request)).Take(request.PageSize).Select(room => room.Id).ToList();
        if (ids.Count == 0) return payload;
        var total = await asyncExecuter.CountAsync(slots);
        var page = await asyncExecuter.ToListAsync(rooms.Where(room => ids.Contains(room.Id))
            .Select(room => new { room.Id, room.RoomName, room.RoomTypeId }).Take(request.PageSize));
        var typeIds = page.Select(room => room.RoomTypeId).Distinct().ToList();
        var types = await asyncExecuter.ToListAsync((await roomTypeRepository.GetQueryableAsync())
            .Where(type => typeIds.Contains(type.Id)).Select(type => new { type.Id, type.RoomTypeName }));
        var typeNames = types.ToDictionary(type => type.Id, type => type.RoomTypeName);
        var usage = await asyncExecuter.ToListAsync(slots.SelectMany(slot => slot.Subjects)
            .Where(entry => entry.RoomId.HasValue && ids.Contains(entry.RoomId.Value)).GroupBy(entry => entry.RoomId!.Value)
            .Select(group => new { Id = group.Key, Count = group.Count() }));
        var counts = usage.ToDictionary(room => room.Id, room => room.Count);
        payload.Rows = page.OrderBy(room => ids.IndexOf(room.Id)).Select(room => new Dictionary<string, string?>
        {
            ["RoomNumber"] = room.RoomName, ["RoomType"] = typeNames.GetValueOrDefault(room.RoomTypeId, "—"),
            ["TotalAvailablePeriods"] = total.ToString(), ["UtilizedPeriods"] = counts.GetValueOrDefault(room.Id).ToString(),
            ["FreePeriods"] = (total - counts.GetValueOrDefault(room.Id)).ToString()
        }).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildTimetableAuditPreviewAsync(AcademicReportRequestDto request, AcademicReportPreviewDto payload)
    {
        var query = (await GetTimetableExportQueryAsync(request)).Where(timetable => timetable.LastModificationTime != null);
        payload.TotalCount = await asyncExecuter.CountAsync(query);
        var items = await asyncExecuter.ToListAsync(query.OrderByDescending(timetable => timetable.LastModificationTime)
            .Skip(PreviewSkip(request)).Take(request.PageSize).Select(timetable => new TimetableExportItem
            {
                TimetableId = timetable.Id, StandardId = timetable.StandardId, DivisionId = timetable.DivisionId,
                ModifiedAt = timetable.LastModificationTime, TeacherId = timetable.LastModifierId
            }));
        if (items.Count == 0) return payload;
        await PopulateTimetableExportNamesAsync(items, CancellationToken.None);
        payload.Rows = items.Select(item => new Dictionary<string, string?>
        {
            ["ChangeDate"] = item.ModifiedAt!.Value.ToString("dd MMM yyyy, HH:mm"), ["ChangedBy"] = item.TeacherName,
            ["Class"] = item.StandardName ?? "—", ["Division"] = item.DivisionName ?? "—", ["Subject"] = string.Empty,
            ["Teacher"] = string.Empty, ["PreviousSchedule"] = string.Empty, ["NewSchedule"] = string.Empty, ["ChangeReason"] = string.Empty
        }).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildClasswiseTimetablePreviewAsync(AcademicReportRequestDto request, AcademicReportPreviewDto payload)
    {
        var roots = await GetTimetableExportQueryAsync(request);
        var metadata = await asyncExecuter.ToListAsync(roots.OrderBy(timetable => timetable.Id).Select(timetable => new
        {
            timetable.Id, timetable.StandardId, timetable.DivisionId,
            Count = timetable.Slots.Where(slot => slot.SlotType == SlotType.Period)
                .Sum(slot => slot.Subjects.Count == 0 ? 1 : slot.Subjects.Count)
        }));
        payload.TotalCount = metadata.Sum(item => item.Count);
        var names = metadata.Select(item => new TimetableExportItem
        { TimetableId = item.Id, StandardId = item.StandardId, DivisionId = item.DivisionId }).ToList();
        await PopulateTimetablePreviewPlacementNamesAsync(names);
        var countMap = metadata.ToDictionary(item => item.Id, item => item.Count);
        var ordered = names.OrderBy(item => item.StandardName ?? "").ThenBy(item => item.DivisionName ?? "");
        var skip = PreviewSkip(request);
        var remaining = request.PageSize;
        var slices = new List<(Guid Id, int Skip, int Take, int Count)>();
        foreach (var key in ordered)
        {
            var count = countMap[key.TimetableId];
            if (skip >= count) { skip -= count; continue; }
            var take = Math.Min(remaining, count - skip);
            slices.Add((key.TimetableId, skip, take, count));
            remaining -= take; skip = 0;
            if (remaining == 0) break;
        }
        if (slices.Count == 0) return payload;
        var ids = slices.Select(slice => slice.Id).ToList();
        var source = (await GetTimetablePreviewItemsAsync(request)).Where(item => ids.Contains(item.TimetableId));
        var fullIds = slices.Where(slice => slice.Skip == 0 && slice.Take == slice.Count).Select(slice => slice.Id).ToList();
        IQueryable<TimetableExportItem>? page = fullIds.Count == 0 ? null
            : source.Where(item => fullIds.Contains(item.TimetableId)).Take(request.PageSize);
        foreach (var slice in slices.Where(slice => slice.Skip != 0 || slice.Take != slice.Count))
        {
            var part = source.Where(item => item.TimetableId == slice.Id).OrderBy(item => item.Day).ThenBy(item => item.Sequence)
                .ThenBy(item => item.SlotId).ThenBy(item => item.EntryId).Skip(slice.Skip).Take(slice.Take);
            page = page == null ? part : page.Concat(part);
        }
        var items = await asyncExecuter.ToListAsync(page!);
        await PopulateTimetableExportNamesAsync(items, CancellationToken.None);
        payload.Rows = items.OrderBy(item => ids.IndexOf(item.TimetableId)).ThenBy(item => item.Day).ThenBy(item => item.Sequence)
            .ThenBy(item => item.SlotId).ThenBy(item => item.EntryId).Select(item => FormatTimetablePreviewSlot(item, request.ReportType)).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildSortedTimetableSlotPreviewAsync(AcademicReportRequestDto request, AcademicReportPreviewDto payload)
    {
        var query = await GetTimetablePreviewItemsAsync(request);
        payload.TotalCount = await asyncExecuter.CountAsync(query);
        if (PreviewSkip(request) >= payload.TotalCount) return payload;
        var isTeacher = request.ReportType == AcademicReportTypes.TeacherwiseTimetable;
        // Only fields used by the existing .NET string sort are read before choosing detail IDs.
        var keys = await asyncExecuter.ToListAsync(query.OrderBy(item => item.TimetableId).ThenBy(item => item.Day)
            .ThenBy(item => item.Sequence).ThenBy(item => item.SlotId).ThenBy(item => item.EntryId).Select(item => new TimetableExportItem
            {
                TimetableId = item.TimetableId, SlotId = item.SlotId, EntryId = item.EntryId, Day = item.Day, Sequence = item.Sequence,
                TeacherId = isTeacher ? item.TeacherId : null, StandardId = item.StandardId, DivisionId = item.DivisionId,
                StartTime = isTeacher ? default : item.StartTime, EndTime = isTeacher ? default : item.EndTime
            }));
        if (isTeacher)
        {
            var teacherIds = keys.Select(item => item.TeacherId!.Value).Distinct().ToList();
            var teachers = await asyncExecuter.ToListAsync((await employeeRepository.GetQueryableAsync())
                .Where(teacher => teacherIds.Contains(teacher.Id)).Select(teacher => new { teacher.Id, teacher.FirstName, teacher.LastName }));
            var names = teachers.ToDictionary(teacher => teacher.Id,
                teacher => string.Join(" ", new[] { teacher.FirstName, teacher.LastName }.Where(name => !string.IsNullOrWhiteSpace(name))));
            foreach (var key in keys) key.TeacherName = names.GetValueOrDefault(key.TeacherId!.Value, "—");
        }
        else await PopulateTimetablePreviewPlacementNamesAsync(keys);
        var selected = keys.OrderBy(item => isTeacher ? item.TeacherName : $"{item.StandardName ?? "—"} {item.DivisionName ?? ""}".Trim())
            .ThenBy(item => item.Day.ToString()).ThenBy(item => isTeacher ? $"Period {item.Sequence}"
                : $"Period {item.Sequence} ({item.StartTime:HH:mm}–{item.EndTime:HH:mm})")
            .Skip(PreviewSkip(request)).Take(request.PageSize).ToList();
        List<TimetableExportItem> items;
        if (isTeacher)
        {
            var entryIds = selected.Select(item => item.EntryId).ToList();
            items = await asyncExecuter.ToListAsync(query.Where(item => entryIds.Contains(item.EntryId)).Take(request.PageSize));
            items = items.OrderBy(item => entryIds.IndexOf(item.EntryId)).ToList();
        }
        else
        {
            var slotIds = selected.Select(item => item.SlotId).ToList();
            items = await asyncExecuter.ToListAsync(query.Where(item => slotIds.Contains(item.SlotId)).Take(request.PageSize));
            items = items.OrderBy(item => slotIds.IndexOf(item.SlotId)).ToList();
        }
        await PopulateTimetableExportNamesAsync(items, CancellationToken.None);
        payload.Rows = items.Select(item => FormatTimetablePreviewSlot(item, request.ReportType)).ToList();
        return payload;
    }

    private async Task PopulateTimetablePreviewPlacementNamesAsync(List<TimetableExportItem> items)
    {
        var standardIds = items.Select(item => item.StandardId).Distinct().ToList();
        var divisionIds = items.Select(item => item.DivisionId).Distinct().ToList();
        var standards = await asyncExecuter.ToListAsync((await standardRepository.GetQueryableAsync())
            .Where(standard => standardIds.Contains(standard.Id)).Select(standard => new { standard.Id, standard.Name }));
        var divisions = await asyncExecuter.ToListAsync((await divisionRepository.GetQueryableAsync())
            .Where(division => divisionIds.Contains(division.Id)).Select(division => new { division.Id, division.Name }));
        var standardNames = standards.ToDictionary(standard => standard.Id, standard => standard.Name);
        var divisionNames = divisions.ToDictionary(division => division.Id, division => division.Name);
        foreach (var item in items)
        {
            item.StandardName = standardNames.GetValueOrDefault(item.StandardId);
            item.DivisionName = divisionNames.GetValueOrDefault(item.DivisionId);
        }
    }

    private static Dictionary<string, string?> FormatTimetablePreviewSlot(TimetableExportItem item, string reportType)
    {
        if (reportType == AcademicReportTypes.FreePeriodsReport) return new()
        {
            ["Day"] = item.Day.ToString(), ["Period"] = $"Period {item.Sequence} ({item.StartTime:HH:mm}–{item.EndTime:HH:mm})",
            ["Class"] = $"{item.StandardName ?? "—"} {item.DivisionName ?? ""}".Trim(),
            ["Teacher"] = "—", ["Room"] = "—", ["Reason"] = string.Empty, ["Status"] = "Free"
        };
        var values = new Dictionary<string, string?>
        {
            ["Day"] = item.Day.ToString(), ["Period"] = $"Period {item.Sequence}",
            ["StartTime"] = item.StartTime.ToString("HH:mm"), ["EndTime"] = item.EndTime.ToString("HH:mm"),
            ["Subject"] = item.SubjectName, ["Room"] = item.RoomName
        };
        if (reportType == AcademicReportTypes.TeacherwiseTimetable)
        {
            values["TeacherName"] = item.TeacherName; values["Class"] = item.StandardName ?? "—"; values["Division"] = item.DivisionName ?? "—";
        }
        else
        {
            values["Teacher"] = item.TeacherName; values["Status"] = item.SubjectId.HasValue ? "Scheduled" : "Free";
        }
        return values;
    }

    private async Task<AcademicReportPreviewDto> BuildTimetableDistributionPreviewAsync(AcademicReportRequestDto request, AcademicReportPreviewDto payload)
    {
        var source = (await GetTimetablePreviewItemsAsync(request)).Where(item => item.SubjectId.HasValue);
        var grouped = await asyncExecuter.ToListAsync(source.GroupBy(item => new { item.StandardId, item.DivisionId, item.SubjectId })
            .Select(group => new
            {
                group.Key.StandardId, group.Key.DivisionId, group.Key.SubjectId, Count = group.Count(),
                First = group.OrderBy(item => item.TimetableId).ThenBy(item => item.SlotId).ThenBy(item => item.EntryId)
                    .Select(item => new { item.TimetableId, item.SlotId, item.EntryId }).First()
            }));
        var keys = grouped.Select(group => new TimetableExportItem
        {
            StandardId = group.StandardId, DivisionId = group.DivisionId, SubjectId = group.SubjectId,
            TimetableId = group.First.TimetableId, SlotId = group.First.SlotId, EntryId = group.First.EntryId
        }).ToList();
        await PopulateTimetableExportNamesAsync(keys, CancellationToken.None);
        var counts = grouped.ToDictionary(group => (group.StandardId, group.DivisionId, group.SubjectId), group => group.Count);
        IEnumerable<TimetableExportItem> filtered = keys;
        if (request.Filters.SubjectIds is { Count: > 0 })
        {
            var subjects = await asyncExecuter.ToListAsync((await subjectRepository.GetQueryableAsync())
                .Where(subject => request.Filters.SubjectIds.Contains(subject.Id)).Select(subject => new { subject.Id, subject.Name }));
            var names = subjects.ToDictionary(subject => subject.Id, subject => subject.Name);
            var requestedNames = request.Filters.SubjectIds.Select(id => names.GetValueOrDefault(id, "")).ToHashSet();
            filtered = filtered.Where(item => requestedNames.Contains(item.SubjectName));
        }
        var ordered = filtered.OrderBy(item => item.SubjectName).ThenBy(item => item.StandardName ?? "—").ThenBy(item => item.DivisionName ?? "—")
            .ThenBy(item => item.TimetableId).ThenBy(item => item.SlotId).ThenBy(item => item.EntryId).ToList();
        payload.TotalCount = ordered.Count;
        var selected = ordered.Skip(PreviewSkip(request)).Take(request.PageSize).ToList();
        if (selected.Count == 0) return payload;
        var parameter = Expression.Parameter(typeof(TimetableExportItem), "item");
        var predicate = Expression.Lambda<Func<TimetableExportItem, bool>>(BuildTimetableGroupPredicate(selected, parameter, 0, selected.Count), parameter);
        var teacherRows = await asyncExecuter.ToListAsync(source.Where(predicate).Where(item => item.TeacherId.HasValue)
            .Select(item => new { item.StandardId, item.DivisionId, item.SubjectId, item.TeacherId }).Distinct());
        var teacherIds = teacherRows.Select(row => row.TeacherId!.Value).Distinct().ToList();
        var teachers = await asyncExecuter.ToListAsync((await employeeRepository.GetQueryableAsync())
            .Where(teacher => teacherIds.Contains(teacher.Id)).Select(teacher => new { teacher.Id, teacher.FirstName, teacher.LastName }));
        var teacherNames = teachers.ToDictionary(teacher => teacher.Id,
            teacher => string.Join(" ", new[] { teacher.FirstName, teacher.LastName }.Where(name => !string.IsNullOrWhiteSpace(name))));
        var namesByGroup = teacherRows.GroupBy(row => (row.StandardId, row.DivisionId, row.SubjectId)).ToDictionary(group => group.Key,
            group => string.Join(", ", group.Select(row => teacherNames.GetValueOrDefault(row.TeacherId!.Value, "—")).Distinct().OrderBy(name => name)));
        foreach (var item in selected)
        {
            var key = (item.StandardId, item.DivisionId, item.SubjectId);
            var names = namesByGroup.GetValueOrDefault(key, string.Empty);
            payload.Rows.Add(new()
            {
                ["Subject"] = item.SubjectName, ["Class"] = item.StandardName ?? "—", ["Division"] = item.DivisionName ?? "—",
                ["TotalPeriodsAssigned"] = counts[key].ToString(), ["WeeklyAllocation"] = counts[key].ToString(),
                ["AssignedTeachers"] = names.Length == 0 ? "—" : names
            });
        }
        return payload;
    }

    private static Expression BuildTimetableGroupPredicate(List<TimetableExportItem> groups, ParameterExpression item, int start, int count)
    {
        if (count > 1)
        {
            var left = count / 2;
            return Expression.OrElse(BuildTimetableGroupPredicate(groups, item, start, left), BuildTimetableGroupPredicate(groups, item, start + left, count - left));
        }
        var key = groups[start];
        return Expression.AndAlso(Expression.AndAlso(
            Expression.Equal(Expression.Property(item, nameof(TimetableExportItem.StandardId)), Expression.Constant(key.StandardId)),
            Expression.Equal(Expression.Property(item, nameof(TimetableExportItem.DivisionId)), Expression.Constant(key.DivisionId))),
            Expression.Equal(Expression.Property(item, nameof(TimetableExportItem.SubjectId)), Expression.Constant(key.SubjectId, typeof(Guid?))));
    }
}
