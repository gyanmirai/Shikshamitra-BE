using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Policies;

public interface IWorkCalendarPolicyAppService : IApplicationService
{
	Task<PagedResultDto<WorkCalendarPolicyDto>> GetListAsync(PolicyListRequestDto input);
	Task<WorkCalendarPolicyDto> GetAsync(Guid id);
	Task<List<WorkCalendarHolidayDto>> GetHolidaysAsync(Guid id);
	Task<List<WorkCalendarHolidayDto>> GetHolidayImportPreviewAsync(Guid academicYearId);
	Task<WorkCalendarPolicyDto> CreateAsync(CreateUpdateWorkCalendarPolicyDto input);
	Task<WorkCalendarPolicyDto> UpdateAsync(Guid id, CreateUpdateWorkCalendarPolicyDto input);
	Task<WorkCalendarPolicyDto> CloneAsync(Guid id, HrPolicyCloneRequestDto input);
	Task DeleteAsync(Guid id);
	Task<WorkCalendarPolicyDto> UnarchiveAsync(Guid id);
}

public interface IPayrollPolicyAppService : IApplicationService
{
	Task<PagedResultDto<PayrollPolicyDto>> GetListAsync(PolicyListRequestDto input);
	Task<PayrollPolicyDto> GetAsync(Guid id);
	Task<PayrollPolicyDto> CreateAsync(CreateUpdatePayrollPolicyDto input);
	Task<PayrollPolicyDto> UpdateAsync(Guid id, CreateUpdatePayrollPolicyDto input);
	Task<PayrollPolicyDto> CloneAsync(Guid id, HrPolicyCloneRequestDto input);
	Task DeleteAsync(Guid id);
	Task<PayrollPolicyDto> UnarchiveAsync(Guid id);
	Task<PayrollPolicyDto> SetDefaultAsync(Guid id);
}

public interface IAttendancePolicyAppService : IApplicationService
{
	Task<PagedResultDto<AttendancePolicyDto>> GetListAsync(PolicyListRequestDto input);
	Task<AttendancePolicyDto> GetAsync(Guid id);
	Task<AttendancePolicyDto> CreateAsync(CreateUpdateAttendancePolicyDto input);
	Task<AttendancePolicyDto> UpdateAsync(Guid id, CreateUpdateAttendancePolicyDto input);
	Task<AttendancePolicyDto> CloneAsync(Guid id, HrPolicyCloneRequestDto input);
	Task DeleteAsync(Guid id);
	Task<AttendancePolicyDto> UnarchiveAsync(Guid id);
	Task<AttendancePolicyDto> SetDefaultAsync(Guid id);
}

public interface IHrPolicyAssignmentAppService : IApplicationService
{
	Task<PagedResultDto<HrPolicyAssignmentDto>> GetListAsync(PolicyListRequestDto input);
	Task<HrPolicyAssignmentDto> GetAsync(Guid id);
	Task<HrPolicyAssignmentDto> CreateAsync(CreateUpdateHrPolicyAssignmentDto input);
	Task<HrPolicyAssignmentDto> UpdateAsync(Guid id, CreateUpdateHrPolicyAssignmentDto input);
	Task<HrPolicyAssignmentDto> UpsertEmployeeOverrideAsync(UpsertEmployeePolicyOverrideDto input);
	Task<HrPolicyImpactPreviewDto> PreviewImpactAsync(HrPolicyAssignmentImpactPreviewRequestDto input);
	Task DeleteAsync(Guid id);
	Task<EffectiveEmployeePoliciesDto> GetEffectivePoliciesAsync(Guid employeeId, DateTime date);
	Task<EffectiveEmployeePoliciesDto> GetPreviewAsync(PolicyAssignmentPreviewRequestDto input);
}

public interface IHrPolicySafetyAppService : IApplicationService
{
	Task<HrPolicyImpactPreviewDto> PreviewPolicyImpactAsync(HrPolicyImpactPreviewRequestDto input);
	Task<HrPolicyAuditSummaryDto> GetAuditSummaryAsync(string policyType, Guid id);
}

public interface IHrPolicyVersionAppService : IApplicationService
{
	Task<PagedResultDto<HrPolicyVersionDto>> GetListAsync(string policyType, Guid policyId, PagedAndSortedResultRequestDto input);
	Task<HrPolicyVersionDto> GetAsync(string policyType, Guid policyId, Guid versionId);
	Task<HrPolicyVersionCompareDto> CompareAsync(string policyType, Guid policyId, Guid fromVersionId, Guid toVersionId);
	Task<HrPolicyImpactPreviewDto> GetRestorePreviewAsync(string policyType, Guid policyId, Guid versionId);
	Task<object> RestoreDraftAsync(string policyType, Guid policyId, Guid versionId, HrPolicyRestoreDraftRequestDto input);
}

public interface IPolicyHolidayImpactAppService : IApplicationService
{
	Task<PolicyHolidayImpactPreviewDto> GetPreviewAsync(
		Guid academicYearId,
		Guid? workCalendarPolicyId,
		Guid? payrollPolicyId,
		Guid? attendancePolicyId,
		DateTime? fromDate,
		DateTime? toDate,
		string? month);
}
