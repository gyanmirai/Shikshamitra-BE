using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

[RemoteService(false)]
public class RunPayrollAppService(
	PayrollLockWorkflow lockWorkflow,
	PayrollPreviewWorkflow previewWorkflow,
	PayrollReviewWorkflow reviewWorkflow,
	PayrollAdjustmentAppWorkflow adjustmentWorkflow) : ApplicationService, IRunPayrollAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.View)]
	public Task<RunPayrollResponse> GetPreviewAsync(GetRunPayrollInput input) =>
		previewWorkflow.GetPreviewAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.View)]
	public Task<RunPayrollReadinessSummary> GetReadinessAsync(string month, Guid? academicYearId = null) =>
		previewWorkflow.GetReadinessAsync(month, academicYearId);

	[UnitOfWork]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.Lock)]
	public Task<PayrollLockResponse> LockPayrollAsync(RunPayrollRequest request) =>
		lockWorkflow.LockAsync(request);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.Add)]
	public Task<PayrollLockResponse> MarkReviewedAsync(PayrollRunReviewRequest request) =>
		reviewWorkflow.MarkReviewedAsync(request);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.Add)]
	public Task<PayrollLockResponse> UnmarkReviewedAsync(PayrollRunReviewRequest request) =>
		reviewWorkflow.UnmarkReviewedAsync(request);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.View)]
	public Task<List<PayrollAdjustmentDto>> GetAdjustmentsAsync(string month, Guid? employeeId = null, Guid? academicYearId = null) =>
		adjustmentWorkflow.GetAdjustmentsAsync(month, employeeId, academicYearId);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.Adjustments)]
	public Task<PayrollAdjustmentDto> CreateAdjustmentAsync(PayrollAdjustmentRequest request) =>
		adjustmentWorkflow.CreateAdjustmentAsync(request);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.Adjustments)]
	public Task<PayrollAdjustmentDto> UpdateAdjustmentAsync(Guid id, PayrollAdjustmentRequest request) =>
		adjustmentWorkflow.UpdateAdjustmentAsync(id, request);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.Adjustments)]
	public Task DeleteAdjustmentAsync(Guid id) =>
		adjustmentWorkflow.DeleteAdjustmentAsync(id);
}
