using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.Records;

/// <summary>Internal hook invoked by the approved family-change transaction.</summary>
[RemoteService(false)]
public class StudentGuardianAccessReviewService(IRepository<StudentGuardianAccessReview, Guid> reviews,
	IRepository<StudentPortalAccountLink, Guid> links, IIdentityUserRepository users,
	StudentRecordsAccessService access) : ApplicationService, ITransientDependency
{
	internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	public async Task<StudentGuardianAccessReview> CaptureAsync(Guid studentId, Guid changeRequestId, string policyJson)
	{
		await access.EnsureWritableAsync(studentId);
		var existing = await reviews.FindAsync(r => r.TenantId == CurrentTenant.Id && r.ChangeRequestId == changeRequestId);
		if (existing != null) return existing;
		var snapshot = await GetLinksAsync(studentId);
		var mode = StudentRecordsPolicy.FromJson(policyJson).GuardianAccess.OnApprovedChange;
		var review = new StudentGuardianAccessReview(GuidGenerator.Create(), CurrentTenant.Id!.Value, studentId, changeRequestId, JsonSerializer.Serialize(snapshot, JsonOptions), mode);
		if (mode == "SuspendUntilReview")
			foreach (var previous in snapshot) await links.DeleteAsync(l => l.Id == previous.LinkId && l.TenantId == CurrentTenant.Id && l.StudentId == studentId && l.AccountType == "guardian");
		return await reviews.InsertAsync(review, autoSave: true);
	}
	public async Task<List<StudentGuardianPortalLinkDto>> GetLinksAsync(Guid studentId)
	{
		var rows = await links.GetListAsync(l => l.TenantId == CurrentTenant.Id && l.StudentId == studentId && l.AccountType == "guardian");
		var result = new List<StudentGuardianPortalLinkDto>();
		foreach (var link in rows)
		{
			var user = await users.FindAsync(link.UserId);
			if (user == null || user.TenantId != CurrentTenant.Id) continue;
			result.Add(new() { LinkId = link.Id, UserId = user.Id, Relationship = link.Relationship, Email = user.Email,
				EmailConfirmed = user.EmailConfirmed, IsActive = user.IsActive });
		}
		return result;
	}
}
