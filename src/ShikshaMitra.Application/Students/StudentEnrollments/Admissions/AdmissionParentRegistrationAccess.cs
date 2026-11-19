using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionParentRegistrationAccess(IRepository<AdmissionParentRegistration, Guid> grants,
	AdmissionParentRegistrationData data, IDataFilter filters, IdentityUserManager users,
	ICurrentPrincipalAccessor principals, IPermissionChecker permissions, AdmissionApplicationScopeAccessService scope)
	: ApplicationService, ITransientDependency
{
	internal async Task<AdmissionParentRegistration> ResolveAsync(string token)
	{
		if (token?.Length != 64 || !token.All(Uri.IsHexDigit)) throw AdmissionParentRegistrationData.Unavailable();
		AdmissionParentRegistration? grant;
		var hash = AdmissionParentRegistration.Hash(token);
		using (filters.Disable<IMultiTenant>()) grant = await grants.FindAsync(row => row.TokenHash == hash);
		if (grant == null || !grant.CanRead(Clock.Now)) throw AdmissionParentRegistrationData.Unavailable();
		return grant;
	}
	internal async Task<AdmissionApplication> AuthorizeAsync(AdmissionParentRegistration grant, bool requireLiveGrant = true)
	{
		if (grant.TenantId != CurrentTenant.Id || requireLiveGrant && !grant.CanRead(Clock.Now)) throw AdmissionParentRegistrationData.Unavailable();
		var application = await data.Repo<AdmissionApplication>().FindAsync(grant.ApplicationId);
		var documentOnly = application != null && await data.IsDocumentCorrectionAllowedAsync(application, grant);
		if (application == null || !AdmissionParentRegistrationData.IsOpen(application) ||
			application.SubmittedAt.HasValue && !application.CanCorrectForm ||
			(grant.Purpose == "initial" || !application.SubmittedAt.HasValue) && !documentOnly && !await data.IsInitialAllowedAsync(application)) throw AdmissionParentRegistrationData.Unavailable();
		if (!await data.AreDocumentCorrectionStagesOpenAsync(application, grant)) throw AdmissionParentRegistrationData.Unavailable();
		var applicant = await data.Repo<AdmissionApplicant>().GetAsync(application.ApplicantId);
		var saved = JsonSerializer.Deserialize<AdmissionParentRecipientDto>(grant.RecipientJson)!;
		var recipient = AdmissionParentRegistrationData.Recipients(applicant).SingleOrDefault(item => item.Relationship == saved.Relationship && item.CanSend);
		if (recipient == null || AdmissionParentRegistrationData.RecipientFingerprint(applicant, recipient) != grant.RecipientFingerprint ||
			requireLiveGrant && !await SenderAuthorizedAsync(grant, application)) throw AdmissionParentRegistrationData.Unavailable();
		return application;
	}
	internal async Task<bool> SenderAuthorizedAsync(AdmissionParentRegistration grant, AdmissionApplication application)
	{
		var user = await users.FindByIdAsync(grant.RequestedBy.ToString());
		if (user == null || !user.IsActive || user.TenantId != grant.TenantId) return false;
		var claims = new List<Claim> { new(AbpClaimTypes.UserId, user.Id.ToString()), new(AbpClaimTypes.UserName, user.UserName) };
		if (user.TenantId.HasValue) claims.Add(new(AbpClaimTypes.TenantId, user.TenantId.Value.ToString()));
		claims.AddRange((await users.GetRolesAsync(user)).Select(role => new Claim(AbpClaimTypes.Role, role)));
		// This temporary identity is used ONLY to evaluate the sender's CURRENT permission/scope.
		// It is disposed before any parent read projection, mutation, blob write or audit.
		using var checking = principals.Change(new ClaimsPrincipal(new ClaimsIdentity(claims, "RegistrationAuthorityCheck", AbpClaimTypes.UserName, AbpClaimTypes.Role)));
		return await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View) &&
			await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Edit) &&
			(!grant.AllowsDocuments || await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionDocuments.Upload)) && await scope.CanAccessAsync(application);
	}
	internal async Task<AdmissionApplication> StaffAsync(Guid applicationId, bool write)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View);
		if (write) await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Edit);
		var application = await data.Repo<AdmissionApplication>().GetAsync(applicationId);
		await scope.EnsureCanAccessAsync(application);
		return application;
	}
	internal async Task EnsureSnapshotAsync(AdmissionParentRegistration grant, AdmissionApplication application, string snapshot)
	{
		if (!grant.CanWrite(Clock.Now)) throw AdmissionParentRegistrationData.Unavailable();
		if (snapshot != await data.SnapshotAsync(grant, application)) throw AdmissionParentRegistrationData.Conflict();
	}
}
