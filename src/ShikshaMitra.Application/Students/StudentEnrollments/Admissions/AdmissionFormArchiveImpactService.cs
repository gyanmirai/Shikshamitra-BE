using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>On-demand lifecycle impact without changing links or retained submissions.</summary>
[RemoteService(false)]
public class AdmissionFormArchiveImpactService(
	IRepository<AdmissionFormDefinition, Guid> forms,
	IRepository<AdmissionOffering, Guid> offerings,
	IRepository<AdmissionWorkflowDefinition, Guid> workflows,
	IRepository<AdmissionWorkflowStageDefinition, Guid> stages,
	IRepository<AdmissionPublicInquiryLink, Guid> publicLinks,
	AdmissionApplicationScopeAccessService applicationScope,
	AdmissionInquiryScopeAccessService inquiryScope,
	IPermissionChecker permissions) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionFormArchiveImpactDto> ReadAsync(Guid id)
	{
		var form = await forms.FindAsync(item => item.Id == id && item.TenantId == CurrentTenant.Id)
			?? throw new EntityNotFoundException(typeof(AdmissionFormDefinition), id);
		var offeringsVisible = await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View);
		var workflowsVisible = await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.View);
		var formsVisible = await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.View);
		var inquiriesVisible = await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.View);
		var applicationsVisible = await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View);
		var result = new AdmissionFormArchiveImpactDto
		{
			FormDefinitionId = form.Id, FormName = form.Name, Purpose = form.Purpose,
			Status = form.Status, ConcurrencyStamp = form.ConcurrencyStamp,
			HasRestrictedDependencies = !offeringsVisible || !workflowsVisible || !formsVisible || !inquiriesVisible || !applicationsVisible
		};
		var tenantWorkflows = (await workflows.GetQueryableAsync()).Where(item => item.TenantId == CurrentTenant.Id);
		var formIdText = id.ToString("D");
		var compactFormIdText = id.ToString("N");
		var stageRows = !workflowsVisible ? [] : await AsyncExecuter.ToListAsync((await stages.GetQueryableAsync()).Where(stage =>
			stage.StageType == AdmissionWorkflowStageType.Form && stage.ConfigurationJson != null &&
			(stage.ConfigurationJson.ToLower().Contains(formIdText) || stage.ConfigurationJson.ToLower().Contains(compactFormIdText)) &&
			tenantWorkflows.Any(workflow => workflow.Id == stage.WorkflowDefinitionId)));
		var linkedStages = stageRows.Where(stage => AdmissionFormStageAccessService.ReadFormDefinitionId(stage.ConfigurationJson) == id).ToList();
		var linkedStageWorkflowIds = linkedStages.Select(stage => stage.WorkflowDefinitionId).Distinct().ToArray();
		var workflowRows = !workflowsVisible ? [] : await AsyncExecuter.ToListAsync(tenantWorkflows.Where(item => item.DefaultInquiryFormDefinitionId == id || Enumerable.Contains(linkedStageWorkflowIds, item.Id)));
		foreach (var workflow in workflowRows)
		{
			if (workflow.DefaultInquiryFormDefinitionId == id)
				result.Dependencies.Add(new() { Kind = "workflow", Id = workflow.Id, Name = workflow.Name });
			foreach (var stage in linkedStages.Where(stage => stage.WorkflowDefinitionId == workflow.Id))
				result.Dependencies.Add(new()
				{
					Kind = "workflow", Id = workflow.Id, Name = workflow.Name,
					WorkflowStageId = stage.Id, WorkflowStageName = stage.Name
				});
		}
		result.WorkflowCount = result.Dependencies.Where(item => item.Kind == "workflow").Select(item => item.Id).Distinct().Count();
		var linkedOfferings = (await offerings.GetQueryableAsync()).Where(item => item.TenantId == CurrentTenant.Id &&
			(item.InquiryFormDefinitionId == id || item.ApplicationFormDefinitionId == id ||
				item.WorkflowDefinitionId.HasValue && Enumerable.Contains(linkedStageWorkflowIds, item.WorkflowDefinitionId.Value)));
		var scopedOfferings = await applicationScope.GetScopedOfferingsAsync();
		var visibleOfferings = !offeringsVisible ? [] : await AsyncExecuter.ToListAsync(linkedOfferings.Where(item => scopedOfferings.Any(allowed => allowed.Id == item.Id)));
		result.OfferingCount = visibleOfferings.Count;
		result.HasRestrictedOfferings = offeringsVisible
			? await AsyncExecuter.AnyAsync(linkedOfferings.Where(item => !scopedOfferings.Any(allowed => allowed.Id == item.Id)))
			: await AsyncExecuter.AnyAsync(linkedOfferings);
		result.Dependencies.AddRange(visibleOfferings.Select(item => new AdmissionFormArchiveDependencyDto
		{
			Kind = "offering", Id = item.Id, Name = item.Name, IsActive = item.IsActive
		}));
		var links = !offeringsVisible ? [] : await publicLinks.GetListAsync(item => item.TenantId == CurrentTenant.Id && item.InquiryFormDefinitionId == id);
		result.PublicLinkCount = links.Count;
		result.Dependencies.AddRange(links.Select(item => new AdmissionFormArchiveDependencyDto
		{
			Kind = "publicLink", Id = item.Id, Name = item.Name, IsActive = item.IsActive
		}));
		if (formsVisible && form.Purpose == AdmissionFormPurpose.Inquiry)
		{
			var registrations = await forms.GetListAsync(item => item.TenantId == CurrentTenant.Id && item.Purpose == AdmissionFormPurpose.Registration);
			result.Dependencies.AddRange(registrations.Where(item =>
				AdmissionFormInheritanceMetadataStore.ReadPolicy(item) is { Enabled: true } policy &&
				policy.Sources.Any(source => source.InquiryFormDefinitionId == id))
				.Select(item => new AdmissionFormArchiveDependencyDto { Kind = "registrationForm", Id = item.Id, Name = item.Name }));
		}
		if (applicationsVisible)
			result.ApplicationCount = await AsyncExecuter.CountAsync((await applicationScope.GetScopedApplicationsAsync()).Where(item => item.FormDefinitionId == id));
		// The retained form ID is stored in ExtraProperties rather than a mapped column.
		// Read scoped metadata; never return answers or student identities in an impact preview.
		if (inquiriesVisible && form.Purpose == AdmissionFormPurpose.Inquiry)
		{
			var inquiryQuery = await inquiryScope.GetScopedQueryAsync();
			const int batchSize = 200;
			for (var skip = 0; ; skip += batchSize)
			{
				var retained = await AsyncExecuter.ToListAsync(inquiryQuery.OrderBy(item => item.Id).Skip(skip).Take(batchSize).Select(item => item.ExtraProperties));
				result.InquiryCount += retained.Count(properties => properties.TryGetValue("AdmissionFormResponseDefinitionId", out var value) &&
					Guid.TryParse(value?.ToString(), out var formId) && formId == id);
				if (retained.Count < batchSize) break;
			}
		}
		ApplyVisibility(result, offeringsVisible, workflowsVisible, formsVisible, inquiriesVisible, applicationsVisible);
		return result;
	}

	internal static void ApplyVisibility(AdmissionFormArchiveImpactDto impact, bool offeringsVisible, bool workflowsVisible,
		bool formsVisible, bool inquiriesVisible, bool applicationsVisible)
	{
		var removed = impact.Dependencies.RemoveAll(item => item.Kind switch
		{
			"offering" or "publicLink" => !offeringsVisible,
			"workflow" => !workflowsVisible,
			"registrationForm" => !formsVisible,
			_ => true
		});
		impact.HasRestrictedDependencies |= impact.HasRestrictedOfferings || removed > 0;
		if (!offeringsVisible)
		{
			impact.HasRestrictedOfferings |= impact.OfferingCount > 0;
			impact.OfferingCount = 0;
			impact.PublicLinkCount = 0;
		}
		if (!workflowsVisible) impact.WorkflowCount = 0;
		if (!inquiriesVisible)
		{
			impact.HasRestrictedDependencies |= impact.InquiryCount > 0;
			impact.InquiryCount = 0;
		}
		if (!applicationsVisible)
		{
			impact.HasRestrictedDependencies |= impact.ApplicationCount > 0;
			impact.ApplicationCount = 0;
		}
	}
}
