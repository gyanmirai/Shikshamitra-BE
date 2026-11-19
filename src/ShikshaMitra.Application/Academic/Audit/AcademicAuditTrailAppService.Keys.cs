using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.Audit;

public partial class AcademicAuditTrailAppService
{
    private sealed record AuditKey(string Module, Guid SourceId, AcademicAuditEventDto Event);

    private sealed class AuditKeyReadContext(bool descending, int candidateCount)
    {
        public bool Descending { get; } = descending;
        public int CandidateCount { get; } = candidateCount;
        public long TotalCount { get; set; }
    }

    private sealed class AuditKeyRow
    {
        public Guid SourceId { get; set; }
        public Guid EntityId { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public Guid? ActorId { get; set; }
        public string? ActorName { get; set; }
        public string? ActorRole { get; set; }
        public string? Reason { get; set; }
        public string? CorrelationId { get; set; }
        public string? IpAddress { get; set; }
        public string? Device { get; set; }
        public string? Browser { get; set; }
        public string? OperatingSystem { get; set; }
        public string? RequestPath { get; set; }
        public string? HttpMethod { get; set; }
        public string? Context { get; set; }
    }

    private static bool IncludesSource(AcademicAuditEventListRequestDto input, string module) =>
        string.IsNullOrWhiteSpace(input.SubModuleCode) || Normalize(input.SubModuleCode) == module;

    private async Task<List<AuditKey>> LoadKeysAsync(AcademicAuditEventListRequestDto input, AuditSourceScope? scope,
        AuditKeyReadContext? readContext = null)
    {
        var keys = new List<AuditKey>();
        if (IncludesSource(input, "academic_calendar"))
        {
            var query = await calendarAuditRepository.GetQueryableAsync();
            if (scope != null) query = query.Where(row => scope.CalendarIds.Contains(row.AcademicCalendarId));
            keys.AddRange(await ReadKeysAsync(query.Select(row => new AuditKeyRow
            {
                SourceId = row.Id, EntityId = row.AcademicCalendarDayId ?? row.AcademicCalendarId,
                EntityType = row.EntityType ?? "calendar",
                Name = row.EntityName ?? (row.AcademicCalendarDayId.HasValue ? "Calendar day" : "Academic calendar"),
                Action = row.Action.ToString(), Timestamp = row.ChangedAt, ActorId = row.ChangedByUserId,
                ActorName = row.ActorNameSnapshot, ActorRole = row.ActorRoleSnapshot, Reason = row.Reason,
                CorrelationId = row.CorrelationId, IpAddress = row.IpAddress, Device = row.Device,
                Browser = row.Browser, OperatingSystem = row.OperatingSystem, RequestPath = row.RequestPath, HttpMethod = row.HttpMethod
            }), input, "academic_calendar", readContext));
        }
        if (IncludesSource(input, "subject_creation"))
        {
            var query = await subjectAuditLogRepository.GetQueryableAsync();
            if (scope != null) query = query.Where(row => scope.SubjectIds.Contains(row.SubjectId));
            keys.AddRange(await ReadKeysAsync(query.Select(row => new AuditKeyRow
            {
                SourceId = row.Id, EntityId = row.SubjectId, EntityType = "subject", Name = row.EntityName ?? "Subject",
                Action = row.Action, Timestamp = row.ChangedAt, ActorId = row.ChangedByUserId,
                ActorName = row.ActorNameSnapshot, Reason = row.Reason,
                IpAddress = row.IpAddress, Device = row.Device, Browser = row.Browser,
                OperatingSystem = row.OperatingSystem, RequestPath = row.RequestPath, HttpMethod = row.HttpMethod
            }), input, "subject_creation", readContext));
        }
        if (IncludesSource(input, "elective_group_creation"))
            keys.AddRange(await LoadElectiveGroupKeysAsync(input, scope, readContext));
        if (IncludesSource(input, "room_availability"))
        {
            var query = await roomAuditRepository.GetQueryableAsync();
            if (scope != null) query = query.Where(row => row.ReservationId.HasValue && scope.ReservationIds.Contains(row.ReservationId.Value));
            keys.AddRange(await ReadKeysAsync(query.Select(row => new AuditKeyRow
            {
                SourceId = row.Id, EntityId = row.ReservationId ?? row.RoomId,
                EntityType = row.ReservationId.HasValue ? "room_reservation" : "room",
                Name = row.ReservationId.HasValue ? "Room reservation" : "Room", Action = row.Action,
                Timestamp = row.Timestamp, ActorId = row.PerformedByUserId, ActorName = row.UserName, Reason = row.Remarks
            }), input, "room_availability", readContext));
        }
        if (IncludesSource(input, "substitution_management"))
        {
            var query = await substitutionAuditRepository.GetQueryableAsync();
            if (scope != null) query = query.Where(row => scope.SubstitutionIds.Contains(row.SubstitutionRequestId));
            var includeContext = !string.IsNullOrWhiteSpace(input.Search);
            keys.AddRange(await ReadKeysAsync(query.Select(row => new AuditKeyRow
            {
                SourceId = row.Id, EntityId = row.SubstitutionRequestId, EntityType = "substitution_request", Name = "Substitution request",
                Action = row.Action, Timestamp = row.Timestamp, ActorId = row.ActorUserId, ActorName = row.ActorName,
                Context = includeContext ? row.Details : null
            }), input, "substitution_management", readContext));
        }
        if (IncludesSource(input, "seating_plan"))
        {
            var query = await seatingPlanAuditRepository.GetQueryableAsync();
            if (scope != null) query = query.Where(row => scope.ExamIds.Contains(row.ExamId));
            var includeContext = !string.IsNullOrWhiteSpace(input.Search);
            keys.AddRange(await ReadKeysAsync(query.Select(row => new AuditKeyRow
            {
                SourceId = row.Id, EntityId = row.ExamId, EntityType = "exam", Name = "Exam seating plan",
                Action = row.Action.ToString(), Timestamp = row.ChangedAt, ActorId = row.ActorUserId,
                ActorName = row.ActorNameSnapshot, Reason = row.Reason,
                Context = includeContext ? row.AssignmentsSnapshotJson : null
            }), input, "seating_plan", readContext));
        }
        if (IncludesSource(input, "result_creation"))
        {
            var query = await examResultAuditRepository.GetQueryableAsync();
            if (scope != null) query = query.Where(row => scope.ExamIds.Contains(row.ExamId));
            keys.AddRange(await ReadKeysAsync(query.Select(row => new AuditKeyRow
            {
                SourceId = row.Id, EntityId = row.ExamId, EntityType = "exam_result", Name = "Exam result",
                Action = row.Action, Timestamp = row.CreationTime, ActorId = row.PerformedById, Reason = row.Remarks
            }), input, "result_creation", readContext));
        }
        return keys;
    }

    private async Task<List<AuditKey>> ReadKeysAsync(IQueryable<AuditKeyRow> query,
        AcademicAuditEventListRequestDto input, string module, AuditKeyReadContext? readContext = null,
        bool applyDateFilters = true)
    {
        if (!string.IsNullOrWhiteSpace(input.CorrelationId) && module != "academic_calendar") return [];
        if (applyDateFilters && input.FromDate.HasValue) query = query.Where(row => row.Timestamp >= input.FromDate.Value);
        if (applyDateFilters && input.ToDate.HasValue) query = query.Where(row => row.Timestamp <= input.ToDate.Value);
        if (input.EntityId.HasValue) query = query.Where(row => row.EntityId == input.EntityId.Value);
        if (input.ActorUserId.HasValue) query = query.Where(row => row.ActorId == input.ActorUserId.Value);
        if (!string.IsNullOrWhiteSpace(input.CorrelationId))
        {
            var correlationId = input.CorrelationId.Trim();
            query = query.Where(row => row.CorrelationId == correlationId);
        }
        if (readContext != null)
        {
            var count = await AsyncExecuter.LongCountAsync(query);
            readContext.TotalCount += count;
            if (count == 0) return [];
            if (count > readContext.CandidateCount)
            {
                var ordered = readContext.Descending ? query.OrderByDescending(row => row.Timestamp) : query.OrderBy(row => row.Timestamp);
                var boundary = await AsyncExecuter.FirstAsync(ordered.Skip(readContext.CandidateCount - 1).Select(row => row.Timestamp));
                // Keep the entire timestamp boundary so .NET can preserve GUID and synthetic-event ties.
                query = readContext.Descending ? query.Where(row => row.Timestamp >= boundary) : query.Where(row => row.Timestamp <= boundary);
            }
        }
        var rows = await AsyncExecuter.ToListAsync(query.OrderByDescending(row => row.Timestamp));
        return rows.Select(row => MapKey(module, row)).ToList();
    }

    private static AuditKey MapKey(string module, AuditKeyRow row)
    {
        var source = module switch
        {
            "academic_calendar" => "academic_calendar_audit", "subject_creation" => "subject_audit",
            "room_availability" => "room_audit", "substitution_management" => "substitution_audit",
            "seating_plan" => "seating_plan_audit", "elective_group_creation" => "elective_group_entity", _ => "exam_result_audit"
        };
        var summary = module switch
        {
            "academic_calendar" => $"{ToLabel(row.Action)}: {row.Name}",
            "subject_creation" => $"{ToLabel(row.Action)} subject",
            "room_availability" => $"{row.Action} room availability record",
            "substitution_management" => $"{row.Action} substitution request",
            "seating_plan" => $"{ToLabel(row.Action)} seating plan",
            "elective_group_creation" => $"{(row.Action == "created" ? "Created" : "Updated")} {row.Name}", _ => $"{row.Action} exam result"
        };
        var context = module is "academic_calendar" or "subject_creation"
            ? BuildJson(new { Metadata = new { row.IpAddress, row.Device, row.Browser, row.OperatingSystem, row.RequestPath, row.HttpMethod } })
            : row.Context;
        return new AuditKey(module, row.SourceId, AcademicAuditTrailProjection.Create(source, module,
            row.EntityType, row.EntityId, row.Name, row.Action, row.Timestamp, summary, module == "elective_group_creation" ? null : row.SourceId,
            actorUserId: row.ActorId, actorName: row.ActorName, actorRole: row.ActorRole, reason: row.Reason,
            contextJson: context, correlationId: row.CorrelationId));
    }

    private async Task<List<AuditKey>> LoadElectiveGroupKeysAsync(AcademicAuditEventListRequestDto input, AuditSourceScope? scope,
        AuditKeyReadContext? readContext)
    {
        var query = await electiveGroupRepository.GetQueryableAsync();
        query = query.Where(row => !row.IsDeleted);
        if (scope != null) query = query.Where(row => scope.MediumIds.Contains(row.AcademicMediumId));
        if (input.FromDate.HasValue) query = query.Where(row => row.CreationTime >= input.FromDate.Value || row.LastModificationTime >= input.FromDate.Value);
        if (input.ToDate.HasValue) query = query.Where(row => row.CreationTime <= input.ToDate.Value || row.LastModificationTime <= input.ToDate.Value);
        if (input.EntityId.HasValue) query = query.Where(row => row.Id == input.EntityId.Value);
        if (readContext != null)
        {
            var created = query.Select(row => new AuditKeyRow
            {
                SourceId = row.Id, EntityId = row.Id, EntityType = "elective_group", Name = row.Name,
                Action = "created", Timestamp = row.CreationTime, ActorId = row.CreatorId, Reason = null
            });
            var updated = query.Where(row => row.LastModificationTime.HasValue).Select(row => new AuditKeyRow
            {
                SourceId = row.Id, EntityId = row.Id, EntityType = "elective_group", Name = row.Name,
                Action = "updated", Timestamp = row.LastModificationTime!.Value, ActorId = row.LastModifierId, Reason = row.ArchiveReason
            });
            return await ReadKeysAsync(created.Concat(updated), input, "elective_group_creation", readContext, applyDateFilters: false);
        }
        var rows = await AsyncExecuter.ToListAsync(query.OrderByDescending(row => row.LastModificationTime ?? row.CreationTime)
            .Select(row => new { row.Id, row.Name, row.CreationTime, row.CreatorId, row.LastModificationTime, row.LastModifierId, row.ArchiveReason }));
        return rows.SelectMany(row => CreateAuditedEntityEvents("elective_group_entity", "elective_group_creation", "elective_group",
            row.Id, row.Name, row.CreationTime, row.CreatorId, row.LastModificationTime, row.LastModifierId, row.ArchiveReason, "{}")
            .Select(auditEvent => new AuditKey("elective_group_creation", row.Id, auditEvent))).ToList();
    }

    private async Task<List<AcademicAuditEventDto>> HydrateKeysAsync(List<AuditKey> keys,
        AcademicAuditEventListRequestDto input, AuditSourceScope? scope)
    {
        var events = new List<AcademicAuditEventDto>();
        foreach (var group in keys.GroupBy(key => key.Module))
        {
            var ids = group.Select(key => key.SourceId).Distinct().ToArray();
            var rows = group.Key switch
            {
                "academic_calendar" => await GetCalendarEventsAsync(input, scope, ids),
                "subject_creation" => await GetSubjectEventsAsync(input, scope, ids),
                "elective_group_creation" => await GetElectiveGroupEventsAsync(input, scope, ids),
                "room_availability" => await GetRoomEventsAsync(input, scope, ids),
                "substitution_management" => await GetSubstitutionEventsAsync(input, scope, ids),
                "seating_plan" => await GetSeatingPlanEventsAsync(input, scope, ids),
                _ => await GetResultEventsAsync(input, scope, ids)
            };
            var byId = rows.ToDictionary(row => row.Id);
            events.AddRange(group.Select(key => byId[key.Event.Id]));
        }
        var positions = keys.Select((key, index) => (key.Event.Source, key.Event.Id, index))
            .ToDictionary(pair => (pair.Source, pair.Id), pair => pair.index);
        return events.OrderBy(row => positions[(row.Source, row.Id)]).ToList();
    }
}
