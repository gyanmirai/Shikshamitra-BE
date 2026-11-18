using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Students;

namespace ShikshaMitra.Academic.Notifications;

internal static partial class AcademicNotificationFamilyRecipients
{
	public static IEnumerable<NotificationRecipientEligibilityDto> Resolve(AcademicNotificationStudent row,
		Guid tenantId, AcademicNotificationRecipientPolicy policy, IReadOnlyList<CommunicationChannelType> channels,
		IReadOnlyList<string> selectors)
	{
		if (row.Family != null && row.Family.TenantId != tenantId) throw new Volo.Abp.Authorization.AbpAuthorizationException();
		var primary = StudentFamilyContactSelection.Resolve(row.Student, row.Family);
		if (selectors.Contains("PrimaryGuardian"))
		{
			if (primary == null)
				foreach (var channel in channels.Distinct())
					yield return new(new("Guardian", row.Student.Id), channel, false, NotificationErrorCodes.PrimaryGuardianNotConfigured);
			var relationships = (primary == null ? Enumerable.Empty<string>() : [primary])
				.Concat(policy.GuardianMode == NotificationGuardianMode.PrimaryAndSelected ? policy.AdditionalGuardianRelationships : [])
				.Distinct(StringComparer.Ordinal);
			foreach (var relationship in relationships)
			{
				var profile = StudentFamilyContactSelection.Profile(row.Family, relationship);
				var accounts = Accounts(row, tenantId, "guardian", relationship);
				var account = accounts.Length == 1 ? accounts[0] : null;
				var recipient = new NotificationRecipientRefDto("Guardian", row.Student.Id, relationship, account?.User.Id);
				var revision = Revision(row, relationship, account);
				foreach (var channel in channels.Distinct())
				{
					var reason = row.GuardianReviewPending ? "GuardianAccessReviewPending" :
						!StudentFamilyContactSelection.Present(profile) ? "GuardianRelationshipUnavailable" :
						accounts.Length > 1 && channel is CommunicationChannelType.InApp or CommunicationChannelType.WebPush
							? "AccountAssociationAmbiguous" : ChannelExclusion(channel, profile?.Email, profile?.ContactNo, account != null);
					yield return new(recipient, channel, reason == null, reason, revision);
				}
			}
		}
		if (!policy.IncludeStudentAccount || !selectors.Contains("StudentAccount")) yield break;
		var students = Accounts(row, tenantId, "student", "self");
		var self = students.Length == 1 ? students[0] : null;
		foreach (var channel in channels.Distinct())
		{
			var reason = self == null ? "VerifiedAccountRequired" : ChannelExclusion(channel,
				self.User.EmailConfirmed ? self.User.Email : null, self.User.PhoneNumberConfirmed ? self.User.PhoneNumber : null, true);
			yield return new(new("StudentAccount", row.Student.Id, "self", self?.User.Id), channel,
				reason == null, reason, Revision(row, "self", self));
		}
	}

	private static AcademicNotificationAccount[] Accounts(AcademicNotificationStudent row, Guid tenantId, string type, string relationship) =>
		row.Accounts.Where(x => x.Link.TenantId == tenantId && x.Link.StudentId == row.Student.Id &&
			x.Link.AccountType == type && x.Link.Relationship == relationship && !x.Link.IsDeleted &&
			x.Link.UserId == x.User.Id && x.User.TenantId == tenantId && !x.User.IsDeleted && x.User.IsActive &&
			(!x.User.LockoutEnabled || x.User.LockoutEnd == null || x.User.LockoutEnd <= DateTimeOffset.UtcNow)).ToArray();

	internal static string? ChannelExclusion(CommunicationChannelType channel, string? email, string? phone, bool account) => channel switch
	{
		CommunicationChannelType.InApp or CommunicationChannelType.WebPush => account ? null : "VerifiedAccountRequired",
		CommunicationChannelType.Email => string.IsNullOrWhiteSpace(email) ? "EmailMissing" :
			MailAddress.TryCreate(email, out var parsed) && string.Equals(parsed.Address, email, StringComparison.Ordinal) ? null : "EmailInvalid",
		CommunicationChannelType.WhatsApp => string.IsNullOrWhiteSpace(phone) ? "PhoneMissing" : E164().IsMatch(phone) ? null : "PhoneInvalid",
		_ => "ChannelUnsupported"
	};

	private static string Revision(AcademicNotificationStudent row, string relationship, AcademicNotificationAccount? account)
	{
		var profile = StudentFamilyContactSelection.Profile(row.Family, relationship);
		var payload = JsonSerializer.Serialize(new {
			row.Student.Id, row.Student.TenantId, row.Student.Status, row.Student.AcademicYearId, row.Student.DivisionId,
			Relationship = relationship, Primary = StudentFamilyContactSelection.Resolve(row.Student, row.Family),
			profile?.FullName, profile?.Email, profile?.ContactNo, row.GuardianReviewPending,
			Link = account?.Link.Id, User = account?.User.Id, account?.Link.ConcurrencyStamp,
			UserEmail = account?.User.Email, UserPhone = account?.User.PhoneNumber,
			account?.User.IsActive, account?.User.EmailConfirmed, account?.User.PhoneNumberConfirmed
		});
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
	}

	[GeneratedRegex(@"^\+[1-9]\d{7,14}$", RegexOptions.CultureInvariant)]
	private static partial Regex E164();
}
