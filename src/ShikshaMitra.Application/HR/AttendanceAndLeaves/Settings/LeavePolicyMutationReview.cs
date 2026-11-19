using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

[RemoteService(false)]
public class LeavePolicyMutationReview(LeavePolicyImpactPreviewReader reader,
	HrPolicyImpactConfirmationService confirmation, IHttpContextAccessor http,
	IRepository<AcademicYear, Guid> years) : ApplicationService, ITransientDependency
{
	public const string ScopeHeader = "X-HR-Leave-Impact-Scope";
	public async Task EnsureAsync(Guid yearId, LeavePolicy? policy, string action, LeavePolicyCreateRequest? proposal = null)
	{
		// Draft maintenance does not alter an operational configuration. Activation,
		// active edits and default changes retain the reviewed mutation requirement.
		if (policy?.Status != HrPolicyStatus.Active && action != "set-default" &&
			proposal?.Status != "Active" && proposal?.IsDefault != true) return;
		var year = await years.GetAsync(yearId);
		var scope = proposal?.ImpactScope;
		var raw = http.HttpContext?.Request.Headers[ScopeHeader].FirstOrDefault();
		if (scope == null && !string.IsNullOrWhiteSpace(raw))
		{
			try { scope = JsonSerializer.Deserialize<LeavePolicyReviewScope>(raw, new JsonSerializerOptions(JsonSerializerDefaults.Web)); }
			catch (JsonException) { throw new BusinessException(HRErrorCodes.HrPolicyImpactConfirmationInvalid); }
		}
		var today = Clock.Now.Date;
		var date = today < year.StartDate.Date ? year.StartDate.Date : today > year.EndDate.Date ? year.EndDate.Date : today;
		var request = new LeavePolicyImpactRequest { AcademicYearId = yearId, PolicyId = policy?.Id, Action = action,
			AsOfDate = scope?.AsOfDate ?? date, FromDate = scope?.FromDate, ToDate = scope?.ToDate,
			ExpectedVersion = scope?.ExpectedVersion ?? (proposal as LeavePolicyUpdateRequest)?.ExpectedVersion, Proposal = proposal };
		var assessment = await reader.ReadAsync(request);
		await confirmation.EnsureConfirmedAsync(assessment.Preview);
	}
}
