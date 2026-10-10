using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Projects searchable/sortable list data before server pagination.</summary>
[RemoteService(false)]
public class AdmissionApplicationListQueryService(
	IRepository<AdmissionApplicant, Guid> applicants,
	IRepository<AdmissionInquiry, Guid> inquiries,
	IRepository<AdmissionFamily, Guid> families,
	IRepository<AdmissionGuardian, Guid> guardians,
	IRepository<AdmissionOffering, Guid> offerings,
	IRepository<Standard, Guid> standards,
	IRepository<IdentityUser, Guid> users,
	IRepository<AdmissionApplicationStageInstance, Guid> stages) : ITransientDependency
{
	public async Task<IQueryable<AdmissionApplicationListRow>> ProjectAsync(IQueryable<AdmissionApplicationWorkspaceRow> workspaces)
	{
		var applicantQuery = await applicants.GetQueryableAsync();
		var inquiryQuery = await inquiries.GetQueryableAsync();
		var familyQuery = await families.GetQueryableAsync();
		var guardianQuery = await guardians.GetQueryableAsync();
		var offeringQuery = await offerings.GetQueryableAsync();
		var standardQuery = await standards.GetQueryableAsync();
		var userQuery = await users.GetQueryableAsync();
		var stageQuery = await stages.GetQueryableAsync();
		return from workspace in workspaces
			join applicant in applicantQuery on workspace.Application.ApplicantId equals applicant.Id into linkedApplicants
			from applicant in linkedApplicants.DefaultIfEmpty()
			join inquiry in inquiryQuery on workspace.Application.InquiryId equals (Guid?)inquiry.Id into linkedInquiries
			from inquiry in linkedInquiries.DefaultIfEmpty()
			join family in familyQuery on applicant.FamilyId equals family.Id into linkedFamilies
			from family in linkedFamilies.DefaultIfEmpty()
			join guardian in guardianQuery on family.PrimaryGuardianId equals (Guid?)guardian.Id into linkedGuardians
			from guardian in linkedGuardians.DefaultIfEmpty()
			join offering in offeringQuery on workspace.Application.AdmissionOfferingId equals offering.Id
			join standard in standardQuery on offering.StandardId equals standard.Id into linkedStandards
			from standard in linkedStandards.DefaultIfEmpty()
			join owner in userQuery on workspace.Application.AssignedCounsellorId equals (Guid?)owner.Id into linkedOwners
			from owner in linkedOwners.DefaultIfEmpty()
			join stage in stageQuery on workspace.Application.CurrentStageInstanceId equals (Guid?)stage.Id into currentStages
			from stage in currentStages.DefaultIfEmpty()
			select new AdmissionApplicationListRow
			{
				Application = workspace.Application, HasReachedDecision = workspace.HasReachedDecision,
				DecisionEnteredAt = workspace.DecisionEnteredAt,
				ApplicantName = applicant == null ? (inquiry == null ? string.Empty : inquiry.ApplicantName) : applicant.FirstName + " " + (applicant.LastName ?? string.Empty),
				ContactPhone = guardian == null ? (inquiry == null ? null : inquiry.NormalizedPhone) : guardian.Mobile,
				ContactEmail = guardian == null || guardian.Email == null ? (inquiry == null ? null : inquiry.Email) : guardian.Email,
				StandardName = standard == null ? null : standard.Name, OfferingCode = offering.Code,
				CurrentStageName = stage == null ? string.Empty : stage.Name,
				AssignedCounsellorName = owner == null ? null :
					((owner.Name == null || owner.Name == "") && (owner.Surname == null || owner.Surname == "")
						? owner.UserName : ((owner.Name ?? string.Empty) + " " + (owner.Surname ?? string.Empty)).Trim())
			};
	}

	public static IOrderedQueryable<AdmissionApplicationListRow> Sort(IQueryable<AdmissionApplicationListRow> query, string? sorting)
	{
		var parts = (string.IsNullOrWhiteSpace(sorting) ? "creationTime desc" : sorting).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
		var descending = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);
		var sorted = parts[0].ToLowerInvariant() switch
		{
			"applicationnumber" => descending ? query.OrderByDescending(row => row.Application.ApplicationNumber) : query.OrderBy(row => row.Application.ApplicationNumber),
			"applicantname" => descending ? query.OrderByDescending(row => row.ApplicantName) : query.OrderBy(row => row.ApplicantName),
			"standardname" => descending ? query.OrderByDescending(row => row.StandardName) : query.OrderBy(row => row.StandardName),
			"contactphone" => descending ? query.OrderByDescending(row => row.ContactPhone) : query.OrderBy(row => row.ContactPhone),
			"assignedcounsellorname" => descending ? query.OrderByDescending(row => row.AssignedCounsellorName) : query.OrderBy(row => row.AssignedCounsellorName),
			"status" => descending ? query.OrderByDescending(row => row.Application.Status) : query.OrderBy(row => row.Application.Status),
			"lastsavedat" => descending ? query.OrderByDescending(row => row.Application.LastSavedAt) : query.OrderBy(row => row.Application.LastSavedAt),
			"submittedat" => descending ? query.OrderByDescending(row => row.Application.SubmittedAt) : query.OrderBy(row => row.Application.SubmittedAt),
			"decisionat" => descending ? query.OrderByDescending(row => row.Application.DecisionAt) : query.OrderBy(row => row.Application.DecisionAt),
			"finalizedat" => descending ? query.OrderByDescending(row => row.Application.FinalizedAt) : query.OrderBy(row => row.Application.FinalizedAt),
			"decisionenteredat" => descending ? query.OrderByDescending(row => row.DecisionEnteredAt) : query.OrderBy(row => row.DecisionEnteredAt),
			"creationtime" => descending ? query.OrderByDescending(row => row.Application.CreationTime) : query.OrderBy(row => row.Application.CreationTime),
			_ => query.OrderByDescending(row => row.Application.CreationTime)
		};
		return sorted.ThenBy(row => row.Application.Id);
	}
}

public class AdmissionApplicationListRow : AdmissionApplicationWorkspaceRow
{
	public string ApplicantName { get; set; } = string.Empty;
	public string OfferingCode { get; set; } = string.Empty;
	public string CurrentStageName { get; set; } = string.Empty;
	public string? StandardName { get; set; }
	public string? ContactPhone { get; set; }
	public string? ContactEmail { get; set; }
	public string? AssignedCounsellorName { get; set; }
}
