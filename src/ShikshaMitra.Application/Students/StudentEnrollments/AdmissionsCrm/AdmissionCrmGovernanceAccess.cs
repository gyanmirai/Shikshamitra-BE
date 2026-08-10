using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmGovernanceAccess(
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	IRepository<AcademicYear, Guid> academicYearRepository,
	StudentScopeAccessService scopeAccess)
	: ApplicationService, ITransientDependency
{
	public Task EnsureAcademicYearAccessAsync(Guid academicYearId, string operation) =>
		scopeAccess.EnsureCanAccessAcademicYearAsync(academicYearId, operation);

	public Task<bool> HasAcademicYearWideScopeAsync(Guid academicYearId) =>
		scopeAccess.CurrentUserHasAcademicYearWideScopeAsync(academicYearId);

	public Task<IQueryable<AdmissionInquiryLead>> ApplyLeadScopeAsync(IQueryable<AdmissionInquiryLead> query) =>
		scopeAccess.ApplyCurrentUserAdmissionLeadScopeAsync(query);

	public async Task<AdmissionInquiryLead> GetLeadAsync(Guid id, string operation)
	{
		var lead = await leadRepository.GetAsync(id);
		await scopeAccess.EnsureCanAccessAdmissionLeadAsync(lead, operation);
		return lead;
	}

	public async Task<List<Guid>> GetScopedLeadIdsAsync(Guid academicYearId)
	{
		var queryable = await leadRepository.GetQueryableAsync();
		var query = queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId);
		query = await scopeAccess.ApplyCurrentUserAdmissionLeadScopeAsync(query);
		return await AsyncExecuter.ToListAsync(query.Select(x => x.Id));
	}

	public async Task EnsureReportScheduleAccessAsync(Guid academicYearId, string operation)
	{
		await scopeAccess.EnsureCanAccessAcademicYearAsync(academicYearId, operation);
		if (await scopeAccess.CurrentUserHasAcademicYearWideScopeAsync(academicYearId)) return;
		throw new BusinessException(UserScopeErrorCodes.AccessDenied)
			.WithData("AcademicYearId", academicYearId).WithData("Operation", operation);
	}

	public async Task EnsureRetentionMutationAccessAsync(Guid academicYearId, string operation)
	{
		await scopeAccess.EnsureCanAccessAcademicYearAsync(academicYearId, operation);
		if (await scopeAccess.CurrentUserHasAcademicYearWideScopeAsync(academicYearId)) return;
		throw new BusinessException("AdmissionCrm:AcademicYearWideScopeRequired")
			.WithData("AcademicYearId", academicYearId).WithData("Operation", operation);
	}

	public async Task EnsureAutomationYearAsync(Guid academicYearId, string operation)
	{
		var tenantId = CurrentTenant.Id;
		if (!tenantId.HasValue || academicYearId == Guid.Empty)
			throw NotFound(academicYearId, operation);
		var queryable = await academicYearRepository.GetQueryableAsync();
		var exists = await AsyncExecuter.AnyAsync(queryable.Where(x => x.Id == academicYearId && x.TenantId == tenantId));
		if (!exists) throw NotFound(academicYearId, operation);
	}

	private static BusinessException NotFound(Guid academicYearId, string operation) =>
		new BusinessException("AdmissionCrm:AcademicYearNotFound")
			.WithData("AcademicYearId", academicYearId).WithData("Operation", operation);
}
