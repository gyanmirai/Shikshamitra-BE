using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

[RemoteService(false)]
public class LeavePolicyImpactPreviewReader(
	ILeavePolicyRepository policies,
	IRepository<AcademicYear, Guid> years,
	IPayrollInputRevisionStore revisions,
	LeavePolicyImpactPopulationReader populations,
	LeavePolicyImpactReferenceReader references) : ApplicationService, ITransientDependency
{
	public async Task<LeavePolicyImpactAssessment> ReadAsync(LeavePolicyImpactRequest input)
	{
		var revisionBefore = await revisions.ReadAsync();
		var year = await years.GetAsync(input.AcademicYearId);
		if (year.TenantId != CurrentTenant.Id) throw new EntityNotFoundException(typeof(AcademicYear), year.Id);
		var from = (input.FromDate ?? input.AsOfDate).Date;
		var to = (input.ToDate ?? input.AsOfDate).Date;
		if (input.AsOfDate == default || from > to || input.AsOfDate.Date < from || input.AsOfDate.Date > to ||
			from < year.StartDate.Date || to > year.EndDate.Date || (to - from).TotalDays > 400)
			throw new BusinessException(HRErrorCodes.PolicyAssignmentRangeInvalid);
		if (input.Action is not ("inspect" or "update" or "create" or "set-default" or "archive"))
			throw new BusinessException(HRErrorCodes.HrPolicyInvalidType);
		if (input.Action == "create" && input.PolicyId.HasValue)
			throw new BusinessException(HRErrorCodes.HrPolicyInvalidVersionSnapshot);
		if (input.Action is "update" or "create")
		{
			if (input.Proposal == null) throw new BusinessException(HRErrorCodes.HrPolicyInvalidVersionSnapshot);
			var validation = await new LeavePolicyCreateRequestValidator().ValidateAsync(input.Proposal);
			if (!validation.IsValid) throw new Volo.Abp.Validation.AbpValidationException(validation.ToString());
			input.Proposal.Rules = input.Proposal.Rules.OrderBy(x => x.PaidType, StringComparer.Ordinal).ToList();
			input.Proposal.ImpactScope = new() { AsOfDate = input.AsOfDate.Date, FromDate = input.FromDate?.Date,
				ToDate = input.ToDate?.Date, ExpectedVersion = input.ExpectedVersion };
		}
		var all = await policies.GetListByAcademicYearIdAsync(year.Id, includeInactive: true);
		var policy = input.PolicyId.HasValue ? all.SingleOrDefault(x => x.Id == input.PolicyId) : null;
		if (input.Action != "create" && policy == null) throw new EntityNotFoundException(typeof(LeavePolicy), input.PolicyId);
		if (input.ExpectedVersion.HasValue && input.ExpectedVersion != policy?.Version)
			throw new BusinessException(HRErrorCodes.HrPolicyImpactConfirmationInvalid);
		var population = await populations.ReadAsync(input, all, from, to);
		var refs = await references.ReadAsync(year.Id, input.PolicyId, policy?.Rules.Select(x => x.Id).ToList() ?? [], from, to);
		var revisionAfter = await revisions.ReadAsync();
		var sourceStable = revisionBefore == revisionAfter;
		var populationComplete = population.Complete && sourceStable;
		var complete = populationComplete && refs.UnresolvedPayrollRecordCount == 0;
		var result = new LeavePolicyImpactDto
		{
			AsOfDate = input.AsOfDate.Date, FromDate = from, ToDate = to, CalculatedAt = Clock.Now,
			Completeness = complete ? "reference_assessment_complete" : sourceStable ? "partial" : "unavailable",
			PayrollPeriods = sourceStable ? refs.PayrollPeriods : [],
			Limitation = "Balance changes and salary differences are not calculated. Existing requests and settlements remain pinned to their original rules. Payroll exposure is provenance for review, not a recalculation or proof of changed pay."
		};
		void Count(string key, int? count, string definition, string? state = null, string scope = "requested_window", string? reason = null)
		{
			if (state == null && !sourceStable)
			{
				count = null;
				reason = "Source inputs changed during assessment; refresh before using this measure.";
			}
			result.Measures[key] = new()
			{
				Count = count, Status = state ?? (count.HasValue ? "calculated" : "unavailable"), Scope = scope,
				Definition = definition, AvailabilityReason = count.HasValue ? null : reason ?? definition,
				DrilldownKey = count.HasValue ? key : null
			};
		}
		bool Overlaps(EmployeeLeave request) => request.StartDateOnly <= DateOnly.FromDateTime(to) && request.EndDateOnly >= DateOnly.FromDateTime(from);
		Count("effectiveStaff", populationComplete ? population.Staff.Count(x => x.Current.PolicyId == input.PolicyId && x.Current.Usable) : null,
			"Distinct currently active staff resolving to this policy on AsOfDate, using staff > group > default precedence and actual group membership. Policy dates do not exclude resolution.", scope: "as_of_date",
			reason: "One or more current/proposed staff resolutions are incomplete; this is not a zero population.");
		Count("changedStaff", populationComplete ? population.Staff.Count(x => x.ChangesInWindow) : null,
			"Distinct currently active staff whose resolved policy identity, source, usability, or operational configuration changes at any assignment boundary in the requested window; this is not a balance outcome.",
			reason: "One or more current/proposed staff resolutions are incomplete; this is not a zero population.");
		Count("assignmentReferences", refs.Assignments.Count, "All same-year assignment records explicitly referencing this policy, including losing, inactive and historical assignments; not a staff count.", scope: "academic_year");
		Count("currentAssignmentReferences", refs.Assignments.Count(x => x.IsActive && x.EffectiveFrom.Date <= input.AsOfDate.Date && (!x.EffectiveTo.HasValue || x.EffectiveTo.Value.Date >= input.AsOfDate.Date)), "Active assignment references effective on AsOfDate, including losing assignments.", scope: "as_of_date");
		Count("futureAssignmentReferences", refs.Assignments.Count(x => x.IsActive && x.EffectiveFrom.Date > input.AsOfDate.Date), "Active assignment references starting after AsOfDate.", scope: "academic_year");
		Count("historicalAssignmentReferences", refs.Assignments.Count(x => !x.IsActive || (x.EffectiveTo.HasValue && x.EffectiveTo.Value.Date < input.AsOfDate.Date)), "Ended or inactive assignment references.", scope: "academic_year");
		Count("openRequestReferences", refs.Requests.Count(x => (x.Status is EmployeeLeaveStatusType.Pending or EmployeeLeaveStatusType.Reviewed) && Overlaps(x)), "Pending or Reviewed pinned requests overlapping the requested window; they are not rewritten.");
		Count("pendingRequestReferences", refs.Requests.Count(x => x.Status == EmployeeLeaveStatusType.Pending && Overlaps(x)), "Pending pinned requests overlapping the requested window.");
		Count("reviewedRequestReferences", refs.Requests.Count(x => x.Status == EmployeeLeaveStatusType.Reviewed && Overlaps(x)), "Reviewed pinned requests overlapping the requested window.");
		Count("approvedRequestReferences", refs.Requests.Count(x => x.Status == EmployeeLeaveStatusType.Approved && Overlaps(x)), "Approved pinned requests overlapping the requested window; approved values remain unchanged.");
		Count("historicalRequestReferences", refs.Requests.Count(x => x.EndDateOnly < DateOnly.FromDateTime(from)), "Pinned requests ending before the requested window, regardless of status.", scope: "all_policy_history");
		Count("futureRequestReferences", refs.Requests.Count(x => x.StartDateOnly > DateOnly.FromDateTime(to)), "Pinned requests starting after the requested window, regardless of status.", scope: "all_policy_history");
		Count("requestReferences", refs.Requests.Count, "All nondeleted requests pinned to the policy, including approved, rejected, cancelled and historical requests.", scope: "all_policy_history");
		Count("settlementReferences", refs.Settlements.Count, "Nondeleted settlement items referencing the source policy or a target rule, including cross-year transfers.", scope: "all_policy_history");
		Count("payrollExposure", refs.UnresolvedPayrollRecordCount == 0 ? refs.PayrollPeriods.Count : null,
			"Distinct payroll months in the window with an actual employee/request-date snapshot overlap or settlement payroll-month provenance; no leave-policy/version lineage is inferred on snapshots.",
			reason: "One or more linked payroll records have an invalid month and cannot be scoped; known periods are only a partial result.");
		Count("changedBalanceRows", null, "Employee/bucket balances require a date-parametric current/proposed evaluator; public balance resolution remains at academic-year start.", "not_calculated");
		Count("salaryDifferences", null, "No salary simulation or automatic payroll recalculation is performed.", "not_calculated");
		result.Fingerprint = LeavePolicyImpactFingerprintBuilder.Build(CurrentTenant.Id, CurrentUser.Id,
			revisionBefore, input, result, all, population, refs, complete);
		var preview = new HrPolicyImpactPreviewDto
		{
			PolicyType = "leave", PolicyId = input.PolicyId, AcademicYearId = year.Id, Action = input.Action,
			CurrentVersion = policy?.Version, ProposedVersion = (policy?.Version ?? 0) + 1, LeaveImpact = result
		};
		var noChange = policy != null && (input.Action == "inspect" || input.Action == "set-default" && policy.IsDefault ||
			input.Action == "archive" && policy.Status == HrPolicyStatus.Archived ||
			input.Action == "update" && input.Proposal != null &&
			LeavePolicySnapshots.Configuration(LeavePolicySnapshots.Capture(policy)) == LeavePolicySnapshots.Configuration(input.Proposal));
		if (noChange) preview.ProposedVersion = policy!.Version;
		if (!complete) preview.Warnings.Add(new() { Severity = "blocking", Code = HRErrorCodes.HrPolicyImpactBlocking,
			Message = "Population, payroll provenance, or source stability is incomplete. Refresh and resolve unavailable measures before using the assessment." });
		if (input.Action != "inspect" && !noChange && (policy?.Status == HrPolicyStatus.Active || input.Action == "set-default" || input.Proposal?.IsDefault == true ||
			input.Action == "update" && input.Proposal?.Status == "Active"))
			preview.Warnings.Add(new() { Severity = "warning", Code = HRErrorCodes.AffectedEmployees,
				Message = "Review the exact proposal and reference assessment before changing an active/default leave policy. Uncalculated balance and salary outcomes are not zero." });
		var differences = new System.Collections.Generic.List<HrPolicyVersionDifferenceDto>();
		if (policy != null && input.Action != "inspect")
		{
			var current = LeavePolicySnapshots.Capture(policy);
			var proposed = input.Proposal ?? LeavePolicySnapshots.Capture(policy);
			if (input.Action == "set-default") proposed.IsDefault = true;
			if (input.Action == "archive") { proposed.Status = "Archived"; proposed.IsDefault = false; }
			differences = HrPolicyVersionDiffBuilder.BuildDifferences(LeavePolicySnapshots.Configuration(current),
				LeavePolicySnapshots.Configuration(proposed), new HrPolicyVersionDisplayContext());
		}
		return new(preview, population, refs, differences);
	}
}

public sealed record LeavePolicyImpactAssessment(HrPolicyImpactPreviewDto Preview,
	LeaveImpactPopulation Population, LeaveImpactReferences References,
	System.Collections.Generic.List<HrPolicyVersionDifferenceDto> Differences);
