using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmExportContentBuilder(
	IRepository<AdmissionCrmApplicantIdentityProfile, Guid> identityProfileRepository,
	IRepository<AdmissionCrmPerson, Guid> personRepository,
	IRepository<AdmissionCrmDataQualityIssue, Guid> dataQualityRepository,
	IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository,
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	IRepository<AdmissionCrmRouteAssignment, Guid> routeRepository,
	AdmissionCrmGovernanceAccess access)
	: ApplicationService, ITransientDependency
{
	public async Task<string> BuildAsync(Guid academicYearId, RequestAdmissionCrmExportDto input, DateTime at, bool enforceUserScope)
	{
		var scopedLeadIds = enforceUserScope ? await access.GetScopedLeadIdsAsync(academicYearId) : null;
		var hasAcademicYearWideScope = !enforceUserScope || await access.HasAcademicYearWideScopeAsync(academicYearId);
		var rows = Header(input.ReportType, academicYearId, at);
		if (input.ReportType == AdmissionCrmExportReportType.AuditTimeline)
			await AddAuditRowsAsync(rows, academicYearId, scopedLeadIds);
		else if (input.ReportType == AdmissionCrmExportReportType.DataQuality)
			await AddDataQualityRowsAsync(rows, academicYearId, scopedLeadIds, hasAcademicYearWideScope);
		else if (input.ReportType == AdmissionCrmExportReportType.GovernmentReadiness)
			await AddGovernmentReadinessRowsAsync(rows, academicYearId, scopedLeadIds);
		else
			await AddLeadRowsAsync(rows, academicYearId, enforceUserScope);
		return string.Join("\r\n", rows.Select(row => string.Join(",", row.Select(Csv))));
	}

	private async Task AddAuditRowsAsync(List<string[]> rows, Guid academicYearId, List<Guid>? scopedLeadIds)
	{
		rows.Add(["LeadId", "EventType", "Title", "ActorUserId", "OccurredAt", "Details"]);
		var queryable = await timelineRepository.GetQueryableAsync();
		var persisted = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId && (scopedLeadIds == null || scopedLeadIds.Contains(x.LeadId))));
		var items = persisted.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id).Take(10_000);
		rows.AddRange(items.Select(x => new[] { x.LeadId.ToString(), x.EventType.ToString(), x.Title,
			x.ActorUserId?.ToString() ?? "", x.OccurredAt.ToString("O"), x.Details ?? "" }));
	}

	private async Task AddDataQualityRowsAsync(List<string[]> rows, Guid academicYearId, List<Guid>? scopedLeadIds, bool hasWideScope)
	{
		rows.Add(["IssueId", "Severity", "Status", "IssueType", "LeadId", "DetectedAt", "Summary"]);
		var queryable = await dataQualityRepository.GetQueryableAsync();
		var items = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId && (hasWideScope ||
			 (x.LeadId.HasValue && scopedLeadIds != null && scopedLeadIds.Contains(x.LeadId.Value)))).Take(10_000));
		rows.AddRange(items.Select(x => new[] { x.Id.ToString(), x.Severity.ToString(), x.Status.ToString(),
			x.IssueType.ToString(), x.LeadId?.ToString() ?? "", x.LastDetectedAt.ToString("O"), x.Title }));
	}

	private async Task AddGovernmentReadinessRowsAsync(List<string[]> rows, Guid academicYearId, List<Guid>? scopedLeadIds)
	{
		rows.Add(["LeadId", "ApplicantPersonId", "ApplicantName", "DateOfBirth", "DesiredClass", "RouteType", "RouteStatus",
			"IdentityResolution", "CoreIdentityVerified", "PEN", "PENStatus", "APAAR", "APAARConsentStatus", "APAARStatus",
			"UDISEStudentCode", "UDISEStatus", "DataProvenance"]);
		var profileQueryable = await identityProfileRepository.GetQueryableAsync();
		var profiles = await AsyncExecuter.ToListAsync(profileQueryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId && (scopedLeadIds == null || scopedLeadIds.Contains(x.LeadId))));
		var personIds = profiles.Select(x => x.ApplicantPersonId).Distinct().ToList();
		var personQueryable = await personRepository.GetQueryableAsync();
		var people = (await AsyncExecuter.ToListAsync(personQueryable.Where(x => x.TenantId == CurrentTenant.Id && personIds.Contains(x.Id)))).ToDictionary(x => x.Id);
		var leadIds = profiles.Select(x => x.LeadId).Distinct().ToList();
		var leadQueryable = await leadRepository.GetQueryableAsync();
		var leads = (await AsyncExecuter.ToListAsync(leadQueryable.Where(x => x.TenantId == CurrentTenant.Id && leadIds.Contains(x.Id)))).ToDictionary(x => x.Id);
		var routeQueryable = await routeRepository.GetQueryableAsync();
		var routes = (await AsyncExecuter.ToListAsync(routeQueryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId && x.IsPrimary))).GroupBy(x => x.LeadId)
			.ToDictionary(x => x.Key, x => x.OrderByDescending(route => route.CreationTime).First());
		foreach (var profile in profiles.OrderBy(x => x.LeadId))
		{
			if (!people.TryGetValue(profile.ApplicantPersonId, out var person) || !leads.TryGetValue(profile.LeadId, out var lead)) continue;
			routes.TryGetValue(profile.LeadId, out var route);
			rows.Add([lead.Id.ToString(), person.Id.ToString(),
				string.Join(" ", new[] { person.FirstName, person.MiddleName, person.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))),
				person.DateOfBirth?.ToString("yyyy-MM-dd") ?? "", lead.DesiredClassName ?? "", route?.RouteType.ToString() ?? "",
				route?.Status.ToString() ?? "", profile.Status.ToString(), person.CoreIdentityVerified.ToString(), person.PenNumber ?? "",
				person.PenStatus.ToString(), person.ApaarId ?? "", person.ApaarConsentStatus.ToString(), person.ApaarStatus.ToString(),
				person.UdiseStudentCode ?? "", person.UdiseStatus.ToString(), person.Provenance.ToString()]);
		}
	}

	private async Task AddLeadRowsAsync(List<string[]> rows, Guid academicYearId, bool enforceUserScope)
	{
		rows.Add(["LeadId", "Stage", "Source", "DesiredClass", "OwnerUserId", "CreatedAt", "LastActivityAt"]);
		var queryable = await leadRepository.GetQueryableAsync();
		var query = queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId);
		if (enforceUserScope) query = await access.ApplyLeadScopeAsync(query);
		var items = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.CreationTime).Take(10_000));
		rows.AddRange(items.Select(x => new[] { x.Id.ToString(), x.Stage.ToString(), x.InquirySourceSnapshot ?? "",
			x.DesiredClassName ?? "", x.OwnerUserId?.ToString() ?? "", x.CreationTime.ToString("O"), x.LastActivityTime.ToString("O") }));
	}

	private List<string[]> Header(AdmissionCrmExportReportType reportType, Guid academicYearId, DateTime at) =>
	[
		["Report", reportType.ToString()], ["Tenant", CurrentTenant.Id?.ToString() ?? "host"],
		["AcademicYearId", academicYearId.ToString()], ["GeneratedAt", at.ToUniversalTime().ToString("O")],
		["DataAsOf", at.ToUniversalTime().ToString("O")], ["DefinitionVersion", "1"], []
	];

	private static string Csv(string value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
}
