using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Communication;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Portal;
using Volo.Abp.Authorization;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Notifications;

public interface ILibraryNotificationRecipientResolver
{
	Task<bool> IsCurrentDestinationAsync(LibraryNotificationHistory row);
	Task<bool> HasOptedOutAsync(LibraryNotificationHistory row);
}

public class LibraryNotificationRecipientResolver(
	IRepository<Student, Guid> studentRepository,
	IRepository<Employee, Guid> employeeRepository,
	IRepository<LibraryMember, Guid> libraryMemberRepository,
	IRepository<StudentPortalAccountLink, Guid> links,
	IIdentityUserRepository users,
	StudentPortalAccessService portal,
	LibraryNotificationPreferences preferences,
	ILibraryNotificationAuthorityService authority,
	ILibraryNotificationReminderEligibility eligibility,
	IRepository<ConsentRecord, Guid> consents,
	ICurrentTenant tenant,
	IAsyncQueryableExecuter asyncExecuter,
	LibraryNotificationContactReader contacts)
	: ILibraryNotificationRecipientResolver, ITransientDependency
{
	public async Task<LibraryNotificationRecipient> ResolveMemberAsync(
		Guid memberId,
		int? memberRole = null,
		string? fallbackName = null,
		string? fallbackEmail = null)
	{
		if (memberRole is null or 0)
		{
			var alias = await libraryMemberRepository.FindAsync(memberId);
			if (alias != null && alias.TenantId == tenant.Id) memberId = alias.StudentId;
			var student = await contacts.StudentAsync(memberId);
			if (student != null)
			{
				var membershipQuery = await libraryMemberRepository.GetQueryableAsync();
				var currentMembershipActive = await asyncExecuter.FirstOrDefaultAsync(membershipQuery
					.Where(member => member.TenantId == tenant.Id && member.StudentId == student.Id)
					.OrderByDescending(member => member.CreationTime).ThenBy(member => member.Id)
					.Select(member => (bool?)member.IsActive));
				return new LibraryNotificationRecipient(
					student.Id,
					LibraryNotificationRecipientRoles.Member,
					$"{student.FirstName} {student.MiddleName} {student.LastName}".Trim(),
					PrimaryEmail(student),
					student.Phone,
					student.Status != StudentLifeCycleStatusEnum.Left && student.IsLibraryMember && currentMembershipActive != false,
					MemberName: StudentName(student), MemberRole: 0);
			}
		}

		if (memberRole is null or 1 or 2)
		{
			var employee = await contacts.EmployeeAsync(memberId);
			if (employee != null)
			{
				return new LibraryNotificationRecipient(
					employee.Id,
					LibraryNotificationRecipientRoles.Member,
					$"{employee.FirstName} {employee.LastName}".Trim(),
					employee.Email,
					employee.Phone,
						employee.IsLibraryMember,
						MemberName: $"{employee.FirstName} {employee.LastName}".Trim(), MemberRole: 1);
			}
		}

		return new LibraryNotificationRecipient(
			memberId,
			LibraryNotificationRecipientRoles.Member,
			fallbackName,
			fallbackEmail,
			Phone: null,
			IsActiveLibraryMember: false, MemberRole: memberRole);
	}

	public async Task<List<LibraryNotificationRecipient>> ResolveDestinationsAsync(LibraryNotificationRecipient source, string channel)
	{
		if (!source.MemberId.HasValue)
		{
			if (source.Kind != "Operational") return [source with { Kind = "Direct" }];
			var operational = new List<LibraryNotificationRecipient>();
			foreach (var userId in await preferences.GetOperationalIdsAsync())
			{
				var user = await users.FindAsync(userId);
				if (user != null && user.TenantId == tenant.Id && user.IsActive && await authority.CanReceiveOperationalAsync(userId))
					operational.Add(source with { UserId = userId, Email = user.Email, RecipientName = user.UserName });
			}
			return operational;
		}
		var student = source.MemberRole is null or 0 ? await contacts.StudentAsync(source.MemberId.Value) : null;
		if (student != null)
		{
			if (channel == LibraryNotificationChannels.Email) return EmailDestinations(student, source);
			if (channel != LibraryNotificationChannels.InApp) return [source];
			var routing = await preferences.GetRoutingAsync();
			var currentLinks = await links.GetListAsync(link => link.TenantId == tenant.Id && link.StudentId == student.Id);
			var result = new List<LibraryNotificationRecipient>();
			if (routing != LibraryStudentInboxRouting.Student)
			{
				var relationships = StudentFamilyContactSelection.MessageRelationships(
					currentLinks.Where(link => link.AccountType == "guardian").Select(link => link.Relationship).Distinct().ToArray(),
					student.GetProperty<string>(StudentFamilyContactSelection.SelectionKey));
				foreach (var link in currentLinks.Where(link => link.AccountType == "guardian" && relationships.Contains(link.Relationship)))
				{
					var user = await users.FindAsync(link.UserId);
					if (CanUsePortal(user)) result.Add(source with { UserId = user!.Id, Email = user.Email,
						RecipientName = user.UserName, Kind = "VerifiedGuardian", Relationship = link.Relationship });
				}
			}
			if (routing != LibraryStudentInboxRouting.Guardians)
			{
				var candidateIds = currentLinks.Where(link => link.AccountType == "student" && link.Relationship == "self").Select(link => link.UserId)
					.Concat(student.UserId.HasValue ? [student.UserId.Value] : Array.Empty<Guid>()).Distinct();
				foreach (var userId in candidateIds)
				{
					try
					{
						if (!(await portal.GetStudentsForUserAsync(userId)).Any(account => account.StudentId == student.Id && account.AccountType == "student")) continue;
						var user = await users.FindAsync(userId);
						if (CanUsePortal(user)) result.Add(source with { UserId = userId, Email = user!.Email, Kind = "VerifiedStudent", Relationship = "self" });
					}
					catch (AbpAuthorizationException) { }
				}
			}
			return result.DistinctBy(recipient => recipient.UserId).ToList();
		}
		var employee = source.MemberRole is null or 1 or 2 ? await contacts.EmployeeAsync(source.MemberId.Value) : null;
		if (employee == null) return [];
		var identity = employee.UserId.HasValue ? await users.FindAsync(employee.UserId.Value) : null;
		return [source with { Kind = "Employee", UserId = identity != null && identity.TenantId == tenant.Id && identity.IsActive ? identity.Id : null }];
	}

	public async Task<bool> IsCurrentDestinationAsync(LibraryNotificationHistory row)
	{
		if (!row.ExtraProperties.TryGetValue("RecipientDestinationSignature", out var saved)) return false;
		if (!row.RecipientMemberId.HasValue && row.ExtraProperties.GetValueOrDefault("RecipientKind")?.ToString() == "Direct")
			return saved?.ToString() == DestinationSignature(tenant.Id, new LibraryNotificationRecipient(null, row.RecipientRole,
				row.RecipientName, row.RecipientEmail, null, true, Kind: "Direct"), row.Channel);
		int? role = row.EventType == LibraryNotificationEventTypes.FineDueReminder && row.RelatedEntityId.HasValue
			? row.ExtraProperties.GetValueOrDefault("RecipientKind")?.ToString() == "Employee" ? 1 : 0
			: null;
		var source = row.RecipientMemberId.HasValue ? await ResolveMemberAsync(row.RecipientMemberId.Value, role) : BuildOperationalRecipient("Library staff");
		if (!source.IsActiveLibraryMember && !await eligibility.AllowsRecoveryAsync(row)) return false;
		return (await ResolveDestinationsAsync(source, row.Channel)).Any(recipient =>
			DestinationSignature(tenant.Id, recipient, row.Channel) == saved?.ToString());
	}

	public async Task<LibraryInboxMemberAccess> GetInboxMemberAccessAsync(Guid userId)
	{
		var linked = (await links.GetListAsync(link => link.TenantId == tenant.Id && link.UserId == userId)).Select(link => link.StudentId).Distinct().ToArray();
		var studentIds = await asyncExecuter.ToListAsync((await studentRepository.GetQueryableAsync()).Where(student => student.TenantId == tenant.Id &&
			(student.UserId == userId || Enumerable.Contains(linked, student.Id))).Select(student => student.Id));
		var employeeIds = await asyncExecuter.ToListAsync((await employeeRepository.GetQueryableAsync())
			.Where(employee => employee.TenantId == tenant.Id && employee.UserId == userId).Select(employee => employee.Id));
		var activeIds = new HashSet<Guid>();
		var inactiveIds = new HashSet<Guid>();
		foreach (var memberId in studentIds.Concat(employeeIds))
		{
			var member = await ResolveMemberAsync(memberId);
			if ((await ResolveDestinationsAsync(member, LibraryNotificationChannels.InApp)).Any(recipient => recipient.UserId == userId))
				(member.IsActiveLibraryMember ? activeIds : inactiveIds).Add(memberId);
		}
		return new(activeIds, inactiveIds);
	}

	public async Task<bool> CanReadOperationalInboxAsync(Guid userId) =>
		(await preferences.GetOperationalIdsAsync()).Contains(userId) && await authority.CanReceiveOperationalAsync(userId);

	public async Task<bool> HasOptedOutAsync(LibraryNotificationRecipient recipient)
	{
		if (string.IsNullOrWhiteSpace(recipient.Email)) return false;
		var email = recipient.Email.Trim().ToLowerInvariant();
		return await consents.AnyAsync(consent => consent.TenantId == tenant.Id && consent.Channel == CommunicationChannelType.Email &&
			consent.Destination.Trim().ToLower() == email && (consent.RecipientId == recipient.MemberId || consent.RecipientId == null) &&
			(consent.Status == CommunicationConsentStatus.OptedOut || consent.Status == CommunicationConsentStatus.Revoked));
	}

	public Task<bool> HasOptedOutAsync(LibraryNotificationHistory row) => HasOptedOutAsync(new LibraryNotificationRecipient(
		row.RecipientMemberId, row.RecipientRole, row.RecipientName, row.RecipientEmail, null, true));

	public static string DestinationSignature(Guid? tenantId, LibraryNotificationRecipient recipient, string channel) =>
		Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
		{
			TenantId = tenantId, recipient.MemberId, recipient.UserId, recipient.Kind, recipient.Relationship, Channel = channel,
			Email = channel == LibraryNotificationChannels.Email ? recipient.Email?.Trim().ToLowerInvariant() : null
		}))));

	private bool CanUsePortal(IdentityUser? user) => user != null && user.TenantId == tenant.Id && user.IsActive &&
		user.EmailConfirmed && !user.ShouldChangePasswordOnNextLogin;

	private static List<LibraryNotificationRecipient> EmailDestinations(LibraryNotificationStudentFacts student, LibraryNotificationRecipient source)
	{
		var available = new[] { "father", "mother", "guardian" }.Where(role => student.Profile(role)?.IsPresent == true).ToArray();
		var selected = student.GetProperty<string>(StudentFamilyContactSelection.SelectionKey);
		var relationships = StudentFamilyContactSelection.MessageRelationships(available, selected);
		if (relationships.Count == 0 && string.IsNullOrWhiteSpace(selected))
			return [source with { Email = student.Email, Kind = "LegacySchoolCommunication" }];
		return relationships.Select(relationship =>
		{
			var profile = student.Profile(relationship);
			return source with { Email = profile?.Email, RecipientName = profile?.FullName, Kind = "SavedGuardianContact", Relationship = relationship };
		}).DistinctBy(recipient => recipient.Email?.Trim().ToLowerInvariant()).ToList();
	}

	private static string? PrimaryEmail(LibraryNotificationStudentFacts student) => EmailDestinations(student, new LibraryNotificationRecipient(
		student.Id, LibraryNotificationRecipientRoles.Member, StudentName(student), null, null, true)).FirstOrDefault()?.Email;
	private static string StudentName(LibraryNotificationStudentFacts student) => student.Name;

	public static LibraryNotificationRecipient BuildOperationalRecipient(string name) =>
		new(
			MemberId: null,
			RecipientRole: LibraryNotificationRecipientRoles.Librarian,
			RecipientName: name,
			Email: null,
			Phone: null,
			IsActiveLibraryMember: true,
			Kind: "Operational");
}

public sealed record LibraryNotificationRecipient(
	Guid? MemberId,
	string RecipientRole,
	string? RecipientName,
	string? Email,
	string? Phone,
	bool IsActiveLibraryMember,
	Guid? UserId = null,
	string Kind = "Member",
	string? Relationship = null,
	string? MemberName = null,
	int? MemberRole = null);

public sealed record LibraryInboxMemberAccess(HashSet<Guid> ActiveIds, HashSet<Guid> InactiveIds);
