using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.Timetables;

namespace ShikshaMitra.Academic.Reports;

public partial class AcademicReportGenerator
{
    private Task<AcademicReportExportRows?> TryCreateTimetableExportAsync(
        AcademicReportRequestDto request, CancellationToken cancellationToken, int batchSize)
    {
        (string Id, string Label)[]? columns = request.ReportType switch
        {
            AcademicReportTypes.ClasswiseTimetable => [("Day", "Day"), ("Period", "Period"), ("StartTime", "Start Time"), ("EndTime", "End Time"), ("Subject", "Subject"), ("Teacher", "Teacher"), ("Room", "Room"), ("Status", "Status")],
            AcademicReportTypes.TeacherwiseTimetable => [("TeacherName", "Teacher Name"), ("Day", "Day"), ("Period", "Period"), ("Subject", "Subject"), ("Class", "Class"), ("Division", "Division"), ("Room", "Room"), ("StartTime", "Start Time"), ("EndTime", "End Time")],
            AcademicReportTypes.RoomUtilizationReport => [("RoomNumber", "Room Number"), ("RoomType", "Room Type"), ("TotalAvailablePeriods", "Total Available Periods"), ("UtilizedPeriods", "Utilized Periods"), ("FreePeriods", "Free Periods")],
            AcademicReportTypes.SubjectwiseTimetableDistribution => [("Subject", "Subject"), ("Class", "Class"), ("Division", "Division"), ("TotalPeriodsAssigned", "Total Periods Assigned"), ("WeeklyAllocation", "Weekly Allocation"), ("AssignedTeachers", "Assigned Teachers")],
            AcademicReportTypes.FreePeriodsReport => [("Day", "Day"), ("Period", "Period"), ("Class", "Class"), ("Teacher", "Teacher"), ("Room", "Room"), ("Reason", "Reason"), ("Status", "Status")],
            AcademicReportTypes.TimetableChangeLog => [("ChangeDate", "Change Date"), ("ChangedBy", "Changed By"), ("Class", "Class"), ("Division", "Division"), ("Subject", "Subject"), ("Teacher", "Teacher"), ("PreviousSchedule", "Previous Schedule"), ("NewSchedule", "New Schedule"), ("ChangeReason", "Change Reason")],
            _ => null
        };
        if (columns == null) return Task.FromResult<AcademicReportExportRows?>(null);
        var metadata = new AcademicReportPreviewDto
        {
            ReportType = request.ReportType, GeneratedAtUtc = DateTime.UtcNow,
            FilterSummary = BuildFilterSummary(request.Filters), NormalizedFilters = request.Filters,
            Columns = columns.Select(column => new AcademicReportColumnDto { Id = column.Id, Label = column.Label }).ToList(),
            Page = 1
        };
        return Task.FromResult<AcademicReportExportRows?>(new AcademicReportExportRows(metadata,
            ReadTimetableExportRowsAsync(request, batchSize, cancellationToken)));
    }

    private async IAsyncEnumerable<Dictionary<string, string?>> ReadTimetableExportRowsAsync(
        AcademicReportRequestDto request, int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var source = request.ReportType switch
        {
            AcademicReportTypes.RoomUtilizationReport => BuildRoomExportRowsAsync(request, batchSize, cancellationToken),
            AcademicReportTypes.TimetableChangeLog => BuildTimetableAuditExportRowsAsync(request, batchSize, cancellationToken),
            AcademicReportTypes.SubjectwiseTimetableDistribution => BuildTimetableDistributionExportRowsAsync(request, batchSize, cancellationToken),
            _ => BuildTimetableSlotExportRowsAsync(request, batchSize, cancellationToken)
        };
        await foreach (var row in AcademicReportExternalSort.SortAsync(source,
            (left, right) => CompareTimetableExportRows(left, right, request.ReportType), cancellationToken: cancellationToken))
            yield return row.Values;
    }

    private async Task<IQueryable<ShikshaMitra.Academic.Timetable.Timetables.Timetable>> GetTimetableExportQueryAsync(AcademicReportRequestDto request)
    {
        // Always start at the tenant-filtered aggregate; slot entities do not carry TenantId.
        var query = await timetableRepository.GetQueryableAsync();
        if (request.ReportType is not (AcademicReportTypes.TeacherwiseTimetable or AcademicReportTypes.RoomUtilizationReport))
        {
            if (request.Filters.ClassIds is { Count: > 0 }) query = query.Where(t => request.Filters.ClassIds.Contains(t.StandardId));
            if (request.Filters.DivisionIds is { Count: > 0 }) query = query.Where(t => request.Filters.DivisionIds.Contains(t.DivisionId));
        }
        return query;
    }

    private async IAsyncEnumerable<TimetableExportItem> ReadTimetableExportItemsAsync(
        AcademicReportRequestDto request, int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var roots = await GetTimetableExportQueryAsync(request);
        var query = from timetable in roots
                    from slot in timetable.Slots
                    where slot.SlotType == SlotType.Period
                    from subject in slot.Subjects.DefaultIfEmpty()
                    select new TimetableExportItem
                    {
                        TimetableId = timetable.Id, StandardId = timetable.StandardId, DivisionId = timetable.DivisionId,
                        SlotId = slot.Id, Day = slot.Day, Sequence = slot.Sequence, StartTime = slot.StartTime, EndTime = slot.EndTime,
                        EntryId = subject == null ? Guid.Empty : subject.Id,
                        SubjectId = subject == null ? null : subject.SubjectId,
                        TeacherId = subject == null ? null : subject.TeacherId,
                        RoomId = subject == null ? null : subject.RoomId
                    };
        if (request.ReportType == AcademicReportTypes.FreePeriodsReport) query = query.Where(item => !item.SubjectId.HasValue);
        if (request.ReportType == AcademicReportTypes.TeacherwiseTimetable)
        {
            query = query.Where(item => item.TeacherId.HasValue);
            if (request.Filters.TeacherIds is { Count: > 0 }) query = query.Where(item => request.Filters.TeacherIds.Contains(item.TeacherId!.Value));
        }
        if (request.ReportType == AcademicReportTypes.SubjectwiseTimetableDistribution) query = query.Where(item => item.SubjectId.HasValue);

        TimetableExportItem? cursor = null;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pageQuery = query;
            if (cursor != null)
            {
                var timetableId = cursor.TimetableId;
                var slotId = cursor.SlotId;
                var entryId = cursor.EntryId;
                pageQuery = pageQuery.Where(item => item.TimetableId.CompareTo(timetableId) > 0 ||
                    (item.TimetableId == timetableId && (item.SlotId.CompareTo(slotId) > 0 ||
                    (item.SlotId == slotId && item.EntryId.CompareTo(entryId) > 0))));
            }
            var items = await asyncExecuter.ToListAsync(pageQuery.OrderBy(item => item.TimetableId)
                .ThenBy(item => item.SlotId).ThenBy(item => item.EntryId).Take(batchSize), cancellationToken);
            if (items.Count == 0) yield break;
            await PopulateTimetableExportNamesAsync(items, cancellationToken);
            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return item;
            }
            cursor = items[^1];
        }
    }

    private async Task PopulateTimetableExportNamesAsync(List<TimetableExportItem> items, CancellationToken cancellationToken)
    {
        var standardIds = items.Select(item => item.StandardId).Distinct().ToList();
        var divisionIds = items.Select(item => item.DivisionId).Distinct().ToList();
        var subjectIds = items.Where(item => item.SubjectId.HasValue).Select(item => item.SubjectId!.Value).Distinct().ToList();
        var teacherIds = items.Where(item => item.TeacherId.HasValue).Select(item => item.TeacherId!.Value).Distinct().ToList();
        var roomIds = items.Where(item => item.RoomId.HasValue).Select(item => item.RoomId!.Value).Distinct().ToList();
        var standards = await asyncExecuter.ToListAsync((await standardRepository.GetQueryableAsync())
            .Where(item => standardIds.Contains(item.Id)).Select(item => new { item.Id, item.Name }), cancellationToken);
        var divisions = await asyncExecuter.ToListAsync((await divisionRepository.GetQueryableAsync())
            .Where(item => divisionIds.Contains(item.Id)).Select(item => new { item.Id, item.Name }), cancellationToken);
        var subjects = await asyncExecuter.ToListAsync((await subjectRepository.GetQueryableAsync())
            .Where(item => subjectIds.Contains(item.Id)).Select(item => new { item.Id, item.Name }), cancellationToken);
        var teachers = await asyncExecuter.ToListAsync((await employeeRepository.GetQueryableAsync())
            .Where(item => teacherIds.Contains(item.Id)).Select(item => new { item.Id, item.FirstName, item.LastName }), cancellationToken);
        var rooms = await asyncExecuter.ToListAsync((await roomRepository.GetQueryableAsync())
            .Where(item => roomIds.Contains(item.Id)).Select(item => new { item.Id, item.RoomName }), cancellationToken);
        var standardNames = standards.ToDictionary(item => item.Id, item => item.Name);
        var divisionNames = divisions.ToDictionary(item => item.Id, item => item.Name);
        var subjectNames = subjects.ToDictionary(item => item.Id, item => item.Name);
        var teacherNames = teachers.ToDictionary(item => item.Id,
            item => string.Join(" ", new[] { item.FirstName, item.LastName }.Where(part => !string.IsNullOrWhiteSpace(part))));
        var roomNames = rooms.ToDictionary(item => item.Id, item => item.RoomName);
        foreach (var item in items)
        {
            item.StandardName = standardNames.GetValueOrDefault(item.StandardId);
            item.DivisionName = divisionNames.GetValueOrDefault(item.DivisionId);
            item.SubjectName = item.SubjectId.HasValue ? subjectNames.GetValueOrDefault(item.SubjectId.Value, "—") : "—";
            item.TeacherName = item.TeacherId.HasValue ? teacherNames.GetValueOrDefault(item.TeacherId.Value, "—") : "—";
            item.RoomName = item.RoomId.HasValue ? roomNames.GetValueOrDefault(item.RoomId.Value, "—") : "—";
        }
    }

    private async IAsyncEnumerable<TimetableExportRow> BuildTimetableSlotExportRowsAsync(
        AcademicReportRequestDto request, int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in ReadTimetableExportItemsAsync(request, batchSize, cancellationToken))
        {
            var values = new Dictionary<string, string?> { ["Day"] = item.Day.ToString(), ["Period"] = $"Period {item.Sequence}" };
            if (request.ReportType == AcademicReportTypes.FreePeriodsReport)
            {
                values["Period"] = $"Period {item.Sequence} ({item.StartTime:HH:mm}–{item.EndTime:HH:mm})";
                values["Class"] = $"{item.StandardName ?? "—"} {item.DivisionName ?? ""}".Trim();
                values["Teacher"] = "—"; values["Room"] = "—"; values["Reason"] = string.Empty; values["Status"] = "Free";
            }
            else
            {
                values["StartTime"] = item.StartTime.ToString("HH:mm"); values["EndTime"] = item.EndTime.ToString("HH:mm");
                values["Subject"] = item.SubjectName; values["Room"] = item.RoomName;
                if (request.ReportType == AcademicReportTypes.TeacherwiseTimetable)
                {
                    values["TeacherName"] = item.TeacherName; values["Class"] = item.StandardName ?? "—";
                    values["Division"] = item.DivisionName ?? "—";
                }
                else
                {
                    values["Teacher"] = item.TeacherName; values["Status"] = item.SubjectId.HasValue ? "Scheduled" : "Free";
                }
            }
            yield return new TimetableExportRow(item, values);
        }
    }

    private async IAsyncEnumerable<TimetableExportRow> BuildTimetableDistributionExportRowsAsync(
        AcademicReportRequestDto request, int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        HashSet<string>? requestedNames = null;
        if (request.Filters.SubjectIds is { Count: > 0 })
        {
            var subjects = await asyncExecuter.ToListAsync((await subjectRepository.GetQueryableAsync())
                .Where(subject => request.Filters.SubjectIds.Contains(subject.Id)).Select(subject => new { subject.Id, subject.Name }), cancellationToken);
            var names = subjects.ToDictionary(subject => subject.Id, subject => subject.Name);
            requestedNames = request.Filters.SubjectIds.Select(id => names.GetValueOrDefault(id, "")).ToHashSet();
        }
        TimetableExportItem? first = null;
        var count = 0;
        var teachers = new HashSet<string>();
        await foreach (var item in AcademicReportExternalSort.SortAsync(ReadTimetableExportItemsAsync(request, batchSize, cancellationToken),
            (left, right) => CompareTimetableDistributionKey(left, right), cancellationToken: cancellationToken))
        {
            if (requestedNames != null && !requestedNames.Contains(item.SubjectName)) continue;
            if (first != null && (first.StandardId != item.StandardId || first.DivisionId != item.DivisionId || first.SubjectId != item.SubjectId))
            {
                yield return BuildTimetableDistributionRow(first, count, teachers);
                first = null; count = 0; teachers.Clear();
            }
            first ??= item;
            count++;
            if (item.TeacherId.HasValue) teachers.Add(item.TeacherName);
        }
        if (first != null) yield return BuildTimetableDistributionRow(first, count, teachers);
    }

    private static TimetableExportRow BuildTimetableDistributionRow(TimetableExportItem item, int count, HashSet<string> teachers)
    {
        var assignedTeachers = string.Join(", ", teachers.OrderBy(name => name));
        return new(item, new Dictionary<string, string?>
        {
            ["Subject"] = item.SubjectName, ["Class"] = item.StandardName ?? "—", ["Division"] = item.DivisionName ?? "—",
            ["TotalPeriodsAssigned"] = count.ToString(), ["WeeklyAllocation"] = count.ToString(),
            ["AssignedTeachers"] = assignedTeachers.Length == 0 ? "—" : assignedTeachers
        });
    }

    private static int CompareTimetableDistributionKey(TimetableExportItem left, TimetableExportItem right)
    {
        var result = left.StandardId.CompareTo(right.StandardId);
        if (result == 0) result = left.DivisionId.CompareTo(right.DivisionId);
        if (result == 0) result = Nullable.Compare(left.SubjectId, right.SubjectId);
        if (result == 0) result = CompareTimetableSourceOrder(left, right, byDay: false);
        return result;
    }

    private async IAsyncEnumerable<TimetableExportRow> BuildRoomExportRowsAsync(
        AcademicReportRequestDto request, int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var timetables = await GetTimetableExportQueryAsync(request);
        var slots = timetables.SelectMany(timetable => timetable.Slots).Where(slot => slot.SlotType == SlotType.Period);
        var total = await asyncExecuter.CountAsync(slots, cancellationToken);
        var query = await roomRepository.GetQueryableAsync();
        if (request.Filters.RoomIds is { Count: > 0 }) query = query.Where(room => request.Filters.RoomIds.Contains(room.Id));
        Guid? cursor = null;
        while (true)
        {
            var page = cursor.HasValue ? query.Where(room => room.Id.CompareTo(cursor.Value) > 0) : query;
            var rooms = await asyncExecuter.ToListAsync(page.OrderBy(room => room.Id).Take(batchSize)
                .Select(room => new { room.Id, room.RoomName, room.RoomTypeId }), cancellationToken);
            if (rooms.Count == 0) yield break;
            var roomIds = rooms.Select(room => room.Id).ToList();
            var typeIds = rooms.Select(room => room.RoomTypeId).Distinct().ToList();
            var types = await asyncExecuter.ToListAsync((await roomTypeRepository.GetQueryableAsync())
                .Where(type => typeIds.Contains(type.Id)).Select(type => new { type.Id, type.RoomTypeName }), cancellationToken);
            var typeNames = types.ToDictionary(type => type.Id, type => type.RoomTypeName);
            var usage = await asyncExecuter.ToListAsync(slots.SelectMany(slot => slot.Subjects)
                .Where(subject => subject.RoomId.HasValue && roomIds.Contains(subject.RoomId.Value))
                .GroupBy(subject => subject.RoomId!.Value).Select(group => new { RoomId = group.Key, Count = group.Count() }), cancellationToken);
            var counts = usage.ToDictionary(row => row.RoomId, row => row.Count);
            foreach (var room in rooms)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var utilized = counts.GetValueOrDefault(room.Id);
                yield return new TimetableExportRow(new TimetableExportItem { TimetableId = room.Id }, new Dictionary<string, string?>
                {
                    ["RoomNumber"] = room.RoomName, ["RoomType"] = typeNames.GetValueOrDefault(room.RoomTypeId, "—"),
                    ["TotalAvailablePeriods"] = total.ToString(), ["UtilizedPeriods"] = utilized.ToString(), ["FreePeriods"] = (total - utilized).ToString()
                });
            }
            cursor = rooms[^1].Id;
        }
    }

    private async IAsyncEnumerable<TimetableExportRow> BuildTimetableAuditExportRowsAsync(
        AcademicReportRequestDto request, int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var query = (await GetTimetableExportQueryAsync(request)).Where(timetable => timetable.LastModificationTime != null);
        Guid? cursor = null;
        while (true)
        {
            var page = cursor.HasValue ? query.Where(timetable => timetable.Id.CompareTo(cursor.Value) > 0) : query;
            var items = await asyncExecuter.ToListAsync(page.OrderBy(timetable => timetable.Id).Take(batchSize)
                .Select(timetable => new TimetableExportItem
                {
                    TimetableId = timetable.Id, StandardId = timetable.StandardId, DivisionId = timetable.DivisionId,
                    ModifiedAt = timetable.LastModificationTime, TeacherId = timetable.LastModifierId
                }), cancellationToken);
            if (items.Count == 0) yield break;
            await PopulateTimetableExportNamesAsync(items, cancellationToken);
            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new TimetableExportRow(item, new Dictionary<string, string?>
                {
                    ["ChangeDate"] = item.ModifiedAt!.Value.ToString("dd MMM yyyy, HH:mm"), ["ChangedBy"] = item.TeacherName,
                    ["Class"] = item.StandardName ?? "—", ["Division"] = item.DivisionName ?? "—", ["Subject"] = string.Empty,
                    ["Teacher"] = string.Empty, ["PreviousSchedule"] = string.Empty, ["NewSchedule"] = string.Empty, ["ChangeReason"] = string.Empty
                });
            }
            cursor = items[^1].TimetableId;
        }
    }

    private static int CompareTimetableExportRows(TimetableExportRow left, TimetableExportRow right, string reportType)
    {
        var comparer = Comparer<string?>.Default;
        var columns = reportType switch
        {
            AcademicReportTypes.TeacherwiseTimetable => new[] { "TeacherName", "Day", "Period" },
            AcademicReportTypes.RoomUtilizationReport => ["RoomNumber"],
            AcademicReportTypes.SubjectwiseTimetableDistribution => ["Subject", "Class", "Division"],
            AcademicReportTypes.FreePeriodsReport => ["Class", "Day", "Period"],
            _ => []
        };
        foreach (var column in columns)
        {
            var comparison = comparer.Compare(left.Values[column], right.Values[column]);
            if (comparison != 0) return comparison;
        }
        if (reportType == AcademicReportTypes.ClasswiseTimetable)
        {
            var result = comparer.Compare(left.Item.StandardName ?? "", right.Item.StandardName ?? "");
            if (result == 0) result = comparer.Compare(left.Item.DivisionName ?? "", right.Item.DivisionName ?? "");
            if (result != 0) return result;
        }
        if (reportType == AcademicReportTypes.TimetableChangeLog)
            return Nullable.Compare(right.Item.ModifiedAt, left.Item.ModifiedAt);
        return CompareTimetableSourceOrder(left.Item, right.Item,
            byDay: reportType != AcademicReportTypes.SubjectwiseTimetableDistribution);
    }

    private static int CompareTimetableSourceOrder(TimetableExportItem left, TimetableExportItem right, bool byDay)
    {
        var result = left.TimetableId.CompareTo(right.TimetableId);
        if (byDay && result == 0) result = left.Day.CompareTo(right.Day);
        if (byDay && result == 0) result = left.Sequence.CompareTo(right.Sequence);
        if (result == 0) result = left.SlotId.CompareTo(right.SlotId);
        if (result == 0) result = left.EntryId.CompareTo(right.EntryId);
        return result;
    }

    private sealed class TimetableExportItem
    {
        public Guid TimetableId { get; set; }
        public Guid StandardId { get; set; }
        public Guid DivisionId { get; set; }
        public Guid SlotId { get; set; }
        public Guid EntryId { get; set; }
        public Guid? SubjectId { get; set; }
        public Guid? TeacherId { get; set; }
        public Guid? RoomId { get; set; }
        public TimetableDayOfWeek Day { get; set; }
        public int Sequence { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public string? StandardName { get; set; }
        public string? DivisionName { get; set; }
        public string SubjectName { get; set; } = "—";
        public string TeacherName { get; set; } = "—";
        public string RoomName { get; set; } = "—";
    }

    private sealed record TimetableExportRow(TimetableExportItem Item, Dictionary<string, string?> Values);
}
