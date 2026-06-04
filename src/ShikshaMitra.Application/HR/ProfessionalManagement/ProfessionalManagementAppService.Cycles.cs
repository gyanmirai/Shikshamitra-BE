using System;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;

namespace ShikshaMitra.HR.ProfessionalManagement;

public partial class ProfessionalManagementAppService
{
	public Task<ProfessionalReviewCycleListResponse> GetReviewCyclesAsync(int page = 1, int pageSize = 20, string? search = null) =>
		AuthorizedAsync(_authorization.EnsureViewAsync, () => _cycleWorkflow.GetReviewCyclesAsync(page, pageSize, search));
	public Task<ProfessionalReviewCycleDto> CreateReviewCycleAsync(ProfessionalReviewCycleRequest input) =>
		AuthorizedAsync(EnsureCycleManagementAsync, () => _cycleWorkflow.CreateReviewCycleAsync(input));
	public Task<ProfessionalReviewCycleDto> UpdateReviewCycleAsync(Guid id, ProfessionalReviewCycleRequest input) =>
		AuthorizedAsync(EnsureCycleManagementAsync, () => _cycleWorkflow.UpdateReviewCycleAsync(id, input));
	public Task<ProfessionalReviewCycleDto> PublishReviewCycleAsync(Guid id) =>
		AuthorizedAsync(EnsureCycleManagementAsync, () => _cycleWorkflow.PublishReviewCycleAsync(id));
	public Task<ProfessionalReviewCycleDto> AssignEmployeesAsync(Guid id, AssignEmployeesRequest input) =>
		AuthorizedAsync(
			() => _authorization.EnsureAnyPermissionAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.AssignReviews, ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.Add),
			() => _cycleWorkflow.AssignEmployeesAsync(id, input));
	public Task<ProfessionalReviewCycleDto> MoveToCalibrationAsync(Guid id) =>
		AuthorizedAsync(
			() => _authorization.EnsureAnyPermissionAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.Calibrate, ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.Add),
			() => _cycleWorkflow.MoveToCalibrationAsync(id));
	public Task<ProfessionalReviewCycleDto> FinalizeCycleAsync(Guid id) =>
		AuthorizedAsync(
			() => _authorization.EnsureAnyPermissionAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.Finalize, ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.Add),
			() => _cycleWorkflow.FinalizeCycleAsync(id));
	public Task<ProfessionalReviewCycleDto> ArchiveCycleAsync(Guid id) =>
		AuthorizedAsync(EnsureCycleManagementAsync, () => _cycleWorkflow.ArchiveCycleAsync(id));
	private Task EnsureCycleManagementAsync() =>
		_authorization.EnsureAnyPermissionAsync(
			ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.ManageCycles,
			ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.Add);
}
