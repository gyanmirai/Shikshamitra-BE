using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionInquiryRuntimeService(
	IRepository<AdmissionInquiry, Guid> inquiries,
	IRepository<AdmissionInquiryManagement, Guid> management,
	AdmissionInquiryPolicyResolver policies) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionInquiryManagement?> EnsureManagementAsync(AdmissionInquiry inquiry)
	{
		var existing = await management.FindAsync(item => item.InquiryId == inquiry.Id);
		if (existing != null) return existing;
		var context = await policies.ResolveAsync(inquiry.AdmissionOfferingId);
		if (context == null) return null;
		return await management.InsertAsync(new AdmissionInquiryManagement(GuidGenerator.Create(), CurrentTenant.Id,
			inquiry.Id, context.Definition?.Id, context.SnapshotJson, inquiry.CreationTime == default ? Clock.Now : inquiry.CreationTime,
			context.Policy.FirstContactDueInHours, context.Policy.QualificationDueInDays), false);
	}

	public async Task EnsureCanStartApplicationAsync(Guid offeringId, AdmissionInquiry? inquiry)
	{
		var context = await policies.ResolveAsync(offeringId);
		if (context == null) return;
		if (inquiry == null)
		{
			if (context.Policy.RequireLinkedInquiry)
				throw new BusinessException(AdmissionInquiryManagementErrorCodes.LinkedInquiryRequired,
					"Complete the managed inquiry before starting this application.");
			return;
		}
		if (await EnsureManagementAsync(inquiry) == null)
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidState);
	}

	public async Task<AdmissionInquiryManagement?> EnsureStageCompleteAsync(AdmissionApplication application,
		AdmissionWorkflowStageDefinition definition)
	{
		var policy = AdmissionInquiryPolicyResolver.Read(definition);
		if (policy == null) return null;
		if (!application.InquiryId.HasValue)
		{
			if (policy.RequireLinkedInquiry) throw new BusinessException(AdmissionInquiryManagementErrorCodes.LinkedInquiryRequired);
			return null;
		}
		var inquiry = await inquiries.FindAsync(application.InquiryId.Value);
		if (inquiry == null || inquiry.ApplicantId != application.ApplicantId ||
			inquiry.AdmissionOfferingId != application.AdmissionOfferingId)
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.LinkedInquiryRequired);
		var state = await EnsureManagementAsync(inquiry);
		if (state == null) throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidState);
		return state;
	}

	public async Task<IReadOnlyDictionary<string, string>> AddFactsAsync(AdmissionApplication application,
		IReadOnlyDictionary<string, string> values, AdmissionInquiryManagement? current = null)
	{
		var result = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
		foreach (var key in result.Keys.Where(key => key.StartsWith("inquiry.management.", StringComparison.OrdinalIgnoreCase)).ToList())
			result.Remove(key);
		if (!application.InquiryId.HasValue) return result;
		current ??= await management.FindAsync(item => item.InquiryId == application.InquiryId.Value);
		if (current == null) return result;
		result["inquiry.management.outcome"] = current.Outcome?.ToString() ?? "Pending";
		result["inquiry.management.contacted"] = (current.ContactCount > 0).ToString();
		result["inquiry.management.overdue"] = IsOverdue(current, Clock.Now).ToString();
		return result;
	}

	public static bool IsOverdue(AdmissionInquiryManagement state, DateTime now) =>
		(!state.LastContactAt.HasValue && state.FirstContactDueAt < now) ||
		(!state.Outcome.HasValue && state.QualificationDueAt < now);

}
