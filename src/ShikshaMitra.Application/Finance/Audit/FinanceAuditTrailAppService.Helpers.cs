using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;

namespace ShikshaMitra.Finance.Audit;

public partial class FinanceAuditTrailAppService
{
	private async Task EnsureCanViewFinanceAuditAsync()
	{
		if (await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Auditing.View)
			|| await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.AuditTrail.View)
			|| await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View))
			return;
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Finance.Auditing.View);
	}

	private static IQueryable<FinanceAuditEvent> ApplyFilters(
		IQueryable<FinanceAuditEvent> query, FinanceAuditEventListRequestDto input)
	{
		if (input.AcademicYearId.HasValue)
			query = query.Where(x => x.AcademicYearId == input.AcademicYearId.Value);
		if (!string.IsNullOrWhiteSpace(input.SubModuleCode))
			query = query.Where(x => x.SubModuleCode == Normalize(input.SubModuleCode));
		if (!string.IsNullOrWhiteSpace(input.EntityType))
			query = query.Where(x => x.EntityType == Normalize(input.EntityType));
		if (input.EntityId.HasValue)
			query = query.Where(x => x.EntityId == input.EntityId.Value);
		if (input.StudentId.HasValue)
			query = query.Where(x => x.StudentId == input.StudentId.Value);
		if (!string.IsNullOrWhiteSpace(input.Action))
			query = query.Where(x => x.Action == Normalize(input.Action));
		if (input.ActorUserId.HasValue)
			query = query.Where(x => x.ActorUserId == input.ActorUserId.Value);
		if (!string.IsNullOrWhiteSpace(input.CorrelationId))
			query = query.Where(x => x.CorrelationId == input.CorrelationId.Trim());
		if (input.FromDate.HasValue)
			query = query.Where(x => x.OccurredAt >= input.FromDate.Value);
		if (input.ToDate.HasValue)
			query = query.Where(x => x.OccurredAt <= input.ToDate.Value);
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim().ToLowerInvariant();
			var grDigits = ExtractGrDigits(input.Search);
			query = query.Where(x =>
				x.EntityName.ToLower().Contains(search) ||
				x.Summary.ToLower().Contains(search) ||
				(x.ActorNameSnapshot != null && x.ActorNameSnapshot.ToLower().Contains(search)) ||
				(x.StudentNameSnapshot != null && x.StudentNameSnapshot.ToLower().Contains(search)) ||
				(x.StudentGrNoSnapshot != null && x.StudentGrNoSnapshot.ToLower().Contains(search)) ||
				(x.StudentRollNoSnapshot != null && x.StudentRollNoSnapshot.ToLower().Contains(search)) ||
				(grDigits.Length > 0 && x.StudentGrNoSnapshot != null && x.StudentGrNoSnapshot.ToLower().Contains(grDigits)) ||
				(x.CorrelationId != null && x.CorrelationId.ToLower().Contains(search)) ||
				(x.ContextJson != null && x.ContextJson.ToLower().Contains(search)));
		}
		return query;
	}

	private static IOrderedQueryable<FinanceAuditEvent> ApplySorting(IQueryable<FinanceAuditEvent> query, string? sorting)
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

	private static (string Field, bool Descending) ParseSorting(string? sorting)
	{
		if (string.IsNullOrWhiteSpace(sorting)) return ("occurredAt", true);
		var parts = sorting.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		var field = (parts.Length > 0 ? parts[0] : "occurredAt") switch
		{
			var n when n.Equals("occurredAt", StringComparison.OrdinalIgnoreCase) => "occurredAt",
			var n when n.Equals("action", StringComparison.OrdinalIgnoreCase) => "action",
			var n when n.Equals("actorNameSnapshot", StringComparison.OrdinalIgnoreCase) => "actorNameSnapshot",
			_ => "occurredAt"
		};
		var descending = parts.Length < 2 || !parts[1].Equals("asc", StringComparison.OrdinalIgnoreCase);
		return (field, descending);
	}

	private static int NormalizeMaxResultCount(int maxResultCount) =>
		maxResultCount <= 0 ? 25 : Math.Min(maxResultCount, 100);

	private static string Normalize(string value) => value.Trim().ToLowerInvariant().Replace("-", "_");

	private static string ExtractGrDigits(string? value)
	{
		if (string.IsNullOrWhiteSpace(value)) return string.Empty;
		var withoutPrefix = Regex.Replace(value.Trim(), @"^(?i)gr[\s\-]*", string.Empty);
		return Regex.Replace(withoutPrefix, @"[^\d]", string.Empty).TrimStart('0');
	}

	private static string BuildStudentDisplayName(string? first, string? middle, string? last) =>
		string.Join(" ", new[] { first, middle, last }.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));

	private static string? BuildStandardDivision(string? standard, string? division)
	{
		if (string.IsNullOrWhiteSpace(standard) && string.IsNullOrWhiteSpace(division)) return null;
		if (string.IsNullOrWhiteSpace(division)) return standard;
		if (string.IsNullOrWhiteSpace(standard)) return division;
		return $"{standard}-{division}";
	}

	private static string BuildStudentLookupLabel(string displayName, string? grNo, string? rollNo, string? standardDivision)
	{
		var parts = new List<string> { displayName };
		if (!string.IsNullOrWhiteSpace(grNo)) parts.Add($"GR {grNo}");
		if (!string.IsNullOrWhiteSpace(rollNo)) parts.Add($"Roll {rollNo}");
		if (!string.IsNullOrWhiteSpace(standardDivision)) parts.Add(standardDivision);
		return string.Join(" · ", parts);
	}
}
