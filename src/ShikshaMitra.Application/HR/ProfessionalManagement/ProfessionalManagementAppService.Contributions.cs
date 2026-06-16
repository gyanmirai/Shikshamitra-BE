using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShikshaMitra.HR.ProfessionalManagement;

public partial class ProfessionalManagementAppService
{
	public async Task<ClassroomObservationDto> AddObservationAsync(Guid id, ClassroomObservationRequest input)
	{
		var observerEmployeeId = Guid.Empty;
		return await AuthorizedAsync(
			async () => observerEmployeeId = await _authorization.ResolveObservationObserverEmployeeIdAsync(id, input.ObserverEmployeeId),
			() => _contributionWorkflow.AddObservationAsync(id, input, observerEmployeeId));
	}

	public Task<List<ClassroomObservationDto>> GetObservationsAsync(Guid id) =>
		AuthorizedAsync(() => _authorization.EnsureCanViewReviewAsync(id), () => _contributionWorkflow.GetObservationsAsync(id));

	public Task<List<PerformanceEvidenceDto>> GetEvidenceAsync(Guid id) =>
		AuthorizedAsync(() => _authorization.EnsureCanViewReviewAsync(id), () => _reviewReader.GetEvidenceAsync(id));

	public Task<PerformanceEvidenceDto> AddEvidenceAsync(Guid id, PerformanceEvidenceRequest input) =>
		AuthorizedAsync(() => _authorization.EnsureCanUploadEvidenceAsync(id), () => _contributionWorkflow.AddEvidenceAsync(id, input));

	public Task DeleteEvidenceAsync(Guid id) =>
		AuthorizedAsync(() => _authorization.EnsureCanDeleteEvidenceAsync(id), () => _contributionWorkflow.DeleteEvidenceAsync(id));

	public Task<PerformanceFeedbackDto> AddFeedbackAsync(Guid id, PerformanceFeedbackRequest input) =>
		AuthorizedAsync(() => _authorization.EnsureCanReviewEmployeeAsync(id), () => _contributionWorkflow.AddFeedbackAsync(id, input));

	public Task<List<PerformanceFeedbackDto>> GetFeedbackAsync(Guid id) =>
		AuthorizedAsync(() => _authorization.EnsureCanViewReviewAsync(id), () => _contributionWorkflow.GetFeedbackAsync(id));

	public Task<DevelopmentPlanDto> AddDevelopmentPlanAsync(Guid id, DevelopmentPlanRequest input) =>
		AuthorizedAsync(() => _authorization.EnsureCanManageOutcomeAsync(id), () => _outcomeWorkflow.AddDevelopmentPlanAsync(id, input));

	public Task<DevelopmentPlanDto> UpdateDevelopmentPlanAsync(Guid id, DevelopmentPlanRequest input) =>
		AuthorizedAsync(() => _authorization.EnsureCanManageOutcomeAsync(id), () => _outcomeWorkflow.UpdateDevelopmentPlanAsync(id, input));
}
