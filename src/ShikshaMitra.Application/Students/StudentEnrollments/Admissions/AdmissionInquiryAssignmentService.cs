using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionInquiryAssignmentService(
	IRepository<AdmissionInquiry, Guid> inquiries,
	IRepository<IdentityUser, Guid> users,
	AdmissionInquiryPeopleService people) : ApplicationService, ITransientDependency
{
	public async Task<Guid?> ResolvePolicyAssignmentAsync(AdmissionInquiryPolicy policy, Guid offeringId)
	{
		if (policy.AssignmentMode == AdmissionCounsellorAssignmentMode.Manual)
		{
			if (!policy.DefaultCounsellorId.HasValue) return null;
			await people.EnsureCanAccessOfferingAsync(policy.DefaultCounsellorId.Value, offeringId);
			return policy.DefaultCounsellorId;
		}

		var candidates = policy.CounsellorPoolUserIds.Distinct().ToList();
		if (candidates.Count == 0) throw new BusinessException(AdmissionInquiryManagementErrorCodes.CounsellorRequired);
		var active = await AsyncExecuter.ToListAsync((await users.GetQueryableAsync())
			.Where(user => candidates.Contains(user.Id) && user.IsActive).Select(user => user.Id));
		if (active.Count != candidates.Count)
			throw new BusinessException(AdmissionErrorCodes.InquiryCounsellorInvalid,
				"Every configured counsellor must be an active tenant user.");
		foreach (var candidate in candidates)
			await people.EnsureCanAccessOfferingAsync(candidate, offeringId);
		var assigned = await AsyncExecuter.ToListAsync((await inquiries.GetQueryableAsync())
			.Where(inquiry => inquiry.AssignedCounsellorId.HasValue && candidates.Contains(inquiry.AssignedCounsellorId.Value))
			.GroupBy(inquiry => inquiry.AssignedCounsellorId)
			.Select(group => new { Id = group.Key!.Value, Count = group.Count() }));
		return candidates.OrderBy(id => assigned.FirstOrDefault(item => item.Id == id)?.Count ?? 0)
			.ThenBy(id => id).First();
	}

	public async Task<Guid> ResolveExplicitAsync(AssignAdmissionCounsellorDto input, Guid offeringId)
	{
		if (input.Mode == AdmissionCounsellorAssignmentMode.Manual)
		{
			var id = input.CounsellorId ?? throw new BusinessException(AdmissionErrorCodes.InquiryCounsellorRequired);
			await people.EnsureCanAccessOfferingAsync(id, offeringId);
			return id;
		}
		var policy = new AdmissionInquiryPolicy
		{
			Version = 1,
			Enabled = true,
			AssignmentMode = AdmissionCounsellorAssignmentMode.RoundRobin,
			CounsellorPoolUserIds = input.CandidateCounsellorIds
		};
		policy.Validate();
		return await ResolvePolicyAssignmentAsync(policy, offeringId)
			?? throw new BusinessException(AdmissionInquiryManagementErrorCodes.CounsellorRequired);
	}

	public Task EnsureValidAsync(Guid id, Guid offeringId) => people.EnsureCanAccessOfferingAsync(id, offeringId);
}
