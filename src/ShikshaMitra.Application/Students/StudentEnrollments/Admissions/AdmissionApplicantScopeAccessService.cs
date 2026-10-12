using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionApplicantScopeAccessService(
	IRepository<AdmissionApplicant, Guid> applicants,
	IRepository<AdmissionApplication, Guid> applications,
	IRepository<AdmissionInquiry, Guid> allInquiries,
	AdmissionApplicationScopeAccessService applicationScope,
	AdmissionInquiryScopeAccessService inquiryScope) : ApplicationService, ITransientDependency
{
	public async Task<IQueryable<AdmissionApplication>> GetApplicationsAsync(Guid applicantId)
	{
		return (await applicationScope.GetScopedApplicationsAsync()).Where(application => application.ApplicantId == applicantId);
	}

	public async Task<AdmissionApplicant> GetAsync(Guid applicantId)
	{
		var scopedApplications = await GetApplicationsAsync(applicantId);
		var inquiries = await inquiryScope.GetScopedQueryAsync();
		var query = (await applicants.GetQueryableAsync()).Where(applicant =>
			applicant.TenantId == CurrentTenant.Id && applicant.Id == applicantId &&
			(scopedApplications.Any() || inquiries.Any(inquiry => inquiry.ApplicantId == applicant.Id)));
		return await AsyncExecuter.FirstOrDefaultAsync(query)
			?? throw new EntityNotFoundException(typeof(AdmissionApplicant), applicantId);
	}

	public async Task<IQueryable<AdmissionActivity>> ApplyActivityScopeAsync(
		IQueryable<AdmissionActivity> query, Guid applicantId)
	{
		await GetAsync(applicantId);
		var scopedApplications = await GetApplicationsAsync(applicantId);
		var inquiries = await inquiryScope.GetScopedQueryAsync();
		return query.Where(activity => activity.TenantId == CurrentTenant.Id && activity.ApplicantId == applicantId &&
			(!activity.ApplicationId.HasValue || scopedApplications.Any(application => application.Id == activity.ApplicationId.Value)) &&
			(!activity.InquiryId.HasValue || inquiries.Any(inquiry => inquiry.Id == activity.InquiryId.Value)));
	}

	public async Task EnsureCanUpdateProfileAsync(Guid applicantId, bool includesGuardian)
	{
		var applicant = await GetAsync(applicantId);
		var targets = (await applicants.GetQueryableAsync()).Where(item => item.TenantId == CurrentTenant.Id &&
			(item.Id == applicantId || (includesGuardian && item.FamilyId == applicant.FamilyId)));
		var scopedApplications = await applicationScope.GetScopedApplicationsAsync();
		var scopedInquiries = await inquiryScope.GetScopedQueryAsync();
		var applicationQuery = await applications.GetQueryableAsync();
		var inquiryQuery = await allInquiries.GetQueryableAsync();
		var hasOutsideApplication = await AsyncExecuter.AnyAsync(applicationQuery.Where(application =>
			application.TenantId == CurrentTenant.Id && targets.Any(item => item.Id == application.ApplicantId) &&
			!scopedApplications.Any(allowed => allowed.Id == application.Id)));
		var hasOutsideInquiry = await AsyncExecuter.AnyAsync(inquiryQuery.Where(inquiry =>
			inquiry.TenantId == CurrentTenant.Id && targets.Any(item => item.Id == inquiry.ApplicantId) &&
			!scopedInquiries.Any(allowed => allowed.Id == inquiry.Id)));
		if (hasOutsideApplication || hasOutsideInquiry)
			throw new EntityNotFoundException(typeof(AdmissionApplicant), applicantId);
	}
}
