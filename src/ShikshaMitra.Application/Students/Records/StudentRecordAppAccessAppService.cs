using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.Records;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_AppAccess.View)]
public class StudentRecordAppAccessAppService(StudentRecordsAccessService access, StudentRecordsActorService actor,
	IRepository<StudentEnrollment, Guid> enrollments, IRepository<StudentPortalAccountLink, Guid> links,
	IRepository<IdentityUser, Guid> users, IRepository<StudentAppAccessRestriction, Guid> restrictions,
	IRepository<StudentAppAccessEvent, Guid> events, IDataFilter dataFilter,
	StudentAppAccessEnforcement enforcement) : ApplicationService, IStudentRecordAppAccessAppService
{
	private const string Self = "self";

	public async Task<StudentRecordAppAccessDto> GetAsync(Guid studentId)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_AppAccess.View);
		var (student, showParents) = await EnsureAccessibleAsync(studentId);
		var recipients = await GetRecipientsAsync(student, showParents);
		var canUpdate = await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Records_AppAccess.Add);
		return await BuildAsync(studentId, recipients, showParents, canUpdate);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_AppAccess.Add)]
	public async Task<StudentRecordAppAccessDto> UpdateAsync(Guid studentId, string recipientKey, UpdateStudentRecordAppAccessDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_AppAccess.View);
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_AppAccess.Add);
		try
		{
			using var work = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true, isolationLevel: IsolationLevel.Serializable);
			var (student, showParents) = await EnsureAccessibleAsync(studentId);
			var reason = input.Reason?.Trim();
			if (reason == null || reason.Length is < 3 or > 400)
				throw new BusinessException(StudentRecordsErrorCodes.AppAccessReasonInvalid);
			var recipients = await GetRecipientsAsync(student, showParents);
			var index = recipients.FindIndex(recipient => string.Equals(recipient.Key, recipientKey, StringComparison.Ordinal));
			if (index < 0) throw new BusinessException(StudentRecordsErrorCodes.AppAccessConflict);
			var current = recipients[index];
			if (!string.Equals(input.ConcurrencyStamp, current.Stamp, StringComparison.Ordinal))
				throw new BusinessException(StudentRecordsErrorCodes.AppAccessConflict);
			if (current.Account is not { IsActive: true } account)
				throw new BusinessException(StudentRecordsErrorCodes.AppAccessRecipientUnavailable);
			if (current.IsEnabled != input.Enabled)
			{
				var restriction = await SaveRestrictionAsync(student, current, input.Enabled);
				var changedBy = await actor.EnsureActiveAsync();
				await events.InsertAsync(new StudentAppAccessEvent(GuidGenerator.Create(), student.TenantId!.Value, studentId,
					account.Id, current.Relationship, current.DisplayName, input.Enabled, reason, Clock.Now.ToUniversalTime(),
					changedBy.Id, NameOf(changedBy.Name, changedBy.Surname)));
				await work.SaveChangesAsync();
				if (!input.Enabled) await enforcement.InvalidateFullyBlockedSessionsAsync(current.AffectedUserIds);
				recipients[index] = current with { Restriction = restriction };
			}
			var result = await BuildAsync(studentId, recipients, showParents, canUpdate: true);
			await work.CompleteAsync();
			return result;
		}
		catch (Exception exception) when (IsWriteConflict(exception))
		{
			throw new BusinessException(StudentRecordsErrorCodes.AppAccessConflict, innerException: exception);
		}
	}

	private async Task<(Student Student, bool ShowParents)> EnsureAccessibleAsync(Guid studentId)
	{
		var record = await access.EnsureWritableAsync(studentId);
		var roles = await access.GetDatabaseRoleNamesAsync();
		if (record.Policy.IsMasked("Identity", roles)) throw new BusinessException(StudentRecordsErrorCodes.AppAccessIdentityRestricted);
		var student = record.Student;
		if (student.Status != StudentLifeCycleStatusEnum.Enrolled || !student.AcademicYearId.HasValue ||
			!await enrollments.AnyAsync(e => e.TenantId == CurrentTenant.Id && e.StudentId == studentId &&
				e.AcademicYearId == student.AcademicYearId.Value && e.IsActive))
			throw new BusinessException(StudentRecordsErrorCodes.AppAccessEnrollmentRequired);
		return (student, !record.Policy.IsMasked("Guardian", roles));
	}

	private async Task<List<Recipient>> GetRecipientsAsync(Student student, bool showParents)
	{
		var activeLinks = await links.GetListAsync(link => link.TenantId == student.TenantId && link.StudentId == student.Id &&
			(link.AccountType == "student" && link.Relationship == Self ||
			 showParents && link.AccountType == "guardian" && (link.Relationship == "father" || link.Relationship == "mother")));
		var selfLink = activeLinks.SingleOrDefault(link => link.AccountType == "student");
		var legacySelfId = student.UserId != selfLink?.UserId ? await GetLegacySelfUserAsync(student) : null;
		var selfUserId = selfLink?.UserId ?? legacySelfId;
		var parentLinks = activeLinks.Where(link => link.AccountType == "guardian")
			.OrderBy(link => link.Relationship, StringComparer.Ordinal).ThenBy(link => link.Id).DistinctBy(link => link.UserId).ToList();
		var selfIds = new[] { selfLink?.UserId, legacySelfId }.OfType<Guid>();
		var accountIds = parentLinks.Select(link => link.UserId).Concat(selfIds).Distinct().ToArray();
		var identities = accountIds.Length == 0 ? new Dictionary<Guid, IdentityUser>() : (await users.GetListAsync(user =>
			user.TenantId == student.TenantId && Enumerable.Contains(accountIds, user.Id))).ToDictionary(user => user.Id);
		var saved = await restrictions.GetListAsync(item => item.TenantId == student.TenantId && item.StudentId == student.Id &&
			(item.UserId == null || showParents && Enumerable.Contains(accountIds, item.UserId.Value)));
		var selfAccount = selfIds.Select(id => identities.GetValueOrDefault(id)).FirstOrDefault(user => user is { IsActive: true });
		var result = new List<Recipient>
		{
			new(Self, Self, NameOf(student.FirstName, student.MiddleName, student.LastName), selfUserId,
				selfLink?.Id, legacySelfId, selfAccount, saved.SingleOrDefault(r => r.UserId == null))
		};
		foreach (var link in parentLinks)
		{
			var user = identities.GetValueOrDefault(link.UserId);
			result.Add(new(link.UserId.ToString("N"), link.Relationship, NameOf(user?.Name, user?.Surname), link.UserId, link.Id, null,
				user, saved.SingleOrDefault(r => r.UserId == link.UserId)));
		}
		return result;
	}

	private async Task<Guid?> GetLegacySelfUserAsync(Student student)
	{
		if (!student.UserId.HasValue) return null;
		using (dataFilter.Disable<ISoftDelete>())
			if (await links.AnyAsync(link => link.TenantId == student.TenantId && link.StudentId == student.Id &&
				link.UserId == student.UserId && link.AccountType == "student" && link.Relationship == Self)) return null;
		return student.UserId;
	}

	private async Task<StudentAppAccessRestriction> SaveRestrictionAsync(Student student, Recipient current, bool enabled)
	{
		if (current.Restriction == null)
			return await restrictions.InsertAsync(new StudentAppAccessRestriction(GuidGenerator.Create(), student.TenantId!.Value,
				student.Id, current.Relationship == Self ? null : current.UserId, isOff: !enabled));
		current.Restriction.SetEnabled(enabled);
		return await restrictions.UpdateAsync(current.Restriction);
	}

	private async Task<StudentRecordAppAccessDto> BuildAsync(Guid studentId, List<Recipient> recipients, bool showParents, bool canUpdate)
	{
		var historyQuery = await events.GetQueryableAsync();
		var history = await AsyncExecuter.ToListAsync(historyQuery.Where(item => item.TenantId == CurrentTenant.Id && item.StudentId == studentId &&
			(showParents || item.Relationship == Self)).OrderByDescending(item => item.ChangedAt).ThenByDescending(item => item.Id)
			.Select(item => new StudentAppAccessHistoryDto(item.Id, item.Relationship, item.DisplayName, item.Enabled, item.Reason, item.ChangedAt, item.ChangedByName)));
		return new(studentId, recipients.Select(item => new StudentAppAccessRecipientDto(item.Key, item.Relationship, item.DisplayName,
			item.Account is not { IsActive: true } ? "not-set-up" : item.IsEnabled ? "on" : "off",
			canUpdate && item.Account is { IsActive: true }, item.Stamp)).ToList(), history);
	}

	private static string? NameOf(params string?[] parts)
	{
		var name = string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim()));
		return name.Length == 0 ? null : name;
	}

	private static bool IsWriteConflict(Exception exception)
	{
		for (Exception? current = exception; current != null; current = current.InnerException)
			if (current is AbpDbConcurrencyException or DbException { SqlState: "40001" or "40P01" or "23505" }) return true;
		return false;
	}

	private sealed record Recipient(string Key, string Relationship, string? DisplayName, Guid? UserId, Guid? LinkId, Guid? LegacySelfUserId,
		IdentityUser? Account, StudentAppAccessRestriction? Restriction)
	{
		public bool IsEnabled => Restriction?.IsOff != true;
		public IEnumerable<Guid> AffectedUserIds => new[] { UserId, LegacySelfUserId }.OfType<Guid>().Distinct();
		public string Stamp => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
			$"{Key}:{Relationship}:{UserId:N}:{LinkId:N}:{LegacySelfUserId:N}:{Account?.Id:N}:{Account?.IsActive}:{Restriction?.ConcurrencyStamp}")));
	}
}
