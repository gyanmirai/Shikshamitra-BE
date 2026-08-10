using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmNumberSeriesService(
	AdmissionCrmNumberSeriesWorkflow numberSeries,
	AdmissionCrmPlacementOptionsWorkflow placementOptions) : ApplicationService, IAdmissionCrmNumberSeriesService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageNumberSeries)]
	public Task<AdmissionCrmNumberSeriesDto> CreateNumberSeriesAsync(Guid yearId, CreateAdmissionCrmNumberSeriesDto input) => numberSeries.CreateAsync(yearId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewActivation)]
	public Task<IReadOnlyList<AdmissionCrmNumberSeriesDto>> GetNumberSeriesAsync(Guid yearId) => numberSeries.GetListAsync(yearId);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewActivation)]
	public Task<IReadOnlyList<AdmissionCrmPlacementOptionDto>> GetPlacementOptionsAsync(Guid yearId) => placementOptions.GetAsync(yearId);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageNumberSeries)]
	public Task<AdmissionCrmNumberSeriesDto> SubmitNumberSeriesAsync(Guid id) => numberSeries.SubmitAsync(id);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.PublishNumberSeries)]
	public Task<AdmissionCrmNumberSeriesDto> PublishNumberSeriesAsync(Guid id) => numberSeries.PublishAsync(id);
}

[Authorize]
[RemoteService(false)]
public class AdmissionCrmIdentifierService(AdmissionCrmIdentifierWorkflow workflow)
	: ApplicationService, IAdmissionCrmIdentifierService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.AssignManualIdentifier)]
	public Task<AdmissionCrmIdentifierAllocationDto> AssignManualIdentifierAsync(Guid id, AssignManualAdmissionCrmIdentifierDto input) => workflow.AssignManualAsync(id, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewActivation)]
	public Task<AdmissionCrmIdentifierGovernanceReportDto> GetIdentifierGovernanceReportAsync(Guid yearId, Guid seriesId) => workflow.GetGovernanceAsync(yearId, seriesId);
}

[Authorize]
[RemoteService(false)]
public class AdmissionCrmActivationLifecycleService(AdmissionCrmActivationLifecycleWorkflow workflow)
	: ApplicationService, IAdmissionCrmActivationLifecycleService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.PreviewActivation)]
	public Task<AdmissionCrmActivationPreviewDto> PreviewAsync(Guid leadId, PreviewAdmissionCrmActivationDto input) => workflow.PreviewAsync(leadId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.RequestActivation)]
	public Task<AdmissionCrmActivationDto> RequestAsync(Guid leadId, RequestAdmissionCrmActivationDto input) => workflow.RequestAsync(leadId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ApproveActivation)]
	public Task<AdmissionCrmActivationDto> ApproveAsync(Guid id) => workflow.ApproveAsync(id);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewActivation)]
	public Task<AdmissionCrmActivationDto?> GetByLeadAsync(Guid leadId) => workflow.GetByLeadAsync(leadId);
}

[Authorize]
[RemoteService(false)]
public class AdmissionCrmActivationExecutionService(AdmissionCrmActivationExecutionWorkflow workflow)
	: ApplicationService, IAdmissionCrmActivationExecutionService
{
	[Authorize]
	public Task<AdmissionCrmActivationDto> ExecuteAsync(Guid id) => workflow.ExecuteAsync(id);
}

[Authorize]
[RemoteService(false)]
public class AdmissionCrmActivationCancellationService(AdmissionCrmActivationCancellationWorkflow workflow)
	: ApplicationService, IAdmissionCrmActivationCancellationService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.CancelAdmission)]
	public Task<AdmissionCrmActivationCancellationPreviewDto> PreviewCancellationAsync(Guid id) => workflow.PreviewAsync(id);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.CancelAdmission)]
	public Task<AdmissionCrmActivationDto> RequestCancellationAsync(Guid id, RequestAdmissionCrmActivationCancellationDto input) => workflow.RequestAsync(id, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.CancelAdmission)]
	public Task<AdmissionCrmActivationDto> ApproveCancellationAsync(Guid id) => workflow.ApproveAsync(id);
}

[Authorize]
[RemoteService(false)]
public class AdmissionCrmServiceHandoffService(AdmissionCrmServiceHandoffWorkflow workflow)
	: ApplicationService, IAdmissionCrmServiceHandoffService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewActivation)]
	public Task<IReadOnlyList<AdmissionCrmServiceHandoffDto>> GetServiceHandoffsAsync(Guid id) => workflow.GetAsync(id);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageServiceHandoffs)]
	public Task<AdmissionCrmServiceHandoffDto> UpdateServiceHandoffAsync(Guid id, UpdateAdmissionCrmServiceHandoffDto input) => workflow.UpdateAsync(id, input);
}
