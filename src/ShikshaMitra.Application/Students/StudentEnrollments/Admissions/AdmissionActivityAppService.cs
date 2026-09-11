using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[Authorize]
public class AdmissionActivityAppService(IRepository<AdmissionActivity, Guid> repository) : ApplicationService, IAdmissionActivityAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public async Task<System.Collections.Generic.List<AdmissionActivityDto>> GetForApplicantAsync(Guid applicantId) => (await repository.GetListAsync(x => x.ApplicantId == applicantId)).OrderByDescending(x => x.OccurredAt).Select(Map).ToList();
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public async Task<System.Collections.Generic.List<AdmissionActivityDto>> GetForApplicationAsync(Guid applicationId) => (await repository.GetListAsync(x => x.ApplicationId == applicationId)).OrderByDescending(x => x.OccurredAt).Select(Map).ToList();
	private static AdmissionActivityDto Map(AdmissionActivity x) => new() { Id = x.Id, ApplicantId = x.ApplicantId, ApplicationId = x.ApplicationId, InquiryId = x.InquiryId, ActivityType = x.ActivityType, Description = x.Description, OccurredAt = x.OccurredAt, CreatorId = x.CreatorId };
}
