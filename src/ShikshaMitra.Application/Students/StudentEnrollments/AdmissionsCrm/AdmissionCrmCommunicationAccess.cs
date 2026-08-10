using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmCommunicationAccess(
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	StudentScopeAccessService scopeAccess)
	: ApplicationService, ITransientDependency
{
	public Task EnsureYearAsync(Guid academicYearId, string operation) =>
		scopeAccess.EnsureCanAccessAcademicYearAsync(academicYearId, operation);

	public Task<bool> HasYearWideScopeAsync(Guid academicYearId) =>
		scopeAccess.CurrentUserHasAcademicYearWideScopeAsync(academicYearId);

	public async Task<AdmissionInquiryLead> GetLeadAsync(Guid id, string operation, bool enforceUserScope = true)
	{
		var lead = await leadRepository.GetAsync(id);
		if (lead.TenantId != CurrentTenant.Id)
			throw new BusinessException("AdmissionCrm:LeadNotFound").WithData("LeadId", id);
		if (enforceUserScope) await scopeAccess.EnsureCanAccessAdmissionLeadAsync(lead, operation);
		return lead;
	}

	public async Task EnsureLeadBelongsToYearAsync(Guid? leadId, Guid academicYearId, string operation)
	{
		if (!leadId.HasValue)
		{
			if (await HasYearWideScopeAsync(academicYearId)) return;
			throw new AbpAuthorizationException("An unassigned communication record requires academic-year-wide scope.");
		}
		var lead = await GetLeadAsync(leadId.Value, operation);
		if (lead.AcademicYearId != academicYearId)
			throw new BusinessException("AdmissionCrm:LeadAcademicYearMismatch")
				.WithData("LeadId", leadId.Value).WithData("AcademicYearId", academicYearId);
	}

	public async Task<List<Guid>> GetScopedLeadIdsAsync(Guid academicYearId)
	{
		var queryable = await leadRepository.GetQueryableAsync();
		var query = queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId);
		query = await scopeAccess.ApplyCurrentUserAdmissionLeadScopeAsync(query);
		return await AsyncExecuter.ToListAsync(query.Select(x => x.Id));
	}
}
