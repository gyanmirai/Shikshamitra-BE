using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmDocumentAccessReader(
	IRepository<AdmissionCrmDocumentChecklistAssignment, Guid> assignmentRepository,
	IRepository<AdmissionCrmApplicantDocument, Guid> documentRepository,
	IRepository<AdmissionCrmRegistrationApplication, Guid> applicationRepository,
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	StudentScopeAccessService studentScopeAccessService)
	: ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmDocumentChecklistAssignment?> GetAssignmentByLeadOrDefaultAsync(Guid leadId)
	{
		var queryable = await assignmentRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == leadId));
	}

	public async Task<AdmissionCrmDocumentChecklistAssignment> GetAssignmentByApplicationAsync(Guid applicationId)
	{
		var queryable = await assignmentRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.RegistrationApplicationId == applicationId))
		       ?? throw new BusinessException("AdmissionCrm:DocumentChecklistNotAssigned");
	}

	public async Task<AdmissionCrmDocumentChecklistAssignment> GetAssignmentAsync(Guid id, string operation)
	{
		var assignment = await assignmentRepository.GetAsync(id);
		if (assignment.TenantId != CurrentTenant.Id) throw new BusinessException("AdmissionCrm:DocumentChecklistAssignmentNotFound");
		var lead = await leadRepository.GetAsync(assignment.LeadId);
		await studentScopeAccessService.EnsureCanAccessAdmissionLeadAsync(lead, operation);
		return assignment;
	}

	public async Task<AdmissionCrmRegistrationApplication> GetApplicationAsync(Guid id, string operation)
	{
		var application = await applicationRepository.GetAsync(id);
		if (application.TenantId != CurrentTenant.Id) throw new BusinessException("AdmissionCrm:RegistrationApplicationNotFound");
		var lead = await leadRepository.GetAsync(application.LeadId);
		await studentScopeAccessService.EnsureCanAccessAdmissionLeadAsync(lead, operation);
		return application;
	}

	public async Task<AdmissionCrmApplicantDocument> GetDocumentAsync(Guid id, string operation)
	{
		var document = await documentRepository.GetAsync(id);
		if (document.TenantId != CurrentTenant.Id || document.SupersededAt.HasValue)
			throw new BusinessException("AdmissionCrm:ApplicantDocumentNotFound");
		var lead = await leadRepository.GetAsync(document.LeadId);
		await studentScopeAccessService.EnsureCanAccessAdmissionLeadAsync(lead, operation);
		return document;
	}

	public async Task<AdmissionCrmApplicantDocument?> GetCurrentDocumentOrDefaultAsync(Guid assignmentId, string requirementKey)
	{
		var queryable = await documentRepository.GetQueryableAsync();
		var documents = await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.ChecklistAssignmentId == assignmentId && x.RequirementKey == requirementKey));
		return documents.Where(x => !x.SupersededAt.HasValue).OrderByDescending(x => x.Version).FirstOrDefault();
	}
}
