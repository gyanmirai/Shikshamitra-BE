using System;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;

namespace ShikshaMitra.HR.ProfessionalManagement;

public partial class ProfessionalManagementAppService
{
	public Task<ProfessionalReviewListResponse> GetCycleReviewsAsync(Guid id, string? search = null) =>
		AuthorizedAsync(_authorization.EnsureViewAsync, () => _reviewReader.GetReviewsAsync(id, null, null, search));

	public Task<ProfessionalReviewListResponse> GetReviewsAsync(Guid? cycleId = null, Guid? employeeId = null, ProfessionalReviewStatus? status = null, string? search = null) =>
		AuthorizedAsync(_authorization.EnsureViewAsync, () => _reviewReader.GetReviewsAsync(cycleId, employeeId, status, search));

	public Task<ProfessionalReviewDto> GetReviewAsync(Guid id) =>
		AuthorizedAsync(() => _authorization.EnsureCanViewReviewAsync(id), () => _reviewReader.GetReviewAsync(id));

	public Task<ProfessionalReviewDto> SaveDraftAsync(Guid id, ProfessionalReviewSaveRequest input) =>
		AuthorizedAsync(() => _authorization.EnsureCanSelfReviewAsync(id), () => _reviewWorkflow.SaveDraftAsync(id, input));

	public Task<ProfessionalReviewDto> SubmitAsync(Guid id, ProfessionalReviewSaveRequest input) =>
		AuthorizedAsync(() => _authorization.EnsureCanSelfReviewAsync(id), () => _reviewWorkflow.SubmitAsync(id, input));

	public async Task<ProfessionalReviewDto> SaveSelfAppraisalAsync(Guid id, PerformanceReviewStageRequest input)
	{
		await _authorization.EnsureCanSelfReviewAsync(id);
		input.StageType = PerformanceReviewStageType.SelfAppraisal;
		return await _reviewWorkflow.SaveStageAsync(id, input, false);
	}

	public async Task<ProfessionalReviewDto> SaveReviewerStageAsync(Guid id, PerformanceReviewStageRequest input)
	{
		await _authorization.EnsureCanReviewEmployeeAsync(id);
		if (input.StageType == PerformanceReviewStageType.SelfAppraisal) input.StageType = PerformanceReviewStageType.ReportingManager;
		return await _reviewWorkflow.SaveStageAsync(id, input, false);
	}

	public async Task<ProfessionalReviewDto> SubmitStageAsync(Guid id, PerformanceReviewStageRequest input)
	{
		if (input.StageType == PerformanceReviewStageType.SelfAppraisal) await _authorization.EnsureCanSelfReviewAsync(id);
		else await _authorization.EnsureCanReviewEmployeeAsync(id);
		return await _reviewWorkflow.SaveStageAsync(id, input, true);
	}

	public Task<ProfessionalReviewDto> ReopenReviewAsync(Guid id, ReopenReviewRequest input) =>
		AuthorizedAsync(() => _authorization.EnsureCanManageOutcomeAsync(id), () => _outcomeWorkflow.ReopenReviewAsync(id, input));

	public Task<PerformanceOutcomeDto> SaveFinalOutcomeAsync(Guid id, PerformanceOutcomeRequest input) =>
		AuthorizedAsync(() => _authorization.EnsureCanManageOutcomeAsync(id), () => _outcomeWorkflow.SaveFinalOutcomeAsync(id, input));

	public Task<ProfessionalReviewListResponse> GetEmployeeReviewHistoryAsync(Guid employeeId) =>
		AuthorizedAsync(() => _authorization.EnsureCanViewEmployeeHistoryAsync(employeeId), () => _reviewReader.GetEmployeeReviewHistoryAsync(employeeId));
}
