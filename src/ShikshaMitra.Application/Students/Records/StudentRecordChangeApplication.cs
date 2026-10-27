using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.Records;

/// <summary>Approval may remain valid while application reports a recoverable conflict.</summary>
[RemoteService(false)]
public class StudentRecordChangeApplication(IRepository<StudentRecordChange, Guid> changes,
	IRepository<StudentRecordChangeDecision, Guid> decisions, StudentRecordPayloadService payloads,
	StudentRecordsAccessService access, IPermissionChecker permissions, IdentityUserManager users,
	ICurrentPrincipalAccessor principals, StudentGuardianAccessReviewService guardianReviews) : ApplicationService, ITransientDependency
{
	public async Task ApplyOrScheduleAsync(Guid id)
	{
		try
		{
			using var work = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
			var item = await changes.GetAsync(x => x.Id == id && x.TenantId == CurrentTenant.Id);
			if (item.Status is not ("Approved" or "Scheduled")) return;
			var now = new DateTimeOffset(Clock.Now.ToUniversalTime());
			await EnsureApplicationActorAsync(item);
			if (item.EffectiveAt > now)
			{
				if (item.Status == "Approved") item.Schedule();
				await changes.UpdateAsync(item);
				await work.CompleteAsync();
				return;
			}
			var allowed = await access.EnsureWritableAsync(item.StudentId, item.Section == "placement");
			if (StudentRecordVisibility.IsMasked(allowed.Policy, item.Section, await access.GetDatabaseRoleNamesAsync()))
				throw new BusinessException("Students:Records:RestrictedFields");
			var permission = item.RequiredApprovals > 0 ? ShikshaMitraSubmodulePermissions.Students.Records_Changes.Approve
				: ShikshaMitraSubmodulePermissions.Students.Records_Changes.Create;
			if (!await permissions.IsGrantedAsync(permission)) throw new AbpAuthorizationException();
			var before = await payloads.CaptureAsync(item.StudentId, item.Section, item.Section == "custom-details" ? item.BeforeJson : null);
			if (StudentRecordPayloadService.Hash(before) != StudentRecordPayloadService.Hash(item.BeforeJson))
				throw new BusinessException("Students:Records:StaleSnapshot");
			await payloads.ApplyAsync(item.StudentId, item.Section, item.PayloadJson, item.Id);
			if (item.Section == "family")
			{
				var existingAccess = await guardianReviews.GetLinksAsync(item.StudentId);
				if (StudentFamilyContactSelection.RequiresAccessReview(before, item.PayloadJson, existingAccess.Select(link => link.Relationship).ToArray()))
					await guardianReviews.CaptureAsync(item.StudentId, item.Id, item.PolicyJson);
			}
			item.Apply(now);
			await changes.UpdateAsync(item);
			await work.CompleteAsync();
		}
		catch (BusinessException exception) { await MarkConflictAsync(id, exception.Code ?? "Students:Records:ValidationConflict"); }
		catch (AbpAuthorizationException) { await MarkConflictAsync(id, "Students:Records:AuthorityChanged"); }
		catch (AbpDbConcurrencyException) { await MarkConflictAsync(id, "Students:Records:ConcurrencyConflict"); }
		catch (Exception exception) when (StudentOnboardingRollConflictMapper.IsSerializationConflict(exception))
		{ await MarkConflictAsync(id, "Students:Records:ConcurrencyConflict"); }
	}
	public async Task ApplyScheduledAsync(Guid id)
	{
		Guid actor;
		using (var read = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: false))
		{
			var item = await changes.GetAsync(x => x.Id == id && x.TenantId == CurrentTenant.Id);
			if (item.Status is not ("Approved" or "Scheduled")) return;
			actor = item.RequestedBy;
			if (item.RequiredApprovals > 0)
			{
				var last = (await decisions.GetListAsync(x => x.ChangeId == id && x.TenantId == CurrentTenant.Id && x.Decision == "Approved"))
					.OrderByDescending(x => x.Stage).FirstOrDefault();
				if (last == null) throw new BusinessException("Students:Records:ApprovalMissing");
				actor = last.ActorId;
			}
			await read.CompleteAsync();
		}
		var user = await users.FindByIdAsync(actor.ToString());
		if (user == null || user.TenantId != CurrentTenant.Id || !user.IsActive)
		{
			await MarkConflictAsync(id, "Students:Records:AuthorityChanged");
			return;
		}
		var claims = new List<Claim> { new(AbpClaimTypes.UserId, actor.ToString()), new(AbpClaimTypes.UserName, user.UserName) };
		if (CurrentTenant.Id.HasValue) claims.Add(new(AbpClaimTypes.TenantId, CurrentTenant.Id.Value.ToString()));
		foreach (var role in await users.GetRolesAsync(user)) claims.Add(new(AbpClaimTypes.Role, role));
		using var identity = principals.Change(new ClaimsPrincipal(new ClaimsIdentity(claims, "RecordsScheduledCommand")));
		await ApplyOrScheduleAsync(id);
	}
	private async Task EnsureApplicationActorAsync(StudentRecordChange item)
	{
		if (item.RequiredApprovals == 0)
		{
			if (CurrentUser.Id != item.RequestedBy) throw new AbpAuthorizationException();
			return;
		}
		var history = await decisions.GetListAsync(x => x.ChangeId == item.Id && x.TenantId == CurrentTenant.Id && x.Decision == "Approved");
		var last = history.OrderByDescending(x => x.Stage).FirstOrDefault();
		var stages = JsonSerializer.Deserialize<List<StudentRecordsApprovalStage>>(item.ApprovalStagesJson, StudentRecordPayloadService.JsonOptions)!;
		if (last == null || history.Count != item.RequiredApprovals || last.Stage != item.RequiredApprovals - 1 ||
			last.ActorId != CurrentUser.Id || !stages[last.Stage].RoleNames.Intersect(await access.GetDatabaseRoleNamesAsync(), StringComparer.OrdinalIgnoreCase).Any())
			throw new AbpAuthorizationException();
	}
	private async Task MarkConflictAsync(Guid id, string code)
	{
		using var work = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
		var item = await changes.GetAsync(x => x.Id == id && x.TenantId == CurrentTenant.Id);
		if (item.Status is "Approved" or "Scheduled")
		{
			item.MarkConflict(code);
			await changes.UpdateAsync(item);
		}
		await work.CompleteAsync();
	}
}
