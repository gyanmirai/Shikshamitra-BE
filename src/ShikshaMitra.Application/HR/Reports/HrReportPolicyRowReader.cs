using ShikshaMitra.HR.Audit;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.Security.UserScope;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Reports;

using static HrReportRowFormatting;

public class HrReportPolicyRowReader(
	IRepository<HrPolicyAssignment, Guid> policyAssignmentRepository,
	IRepository<HrAuditEvent, Guid> auditRepository,
	HrReportPolicyScopeService policyScopeService,
	IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
	public async Task<List<Dictionary<string, string?>>> BuildRowsAsync(HrReportRequestDto input, UserScopeContext scope)
	{
		var result = await BuildRowsResultAsync(input, scope, exportAllRows: true);
		return result.Rows;
	}

	public async Task<HrReportRowsResult> BuildRowsResultAsync(HrReportRequestDto input, UserScopeContext scope, bool exportAllRows)
	{
		var query = await BuildCombinedPolicyQueryAsync(input, scope);
		var totalCount = await asyncExecuter.CountAsync(query);
		var sortedQuery = HrReportPolicyRowSort.Apply(query, input.Sorts);
		var skip = exportAllRows ? 0 : (input.Page - 1) * input.PageSize;
		var take = exportAllRows ? HrReportRequestProcessor.MaxExportRows : input.PageSize;
		var rows = await asyncExecuter.ToListAsync(sortedQuery.Skip(skip).Take(take));

		return new HrReportRowsResult(rows.Select(ToRow).ToList(), totalCount, true);
	}

	public async Task<HrReportRowsResult> BuildGroupedRowsResultAsync(
		HrReportRequestDto input,
		UserScopeContext scope,
		bool exportAllRows)
	{
		var groupField = input.Groups?.SingleOrDefault() ?? "recordType";
		var query = await BuildCombinedPolicyQueryAsync(input, scope);
		var keyedQuery = groupField switch
		{
			"action" => query.Select(x => new { Label = x.Action }),
			"isActive" => query.Select(x => new { Label = x.IsActive ?? string.Empty }),
			_ => query.Select(x => new { Label = x.RecordType })
		};
		var groupedQuery = keyedQuery.GroupBy(x => x.Label).Select(group => new
		{
			Label = group.Key,
			RowCount = group.Count()
		});

		var totalCount = await asyncExecuter.CountAsync(groupedQuery);
		var groups = await asyncExecuter.ToListAsync(groupedQuery);
		var rows = groups.Select(group => Row(
			(groupField, string.IsNullOrWhiteSpace(group.Label) ? "Unspecified" : group.Label),
			("rowCount", group.RowCount.ToString(System.Globalization.CultureInfo.InvariantCulture)))).ToList();
		return HrReportAggregateRows.BuildResult(rows, input, exportAllRows, totalCount, groupField);
	}

	private async Task<IQueryable<PolicyReportProjection>> BuildCombinedPolicyQueryAsync(
		HrReportRequestDto input,
		UserScopeContext scope)
	{
		var policyQuery = await BuildScopedPolicyAssignmentQueryAsync(input, scope);
		var visibleAssignmentIds = await policyScopeService.ResolveVisibleAssignmentIdsForAuditAsync(policyQuery, scope);
		var auditQuery = BuildScopedAuditQuery(
			ApplyAuditDateFilters(await auditRepository.GetQueryableAsync(), input),
			visibleAssignmentIds);

		var policyRows = policyQuery.Select(x => new PolicyReportProjection
		{
			RecordType = "Policy Assignment",
			Entity = x.TargetType.ToString(),
			Action = x.IsActive ? "Active" : "Inactive",
			Target = x.TargetDisplayName ?? x.TargetKey,
			EffectiveFrom = x.EffectiveFrom,
			EffectiveTo = x.EffectiveTo,
			IsActive = x.IsActive ? "Yes" : "No",
			OccurredAt = null,
			Summary = x.AssignmentReason,
			SortDate = x.EffectiveFrom
		});
		var auditRows = auditQuery.Select(x => new PolicyReportProjection
		{
			RecordType = "Audit",
			Entity = x.EntityType,
			Action = x.Action,
			Target = x.EntityName,
			EffectiveFrom = null,
			EffectiveTo = null,
			IsActive = null,
			OccurredAt = x.OccurredAt,
			Summary = x.Summary,
			SortDate = x.OccurredAt
		});

		return policyRows.Concat(auditRows);
	}

	private async Task<IQueryable<HrPolicyAssignment>> BuildScopedPolicyAssignmentQueryAsync(
		HrReportRequestDto input,
		UserScopeContext scope)
	{
		var policyQuery = ApplyPolicyDateFilters(await policyAssignmentRepository.GetQueryableAsync(), input);
		return await policyScopeService.ApplyScopeAsync(policyQuery, scope);
	}

	private static IQueryable<HrPolicyAssignment> ApplyPolicyDateFilters(
		IQueryable<HrPolicyAssignment> query,
		HrReportRequestDto input)
	{
		if (input.Filters.FromDate.HasValue)
		{
			var from = input.Filters.FromDate.Value.ToDateTime(TimeOnly.MinValue);
			query = query.Where(x => x.EffectiveFrom >= from);
		}

		if (input.Filters.ToDate.HasValue)
		{
			var to = input.Filters.ToDate.Value.ToDateTime(TimeOnly.MaxValue);
			query = query.Where(x => x.EffectiveFrom <= to);
		}

		return query;
	}

	private static IQueryable<HrAuditEvent> ApplyAuditDateFilters(
		IQueryable<HrAuditEvent> query,
		HrReportRequestDto input)
	{
		if (input.Filters.FromDate.HasValue)
		{
			var from = input.Filters.FromDate.Value.ToDateTime(TimeOnly.MinValue);
			query = query.Where(x => x.OccurredAt >= from);
		}

		if (input.Filters.ToDate.HasValue)
		{
			var to = input.Filters.ToDate.Value.ToDateTime(TimeOnly.MaxValue);
			query = query.Where(x => x.OccurredAt <= to);
		}

		return query;
	}

	private static IQueryable<HrAuditEvent> BuildScopedAuditQuery(
		IQueryable<HrAuditEvent> auditQuery,
		HashSet<Guid>? visibleAssignmentIds)
	{
		if (visibleAssignmentIds == null)
		{
			return auditQuery;
		}

		return auditQuery.Where(x =>
			x.EntityType == "policy_assignment" &&
			visibleAssignmentIds.Contains(x.EntityId));
	}

	private static Dictionary<string, string?> ToRow(PolicyReportProjection row) => Row(
		("recordType", row.RecordType),
		("entity", row.Entity),
		("action", row.Action),
		("target", row.Target),
		("effectiveFrom", FormatDate(row.EffectiveFrom)),
		("effectiveTo", FormatDate(row.EffectiveTo)),
		("isActive", row.IsActive),
		("occurredAt", FormatDateTime(row.OccurredAt)),
		("summary", row.Summary));

}
