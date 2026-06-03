using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp;

namespace ShikshaMitra.HR.ProfessionalManagement;

[Authorize]
[RemoteService(false)]
public partial class ProfessionalManagementAppService(
	ProfessionalManagementAuthorizationService authorization,
	ProfessionalManagementDashboardReader dashboardReader,
	ProfessionalManagementReviewReader reviewReader,
	ProfessionalManagementTemplateWorkflow templateWorkflow,
	ProfessionalManagementCycleWorkflow cycleWorkflow,
	ProfessionalManagementReviewWorkflow reviewWorkflow,
	ProfessionalManagementReviewContributionWorkflow contributionWorkflow,
	ProfessionalManagementOutcomeWorkflow outcomeWorkflow,
	ProfessionalReviewExportService exportService) : ApplicationService, IProfessionalManagementAppService
{
	private readonly ProfessionalManagementAuthorizationService _authorization = authorization;
	private readonly ProfessionalManagementDashboardReader _dashboardReader = dashboardReader;
	private readonly ProfessionalManagementReviewReader _reviewReader = reviewReader;
	private readonly ProfessionalManagementTemplateWorkflow _templateWorkflow = templateWorkflow;
	private readonly ProfessionalManagementCycleWorkflow _cycleWorkflow = cycleWorkflow;
	private readonly ProfessionalManagementReviewWorkflow _reviewWorkflow = reviewWorkflow;
	private readonly ProfessionalManagementReviewContributionWorkflow _contributionWorkflow = contributionWorkflow;
	private readonly ProfessionalManagementOutcomeWorkflow _outcomeWorkflow = outcomeWorkflow;
	private readonly ProfessionalReviewExportService _exportService = exportService;

	private async Task<T> AuthorizedAsync<T>(Func<Task> authorize, Func<Task<T>> action) { await authorize(); return await action(); }
	private async Task<T> AuthorizedAsync<T>(Func<Task> authorize, Func<T> action) { await authorize(); return action(); }
	private async Task AuthorizedAsync(Func<Task> authorize, Func<Task> action) { await authorize(); await action(); }
}
