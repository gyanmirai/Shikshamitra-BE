using ShikshaMitra.HR.Audit;
using ShikshaMitra.HR.Policies;
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
	IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
	public async Task<List<Dictionary<string, string?>>> BuildRowsAsync(HrReportRequestDto input)
	{
		var policyQuery = await policyAssignmentRepository.GetQueryableAsync();
		var auditQuery = await auditRepository.GetQueryableAsync();

		if (input.Filters.FromDate.HasValue)
		{
			var from = input.Filters.FromDate.Value.ToDateTime(TimeOnly.MinValue);
			policyQuery = policyQuery.Where(x => x.EffectiveFrom >= from);
			auditQuery = auditQuery.Where(x => x.OccurredAt >= from);
		}

		if (input.Filters.ToDate.HasValue)
		{
			var to = input.Filters.ToDate.Value.ToDateTime(TimeOnly.MaxValue);
			policyQuery = policyQuery.Where(x => x.EffectiveFrom <= to);
			auditQuery = auditQuery.Where(x => x.OccurredAt <= to);
		}

		var policyRows = (await asyncExecuter.ToListAsync(policyQuery.OrderByDescending(x => x.EffectiveFrom).Take(HrReportRequestProcessor.MaxExportRows / 2)))
			.Select(x => Row(
				("recordType", "Policy Assignment"),
				("entity", x.TargetType.ToString()),
				("action", x.IsActive ? "Active" : "Inactive"),
				("target", x.TargetDisplayName ?? x.TargetKey),
				("effectiveFrom", FormatDate(x.EffectiveFrom)),
				("effectiveTo", FormatDate(x.EffectiveTo)),
				("isActive", x.IsActive ? "Yes" : "No"),
				("occurredAt", null),
				("summary", x.AssignmentReason)));

		var auditRows = (await asyncExecuter.ToListAsync(auditQuery.OrderByDescending(x => x.OccurredAt).Take(HrReportRequestProcessor.MaxExportRows / 2)))
			.Select(x => Row(
				("recordType", "Audit"),
				("entity", x.EntityType),
				("action", x.Action),
				("target", x.EntityName),
				("effectiveFrom", null),
				("effectiveTo", null),
				("isActive", null),
				("occurredAt", FormatDateTime(x.OccurredAt)),
				("summary", x.Summary)));

		return policyRows.Concat(auditRows).Take(HrReportRequestProcessor.MaxExportRows).ToList();
	}
}
