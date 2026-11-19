using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.ValueObjects;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.Records;

[RemoteService(false)]
[Authorize]
public class StudentGuardianAccessAppService(IRepository<StudentGuardianAccessReview, Guid> reviews,
	IRepository<StudentPortalAccountLink, Guid> links, IStudentRepository students,
	StudentGuardianAccessReviewService capture, StudentRecordsAccessService access,
	IdentityUserManager users, IdentityRoleManager roles) : ApplicationService, IStudentGuardianAccessAppService
{
	public async Task<List<StudentGuardianPortalLinkDto>> GetLinksAsync(Guid studentId)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_GuardianAccess.View);
		await EnsureAccessAsync(studentId); return await capture.GetLinksAsync(studentId);
	}
	public async Task<List<StudentGuardianAccessReviewDto>> GetReviewsAsync(Guid studentId)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_GuardianAccess.View);
		await EnsureAccessAsync(studentId);
		return (await reviews.GetListAsync(r => r.TenantId == CurrentTenant.Id && r.StudentId == studentId)).OrderByDescending(r => r.CreationTime).Select(Map).ToList();
	}
	[UnitOfWork(true)]
	public async Task<StudentGuardianAccessReviewDto> ResolveAsync(Guid reviewId, ResolveStudentGuardianAccessDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_GuardianAccess.Manage);
		var review = await reviews.FindAsync(r => r.Id == reviewId && r.TenantId == CurrentTenant.Id) ?? throw new EntityNotFoundException(typeof(StudentGuardianAccessReview), reviewId);
		await EnsureAccessAsync(review.StudentId);
		if (review.Status != "Pending" || input.ConcurrencyStamp != review.ConcurrencyStamp) throw new BusinessException("Students:Records:GuardianReviewConflict");
		if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length < 5 || input.Reason.Length > 2000 || input.RevokeLinkIds == null || input.KeepLinkIds == null)
			throw new BusinessException("Students:Records:GuardianReviewInvalid");
		var current = await capture.GetLinksAsync(review.StudentId);
		var previous = JsonSerializer.Deserialize<List<StudentGuardianPortalLinkDto>>(review.PreviousLinksJson, StudentGuardianAccessReviewService.JsonOptions)!;
		var reviewLinks = previous.Concat(current).DistinctBy(l => l.LinkId).ToList();
		var selected = input.RevokeLinkIds.Concat(input.KeepLinkIds).ToArray();
		if (selected.Distinct().Count() != selected.Length || selected.Length != reviewLinks.Count || reviewLinks.Any(l => !selected.Contains(l.LinkId)))
			throw new BusinessException("Students:Records:GuardianLinkSelectionInvalid");
		if (input.KeepLinkIds.Count > 0 && !input.ConfirmRetainedGuardiansVerified) throw new BusinessException("Students:Records:GuardianIdentityVerificationRequired");
		var student = await students.GetWithDetailsAsync(review.StudentId) ?? throw new EntityNotFoundException(typeof(Student), review.StudentId);
		foreach (var retained in reviewLinks.Where(l => input.KeepLinkIds.Contains(l.LinkId)))
		{
			var guardian = GetGuardian(student, retained.Relationship);
			var user = await users.FindByIdAsync(retained.UserId.ToString());
			if (user == null || user.TenantId != CurrentTenant.Id || !user.IsActive || !user.EmailConfirmed || !SameEmail(guardian?.Email, user.Email)) throw new BusinessException("Students:Records:GuardianIdentityMismatch");
			var roleNames = await users.GetRolesAsync(user);
			if (roleNames.Count != 1 || roleNames[0] != "Parent") throw new BusinessException("Students:Records:GuardianIdentityMismatch");
		}
		if (input.NewGuardian != null && input.KeepLinkIds.Count > 0) throw new BusinessException("Students:Records:GuardianLinkAlreadyExists");
		foreach (var id in input.RevokeLinkIds)
		{
			var link = await links.FindAsync(l => l.Id == id && l.TenantId == CurrentTenant.Id && l.StudentId == review.StudentId && l.AccountType == "guardian");
			if (link != null) await links.DeleteAsync(link);
		}
		// Flush only link deletions within this transaction before adding a replacement against the active-link uniqueness constraint.
		await CurrentUnitOfWork!.SaveChangesAsync();
		foreach (var retained in reviewLinks.Where(l => input.KeepLinkIds.Contains(l.LinkId) && current.All(c => c.LinkId != l.LinkId)))
			await links.InsertAsync(new StudentPortalAccountLink(GuidGenerator.Create(), CurrentTenant.Id!.Value, student.Id, retained.UserId,
				"guardian", retained.Relationship, CurrentUser.Id!.Value), autoSave: true);
		if (input.NewGuardian != null) await LinkVerifiedGuardianAsync(student, input.NewGuardian);
		review.Resolve(CurrentUser.Id!.Value, Clock.Now, JsonSerializer.Serialize(input, StudentGuardianAccessReviewService.JsonOptions));
		await reviews.UpdateAsync(review, autoSave: true); return Map(review);
	}
	public async Task<StudentGuardianPortalLinkDto?> GetVerifiedGuardianCandidateAsync(Guid studentId, string relationship)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_GuardianAccess.Manage);
		await AuthorizationService.CheckAsync(IdentityPermissions.Users.Update);
		await EnsureAccessAsync(studentId);
		var student = await students.GetWithDetailsAsync(studentId) ?? throw new EntityNotFoundException(typeof(Student), studentId);
		var guardian = GetGuardian(student, relationship);
		if (guardian == null || string.IsNullOrWhiteSpace(guardian.Email)) return null;
		var user = await users.FindByEmailAsync(guardian.Email.Trim());
		if (user == null || user.TenantId != CurrentTenant.Id || !user.IsActive || !user.EmailConfirmed) return null;
		var roleNames = await users.GetRolesAsync(user);
		return roleNames.Count == 1 && roleNames[0] == "Parent" ? new() { UserId = user.Id, Relationship = relationship, Email = user.Email,
			EmailConfirmed = user.EmailConfirmed, IsActive = user.IsActive } : null;
	}
	private async Task EnsureAccessAsync(Guid studentId)
	{
		var permission = await access.EnsureWritableAsync(studentId);
		if (permission.Policy.IsMasked("Guardian", await access.GetDatabaseRoleNamesAsync())) throw new BusinessException("Students:Records:GuardianFieldsRestricted");
	}
	private async Task LinkVerifiedGuardianAsync(Student student, VerifiedStudentGuardianDto input)
	{
		if (!input.ConfirmIdentityVerified || input.Relationship is not ("father" or "mother" or "guardian") || string.IsNullOrWhiteSpace(input.Email) || input.Email.Length > 256)
			throw new BusinessException("Students:Records:GuardianIdentityVerificationRequired");
		var guardian = GetGuardian(student, input.Relationship);
		var email = input.Email.Trim();
		if (guardian == null || !SameEmail(guardian.Email, email) || string.IsNullOrWhiteSpace(guardian.FullName)) throw new BusinessException("Students:Records:GuardianIdentityMismatch");
		if (!await roles.RoleExistsAsync("Parent")) throw new BusinessException("Students:Records:GuardianPortalRoleMissing");
		var user = await users.FindByEmailAsync(email);
		var existingIdentity = user != null;
		if (input.ExistingUserId.HasValue)
		{
			await AuthorizationService.CheckAsync(IdentityPermissions.Users.Update);
			if (user == null || user.Id != input.ExistingUserId || user.TenantId != CurrentTenant.Id || !user.IsActive || !user.EmailConfirmed)
				throw new BusinessException("Students:Records:GuardianIdentityMismatch");
			var roleNames = await users.GetRolesAsync(user);
			if (roleNames.Count != 1 || roleNames[0] != "Parent") throw new BusinessException("Students:Records:GuardianIdentityMismatch");
		}
		else
		{
			await AuthorizationService.CheckAsync(IdentityPermissions.Users.Create);
			if (user != null) throw new BusinessException("Students:Records:GuardianExistingIdentityConfirmationRequired");
			user = new IdentityUser(GuidGenerator.Create(), email, email, CurrentTenant.Id) { Name = guardian.FullName };
			if (!(await users.CreateAsync(user)).Succeeded || !(await users.AddToRoleAsync(user, "Parent")).Succeeded)
				throw new BusinessException("Students:Records:GuardianAccountCreationRejected");
		}
		// Preserve the reviewed identity under a concurrent owner email change.
		// Only optimistic concurrency advances; credentials and role grants stay intact.
		if (existingIdentity && !(await users.UpdateAsync(user)).Succeeded)
			throw new BusinessException("Students:Records:GuardianIdentityMismatch");
		await links.InsertAsync(new StudentPortalAccountLink(GuidGenerator.Create(), CurrentTenant.Id!.Value, student.Id, user.Id,
			"guardian", input.Relationship, CurrentUser.Id!.Value), autoSave: true);
	}
	private static bool SameEmail(string? saved, string candidate) => !string.IsNullOrWhiteSpace(saved) && string.Equals(saved.Trim(), candidate.Trim(), StringComparison.OrdinalIgnoreCase);
	private static ParentGuardianDetails? GetGuardian(Student student, string relationship) => relationship switch
	{
		"father" => student.FamilyDetail?.FatherDetails, "mother" => student.FamilyDetail?.MotherDetails, "guardian" => student.FamilyDetail?.GuardianDetails, _ => null
	};
	private static StudentGuardianAccessReviewDto Map(StudentGuardianAccessReview review) => new()
	{
		Id = review.Id, StudentId = review.StudentId, ChangeRequestId = review.ChangeRequestId, Status = review.Status, ConcurrencyStamp = review.ConcurrencyStamp, OnApprovedChangeMode = review.OnApprovedChangeMode,
		CreationTime = review.CreationTime, ReviewedAt = review.ReviewedAt, ReviewedByUserId = review.ReviewedByUserId,
		PreviousLinks = JsonSerializer.Deserialize<List<StudentGuardianPortalLinkDto>>(review.PreviousLinksJson, StudentGuardianAccessReviewService.JsonOptions)!
	};
}
