using System;
using System.Linq;

namespace ShikshaMitra.HR.Audit;

public static class HrAuditTrailQuery
{
	public static IQueryable<HrAuditEvent> ApplyFilters(IQueryable<HrAuditEvent> query, HrAuditEventListRequestDto input)
	{
		if (input.AcademicYearId.HasValue)
		{
			query = query.Where(x => x.AcademicYearId == input.AcademicYearId.Value);
		}
		if (!string.IsNullOrWhiteSpace(input.EntityType))
		{
			var entityType = Normalize(input.EntityType);
			query = query.Where(x => x.EntityType == entityType);
		}
		if (input.EntityId.HasValue)
		{
			query = query.Where(x => x.EntityId == input.EntityId.Value);
		}
		if (!string.IsNullOrWhiteSpace(input.Action))
		{
			var action = Normalize(input.Action);
			query = query.Where(x => x.Action == action);
		}
		if (input.ActorUserId.HasValue)
		{
			query = query.Where(x => x.ActorUserId == input.ActorUserId.Value);
		}
		if (input.FromDate.HasValue)
		{
			query = query.Where(x => x.OccurredAt >= input.FromDate.Value);
		}
		if (input.ToDate.HasValue)
		{
			query = query.Where(x => x.OccurredAt <= input.ToDate.Value);
		}
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim().ToLowerInvariant();
			query = query.Where(x =>
				x.EntityName.ToLower().Contains(search) ||
				x.Summary.ToLower().Contains(search) ||
				(x.ActorNameSnapshot != null && x.ActorNameSnapshot.ToLower().Contains(search)));
		}
		return query;
	}

	public static IOrderedQueryable<HrAuditEvent> ApplySorting(IQueryable<HrAuditEvent> query, string? sorting)
	{
		var sort = ParseSorting(sorting);
		return sort.Field switch
		{
			"action" => sort.Descending
				? query.OrderByDescending(x => x.Action).ThenByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
				: query.OrderBy(x => x.Action).ThenByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id),
			"actorNameSnapshot" => sort.Descending
				? query.OrderByDescending(x => x.ActorNameSnapshot).ThenByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
				: query.OrderBy(x => x.ActorNameSnapshot).ThenByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id),
			_ => sort.Descending
				? query.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
				: query.OrderBy(x => x.OccurredAt).ThenByDescending(x => x.Id)
		};
	}

	public static int NormalizeMaxResultCount(int maxResultCount) => maxResultCount <= 0 ? 25 : Math.Min(maxResultCount, 100);

	public static string Normalize(string value) => value.Trim().ToLowerInvariant().Replace("-", "_");

	private static (string Field, bool Descending) ParseSorting(string? sorting)
	{
		if (string.IsNullOrWhiteSpace(sorting))
		{
			return ("occurredAt", true);
		}

		var parts = sorting.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		var requestedField = parts.Length > 0 ? parts[0] : "occurredAt";
		var field = requestedField switch
		{
			var name when name.Equals("occurredAt", StringComparison.OrdinalIgnoreCase) => "occurredAt",
			var name when name.Equals("action", StringComparison.OrdinalIgnoreCase) => "action",
			var name when name.Equals("actorNameSnapshot", StringComparison.OrdinalIgnoreCase) => "actorNameSnapshot",
			_ => "occurredAt"
		};
		var descending = parts.Length < 2 || !parts[1].Equals("asc", StringComparison.OrdinalIgnoreCase);
		return (field, descending);
	}
}
