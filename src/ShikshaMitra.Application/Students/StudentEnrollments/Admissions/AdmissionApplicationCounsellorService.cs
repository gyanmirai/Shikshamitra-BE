using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionApplicationCounsellorService : ApplicationService, ITransientDependency
{
	public AdmissionApplicationCounsellorService(
		IRepository<AdmissionApplication, Guid> applications,
		IRepository<AdmissionActivity, Guid> activities,
		IRepository<AdmissionInquiry, Guid> inquiries,
		IRepository<AdmissionInquiryFollowUp, Guid> followUps,
		AdmissionApplicationScopeAccessService scope,
		AdmissionInquiryPeopleService people,
		AdmissionApplicationReadService reader
	)
	{
		_ = applications;
		_ = activities;
		_ = inquiries;
		_ = followUps;
		_ = scope;
		_ = people;
		_ = reader;
	}

	public Task<List<AdmissionInquiryCounsellorDto>> GetAssignableAsync(Guid? offeringId, string? filter)
	{
		return Task.FromResult(new List<AdmissionInquiryCounsellorDto>());
	}

	public Task<AdmissionApplicationDto> AssignAsync(Guid applicationId, AssignAdmissionApplicationCounsellorDto input)
	{
		throw new Volo.Abp.BusinessException("Admissions.StaffAssignmentRetired", "Staff handling is recorded automatically in the activity history.");
	}
}
