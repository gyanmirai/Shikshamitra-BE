using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Students.Records;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.Portal;

/// <summary>Child-specific revocation inside the authorized Records transition transaction.</summary>
[RemoteService(false)]
public class StudentPortalLifecycleService(IRepository<StudentPortalAccountLink, Guid> links,
	IRepository<StudentCredentialEmailRequest, Guid> credentialEmails,
	IStudentRepository students, IdentityUserManager users, IDataFilter dataFilter)
	: ApplicationService, ITransientDependency
{
	private const string RevokedLinksProperty = "ExitRevokedPortalLinks";

	public async Task RevokeAsync(StudentRecordTransition transition, Student student)
	{
		EnsureTenant(transition, student);
		var active = await links.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == student.Id);
		// Record a tombstone for a legacy self association too. Return must explicitly
		// reverify it rather than silently regaining access through Student.UserId.
		if (student.UserId.HasValue && !active.Any(x => x.AccountType == "student" && x.UserId == student.UserId))
		{
			bool hasHistory;
			using (dataFilter.Disable<ISoftDelete>())
				hasHistory = await links.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == student.Id && x.AccountType == "student" && x.UserId == student.UserId);
			if (!hasHistory)
			{
				var legacy = new StudentPortalAccountLink(GuidGenerator.Create(), CurrentTenant.Id!.Value, student.Id,
					student.UserId.Value, "student", "self", CurrentUser.Id!.Value);
				await links.InsertAsync(legacy);
				await CurrentUnitOfWork!.SaveChangesAsync();
				active.Add(legacy);
			}
		}
		transition.SetProperty(RevokedLinksProperty, JsonSerializer.Serialize(active.Select(x => x.Id).ToArray()));
		foreach (var link in active) await links.DeleteAsync(link);
		// Failed deliveries can retain a retry secret. Clear all unsent child-local
		// intents immediately with the exit, including an interrupted sender lease.
		var unsent = await credentialEmails.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == student.Id &&
			(x.Status == "Pending" || x.Status == "Processing" || x.Status == "Failed"));
		foreach (var request in unsent)
		{
			request.Cancel("Student portal access was revoked after leaving the organization.");
			await credentialEmails.UpdateAsync(request);
		}
	}

	public async Task RestoreCorrectionAsync(StudentRecordTransition original, Student student)
	{
		EnsureTenant(original, student);
		if (student.Status == StudentLifeCycleStatusEnum.Left || !original.ExtraProperties.TryGetValue(RevokedLinksProperty, out var raw)) return;
		var ids = JsonSerializer.Deserialize<Guid[]>(raw?.ToString() ?? "[]") ?? [];
		if (ids.Length == 0) return;
		var current = await links.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == student.Id);
		List<StudentPortalAccountLink> revoked;
		using (dataFilter.Disable<ISoftDelete>())
			revoked = await links.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == student.Id && Enumerable.Contains(ids, x.Id));
		var profile = await students.GetWithDetailsAsync(student.Id) ?? throw new EntityNotFoundException(typeof(Student), student.Id);
		foreach (var link in revoked.Where(x => x.IsDeleted))
		{
			// Never overwrite a replacement link or resurrect a guardian whose saved
			// relationship/mailbox has changed since the original exit.
			if (current.Any(x => x.AccountType == link.AccountType))
				throw new BusinessException("Students:Records:PortalAccessChanged");
			var user = await users.FindByIdAsync(link.UserId.ToString());
			var recordedEmail = link.AccountType == "student" ? profile.ContactDetail?.Email : link.Relationship switch
			{
				"father" => profile.FamilyDetail?.FatherDetails?.Email,
				"mother" => profile.FamilyDetail?.MotherDetails?.Email,
				_ => profile.FamilyDetail?.GuardianDetails?.Email
			};
			var roles = user == null ? [] : await users.GetRolesAsync(user);
			if (user == null || user.TenantId != CurrentTenant.Id || !user.IsActive || roles.Count != 1 ||
				roles[0] != (link.AccountType == "student" ? "Student" : "Parent") ||
				!string.Equals(recordedEmail?.Trim(), user.Email, StringComparison.OrdinalIgnoreCase))
				throw new BusinessException("Students:Records:PortalAccessChanged");
			link.IsDeleted = false;
			link.DeleterId = null;
			link.DeletionTime = null;
			await links.UpdateAsync(link);
			current.Add(link);
		}
	}

	private void EnsureTenant(StudentRecordTransition transition, Student student)
	{
		if (!CurrentTenant.Id.HasValue || !CurrentUser.Id.HasValue || transition.TenantId != CurrentTenant.Id ||
			student.TenantId != CurrentTenant.Id || transition.StudentId != student.Id) throw new AbpAuthorizationException();
	}
}
