using System;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;

namespace ShikshaMitra.HR.ProfessionalManagement;

public partial class ProfessionalManagementAppService
{
	public Task<PerformanceTemplateListResponse> GetTemplatesAsync(int page = 1, int pageSize = 20, string? search = null, PerformanceStaffCategory? staffCategory = null) =>
		AuthorizedAsync(_authorization.EnsureViewAsync, () => _templateWorkflow.GetTemplatesAsync(page, pageSize, search, staffCategory));
	public Task<PerformanceTemplateDto> CreateTemplateAsync(PerformanceTemplateRequest input) =>
		AuthorizedAsync(EnsureTemplateManagementAsync, () => _templateWorkflow.CreateTemplateAsync(input));
	public Task<PerformanceTemplateDto> UpdateTemplateAsync(Guid id, PerformanceTemplateRequest input) =>
		AuthorizedAsync(EnsureTemplateManagementAsync, () => _templateWorkflow.UpdateTemplateAsync(id, input));
	public Task<PerformanceTemplateDto> PublishTemplateAsync(Guid id) =>
		AuthorizedAsync(EnsureTemplateManagementAsync, () => _templateWorkflow.PublishTemplateAsync(id));
	public Task<PerformanceTemplateDto> CloneTemplateAsync(Guid id) =>
		AuthorizedAsync(EnsureTemplateManagementAsync, () => _templateWorkflow.CloneTemplateAsync(id));
	public Task<PerformanceTemplateDto> ArchiveTemplateAsync(Guid id) =>
		AuthorizedAsync(EnsureTemplateManagementAsync, () => _templateWorkflow.ArchiveTemplateAsync(id));
	public Task<DefaultPerformanceTemplatesResponse> GetDefaultTemplatesAsync() =>
		AuthorizedAsync(_authorization.EnsureViewAsync, _templateWorkflow.GetDefaultTemplates);
	private Task EnsureTemplateManagementAsync() =>
		_authorization.EnsureAnyPermissionAsync(
			ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.ManageTemplates,
			ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.Add);
}
