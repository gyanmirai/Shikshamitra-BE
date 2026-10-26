using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
namespace ShikshaMitra.HR.Policies;
public class HrPolicyImpactPreviewBuilder(
	IRepository<HrPolicyAssignment, Guid> assignmentRepository,
	IRepository<WorkCalendarPolicy, Guid> workCalendarPolicyRepository,
	IRepository<PayrollPolicy, Guid> payrollPolicyRepository,
	IRepository<AttendancePolicy, Guid> attendancePolicyRepository,
	HrPolicyImpactConfirmationService confirmationService,
	HrPolicyArchiveFallbackWarningService archiveFallbackWarningService,
	HrPolicyWorkCalendarReferenceResolver workCalendarReferenceResolver,
	HrPolicyAffectedEmployeeResolver affectedEmployeeResolver,
	HrPolicyPayrollRiskReader payrollRiskReader,
	HrPolicyDefaultReplacementWarningService defaultReplacementWarningService,
	HrPolicyProposedRuleDifferenceReader ruleDifferences,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	public async Task<HrPolicyImpactPreviewDto> BuildAsync(HrPolicyImpactPreviewRequestDto input)
	{
		var policyType = NormalizePolicyType(input.PolicyType);
		var academicYearId = input.AcademicYearId ?? await ResolveAcademicYearIdAsync(policyType, input.PolicyId, input);
		var preview = new HrPolicyImpactPreviewDto
		{
			PolicyType = policyType,
			PolicyId = input.PolicyId,
			AcademicYearId = academicYearId,
			Action = string.IsNullOrWhiteSpace(input.Action) ? "update" : input.Action.Trim().ToLowerInvariant()
		};

		await FillCurrentVersionAsync(preview);
		await ruleDifferences.FillAsync(preview, input);
		preview.ProposedVersion = preview.CurrentVersion.HasValue ? preview.CurrentVersion.Value + 1 : 1;
		var isRestoreDraft = string.Equals(preview.Action, "restore-draft", StringComparison.OrdinalIgnoreCase);
		var includeDraftReferences = string.Equals(preview.Action, "archive", StringComparison.OrdinalIgnoreCase);
		var workCalendarRefs = isRestoreDraft
			? WorkCalendarPolicyReferences.Empty
			: await workCalendarReferenceResolver.ResolveAsync(policyType, input.PolicyId, academicYearId, includeDraftReferences);

		var affectedAssignments = isRestoreDraft
			? []
			: await GetAffectedAssignmentsAsync(policyType, input.PolicyId, academicYearId, workCalendarRefs);
		preview.AffectedAssignmentCount = affectedAssignments.Count;
		await archiveFallbackWarningService.AddAsync(preview, affectedAssignments);

		var affectedEmployees = await affectedEmployeeResolver.ResolveAsync(academicYearId, affectedAssignments);
		preview.AffectedEmployeeCount = affectedEmployees.Count;
		preview.AffectedEmployees = affectedEmployees.Sample.Select(HrPolicyAssignmentProjection.MapAffectedEmployee).ToList();
		if (!isRestoreDraft)
		{
			await payrollRiskReader.FillPayrollRiskAsync(preview, policyType, input.PolicyId);
		}
		HrPolicyImpactWarningBuilder.AddWorkCalendarReferenceWarning(preview, workCalendarRefs);
		AddProposedWorkCalendarDeactivationWarning(preview, workCalendarRefs, input);
		await AddProposedWorkCalendarStatusWarningAsync(preview, input);
		await defaultReplacementWarningService.AddAsync(preview, input);
		HrPolicyImpactWarningBuilder.AddWarnings(preview);
		if (isRestoreDraft)
		{
			preview.BlockingWarnings = HrPolicyImpactWarningBuilder.GetBlockingWarnings(preview);
			preview.RequiresConfirmation = false;
		}
		else
		{
			await confirmationService.AttachTokenAsync(preview);
		}
		HrPolicyImpactWarningBuilder.FinalizeSummary(preview);
		return preview;
	}

	public static string NormalizePolicyType(string value) => value.Trim().ToLowerInvariant().Replace("-", "_") switch
	{
		"workcalendar" or "work_calendar" => "work_calendar",
		"payroll" => "payroll",
		"attendance" => "attendance",
		_ => throw new BusinessException(HRErrorCodes.HrPolicyInvalidType)
	};

	private async Task<Guid> ResolveAcademicYearIdAsync(string policyType, Guid? policyId, HrPolicyImpactPreviewRequestDto input)
	{
		if (policyType == "work_calendar" && input.WorkCalendarPolicy?.AcademicYearId is Guid calendarYear && calendarYear != Guid.Empty)
		{
			return calendarYear;
		}
		if (policyType == "payroll" && input.PayrollPolicy?.AcademicYearId is Guid payrollYear && payrollYear != Guid.Empty)
		{
			return payrollYear;
		}
		if (policyType == "attendance" && input.AttendancePolicy?.AcademicYearId is Guid attendanceYear && attendanceYear != Guid.Empty)
		{
			return attendanceYear;
		}
		if (!policyId.HasValue)
		{
			throw new BusinessException(HRErrorCodes.HrPolicyAcademicYearRequiredForImpact);
		}

		return policyType switch
		{
			"work_calendar" => (await workCalendarPolicyRepository.GetAsync(policyId.Value)).AcademicYearId,
			"payroll" => (await payrollPolicyRepository.GetAsync(policyId.Value)).AcademicYearId,
			"attendance" => (await attendancePolicyRepository.GetAsync(policyId.Value)).AcademicYearId,
			_ => throw new BusinessException(HRErrorCodes.HrPolicyInvalidType)
		};
	}

	private async Task FillCurrentVersionAsync(HrPolicyImpactPreviewDto preview)
	{
		if (!preview.PolicyId.HasValue) { return; }

		preview.CurrentVersion = preview.PolicyType switch
		{
			"work_calendar" => (await workCalendarPolicyRepository.GetAsync(preview.PolicyId.Value)).Version,
			"payroll" => (await payrollPolicyRepository.GetAsync(preview.PolicyId.Value)).Version,
			"attendance" => (await attendancePolicyRepository.GetAsync(preview.PolicyId.Value)).Version,
			_ => null
		};
	}

	private async Task<List<HrPolicyAssignment>> GetAffectedAssignmentsAsync(
		string policyType,
		Guid? policyId,
		Guid academicYearId,
		WorkCalendarPolicyReferences workCalendarRefs)
	{
		var query = (await assignmentRepository.GetQueryableAsync())
			.Where(x => x.AcademicYearId == academicYearId && x.IsActive);

		if (!policyId.HasValue) { return await asyncExecuter.ToListAsync(query); }

		query = policyType switch
		{
			"payroll" => query.Where(x => x.PayrollPolicyId == policyId.Value),
			"attendance" => query.Where(x => x.AttendancePolicyId == policyId.Value),
			"work_calendar" when workCalendarRefs.IsEmpty => query.Where(_ => false),
			"work_calendar" => query.Where(x =>
				workCalendarRefs.PayrollPolicyIds.Contains(x.PayrollPolicyId) ||
				workCalendarRefs.AttendancePolicyIds.Contains(x.AttendancePolicyId)),
			_ => query.Where(_ => false)
		};

		return await asyncExecuter.ToListAsync(query);
	}

	private async Task AddProposedWorkCalendarStatusWarningAsync(HrPolicyImpactPreviewDto preview, HrPolicyImpactPreviewRequestDto input)
	{
		var workCalendarId = preview.PolicyType switch
		{
			"payroll" => input.PayrollPolicy?.WorkCalendarPolicyId,
			"attendance" => input.AttendancePolicy?.WorkCalendarPolicyId,
			_ => null
		};

		if (!workCalendarId.HasValue || workCalendarId.Value == Guid.Empty)
		{
			return;
		}

		var workCalendar = await workCalendarPolicyRepository.FindAsync(workCalendarId.Value);
		if (workCalendar == null)
		{
			HrPolicyImpactWarningBuilder.AddMissingWorkCalendarWarning(preview, workCalendarId.Value);
			return;
		}

		if (workCalendar.AcademicYearId != preview.AcademicYearId)
		{
			HrPolicyImpactWarningBuilder.AddMismatchedWorkCalendarWarning(preview, workCalendar);
			return;
		}

		var proposedPolicyStatus = preview.PolicyType switch
		{
			"payroll" when input.PayrollPolicy != null => HrPolicyMapping.ParsePolicyStatus(input.PayrollPolicy.Status),
			"attendance" when input.AttendancePolicy != null => HrPolicyMapping.ParsePolicyStatus(input.AttendancePolicy.Status),
			_ => (HrPolicyStatus?) null
		};

		HrPolicyImpactWarningBuilder.AddWorkCalendarStatusWarning(preview, workCalendar, proposedPolicyStatus);
	}

	private static void AddProposedWorkCalendarDeactivationWarning(
		HrPolicyImpactPreviewDto preview,
		WorkCalendarPolicyReferences workCalendarRefs,
		HrPolicyImpactPreviewRequestDto input)
	{
		if (input.WorkCalendarPolicy == null)
		{
			return;
		}

		var proposedStatus = HrPolicyMapping.ParsePolicyStatus(input.WorkCalendarPolicy.Status);
		HrPolicyImpactWarningBuilder.AddWorkCalendarDeactivationWarning(preview, workCalendarRefs, proposedStatus);
	}
}