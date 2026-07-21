using Microsoft.AspNetCore.Http;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Common.Middleware;

public class AdmissionCrmSettingsAccessMiddleware : ITransientDependency
{
	private readonly RequestDelegate _next;

	public AdmissionCrmSettingsAccessMiddleware(RequestDelegate next) => _next = next;

	public async Task InvokeAsync(HttpContext context, ICurrentTenant currentTenant, IRepository<AdmissionCrmSettingsAccessPolicy, Guid> policies)
	{
		var key = ResolveSettingsKey(context.Request.Path);
		if (key is null || currentTenant.Id is null)
		{
			await _next(context);
			return;
		}

		var policy = (await policies.GetListAsync(x => x.SettingsKey == key && x.SectionKey == null)).FirstOrDefault();
		var mode = policy?.AccessMode ?? AdmissionCrmSettingsAccessMode.Editable;
		var isRead = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method) || HttpMethods.IsOptions(context.Request.Method);
		if (mode == AdmissionCrmSettingsAccessMode.Hidden || (mode == AdmissionCrmSettingsAccessMode.ReadOnly && !isRead))
		{
			throw new AbpAuthorizationException(mode == AdmissionCrmSettingsAccessMode.Hidden
				? "This Enrollment settings category is managed by the platform and is hidden for this school."
				: "This Enrollment settings category is managed by the platform and is read-only for this school.");
		}

		await _next(context);
	}

	private static string? ResolveSettingsKey(PathString requestPath)
	{
		var path = requestPath.Value ?? string.Empty;
		if (!path.StartsWith("/api/student-enrollment/admissions-crm", StringComparison.OrdinalIgnoreCase) || path.Contains("/setup/", StringComparison.OrdinalIgnoreCase)) return null;
		if (Contains(path, "cycle", "pipeline-stage", "journey")) return "journey";
		if (Contains(path, "form-template", "inquiry-source", "channel-setting")) return "inquiry-capture";
		if (Contains(path, "playbook", "entrance-exam", "exam-slot")) return "entrance-exams";
		if (Contains(path, "registration", "document-checklist", "applicant-document", "form-number")) return "registration-documents";
		if (Contains(path, "seat-pool", "seat-offer", "waitlist", "admission-decision", "fee-polic")) return "offers-seats";
		if (Contains(path, "routing", "scoring", "owner", "priority")) return "routing-scoring";
		if (Contains(path, "activation", "number-series", "identifier")) return "activation";
		if (Contains(path, "policy", "retention", "privacy", "data-quality")) return "governance";
		if (Contains(path, "message", "communication", "delivery")) return "communications";
		if (Contains(path, "automation", "follow-up", "stale", "archive", "sla")) return "automation";
		return null;
	}

	private static bool Contains(string path, params string[] values) => values.Any(value => path.Contains(value, StringComparison.OrdinalIgnoreCase));
}
