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
public class AdmissionApplicationCounsellorService(
	IRepository<AdmissionApplication, Guid> applications,
	IRepository<AdmissionActivity, Guid> activities,
	IRepository<AdmissionInquiry, Guid> inquiries,
	IRepository<AdmissionInquiryFollowUp, Guid> followUps,
	AdmissionApplicationScopeAccessService scope,
	AdmissionInquiryPeopleService people,
	AdmissionApplicationReadService reader) : ApplicationService, ITransientDependency
{
	public async Task<List<AdmissionInquiryCounsellorDto>> GetAssignableAsync(Guid? offeringId, string? filter)
	{
		if (filter?.Length > 128)
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidPolicy);
		var offerings = await scope.GetScopedOfferingsAsync();
		if (offeringId.HasValue)
		{
			if (!await AsyncExecuter.AnyAsync(offerings.Where(item => item.Id == offeringId.Value)))
				throw new EntityNotFoundException(typeof(AdmissionOffering), offeringId.Value);
			return await people.GetCounsellorsAsync(filter, offeringId.Value);
		}
		var query = await scope.GetScopedApplicationsAsync();
		var assignedIds = await AsyncExecuter.ToListAsync(query
			.Where(application => application.AssignedCounsellorId.HasValue &&
				offerings.Any(offering => offering.Id == application.AdmissionOfferingId))
			.Select(application => application.AssignedCounsellorId!.Value).Distinct());
		var names = await people.GetNamesAsync(assignedIds);
		return names.Where(item => string.IsNullOrWhiteSpace(filter) ||
			item.Value.Contains(filter, StringComparison.OrdinalIgnoreCase))
			.OrderBy(item => item.Value)
			.Select(item => new AdmissionInquiryCounsellorDto
			{
				Id = item.Key, Name = item.Value, UserName = item.Value
			}).ToList();
	}

	public async Task<AdmissionApplicationDto> AssignAsync(Guid applicationId, AssignAdmissionApplicationCounsellorDto input)
	{
		var application = await reader.GetEntityAsync(applicationId);
		AdmissionOfferAccessService.EnsureConcurrency(application, input.ConcurrencyStamp);
		if (application.Status is AdmissionApplicationStatus.Withdrawn or AdmissionApplicationStatus.AdmissionFinalized or AdmissionApplicationStatus.Enrolled)
			throw new BusinessException(AdmissionOfferStageErrorCodes.InvalidState,
				"This application is closed. Its counsellor cannot be changed.");
		await people.EnsureCanAccessOfferingAsync(input.CounsellorId, application.AdmissionOfferingId);
		if (application.AssignedCounsellorId == input.CounsellorId) return await reader.GetAsync(applicationId);
		var previousId = application.AssignedCounsellorId;
		application.AssignCounsellor(input.CounsellorId);
		await applications.UpdateAsync(application, false);
		if (application.InquiryId.HasValue)
		{
			var inquiry = await inquiries.FindAsync(application.InquiryId.Value);
			if (inquiry != null)
			{
				inquiry.AssignCounsellor(input.CounsellorId);
				await inquiries.UpdateAsync(inquiry, false);
				foreach (var followUp in await followUps.GetListAsync(item => item.InquiryId == inquiry.Id &&
					item.Status == AdmissionInquiryFollowUpStatus.Scheduled))
				{
					followUp.Reassign(input.CounsellorId);
					await followUps.UpdateAsync(followUp, false);
				}
			}
		}
		await activities.InsertAsync(new AdmissionActivity(GuidGenerator.Create(), CurrentTenant.Id,
			"ApplicationCounsellorAssigned", "Application counsellor assigned or changed.",
			application.ApplicantId, application.Id, metadataJson: JsonSerializer.Serialize(new
			{
				PreviousCounsellorId = previousId,
				AssignedCounsellorId = input.CounsellorId,
				Reason = input.Reason?.Trim(),
				AssignedBy = CurrentUser.Id
			})), false);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await reader.GetAsync(applicationId);
	}
}
