using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;

namespace ShikshaMitra.HR.ProfessionalManagement;

public partial class ProfessionalManagementAppService
{
	public Task<PerformanceDashboardDto> GetDashboardAsync() =>
		AuthorizedAsync(_authorization.EnsureViewAsync, () => _dashboardReader.GetDashboardAsync(Clock.Now.Date));

	public Task<List<RatingDistributionDto>> GetRatingDistributionAsync(Guid? cycleId = null) =>
		AuthorizedAsync(_authorization.EnsureViewAsync, () => _dashboardReader.GetRatingDistributionAsync(cycleId));

	public Task<List<PendingActionDto>> GetPendingActionsAsync(Guid? cycleId = null) =>
		AuthorizedAsync(_authorization.EnsureViewAsync, () => _dashboardReader.GetPendingActionsAsync(cycleId));

	public Task<List<DevelopmentPlanDto>> GetTrainingNeedsAsync(Guid? cycleId = null) =>
		AuthorizedAsync(_authorization.EnsureViewAsync, () => _dashboardReader.GetTrainingNeedsAsync(cycleId));

	public Task<List<PerformanceOutcomeDto>> GetIncrementRecommendationsAsync(Guid? cycleId = null) =>
		AuthorizedAsync(EnsureConfidentialOutcomeAccessAsync, () => _dashboardReader.GetIncrementRecommendationsAsync(cycleId));

	public async Task<List<PerformanceAuditEventDto>> GetAuditTrailAsync(Guid? reviewId = null)
	{
		await EnsureAuditAccessAsync();
		if (reviewId.HasValue)
		{
			await _authorization.EnsureReviewExistsAsync(reviewId.Value);
		}

		return await _reviewReader.GetAuditTrailAsync(reviewId);
	}

	public Task<byte[]> GetReviewPdfAsync(Guid id) =>
		AuthorizedAsync(
			async () =>
			{
				await _authorization.EnsureAnyPermissionAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.ExportReports, ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.View);
				await _authorization.EnsureCanViewReviewAsync(id);
			},
			() => _exportService.GetReviewPdfAsync(id));

	private Task EnsureConfidentialOutcomeAccessAsync() =>
		_authorization.EnsureAnyPermissionAsync(
			ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.ViewConfidentialOutcome,
			ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.View);

	private Task EnsureAuditAccessAsync() =>
		_authorization.EnsureAnyPermissionAsync(
			ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.ViewAudit,
			ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.View);
}
