using System;
using System.Text.Json;
using System.Text;
using System.Security.Cryptography;
using Volo.Abp.Data;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.Portal;

/// <summary>One expiring email-change intent per existing account. No token or password is persisted.</summary>
internal sealed class StudentPortalEmailChangeState
{
	private const string Key = "StudentPortalEmailChange";
	public Guid RequestId { get; set; }
	public Guid CaseId { get; set; }
	public Guid UserId { get; set; }
	public Guid ActorId { get; set; }
	public string Email { get; set; } = string.Empty;
	public string PreviousEmail { get; set; } = string.Empty;
	public string PreviousUserName { get; set; } = string.Empty;
	public string SecurityStampHash { get; set; } = string.Empty;
	public string Topology { get; set; } = string.Empty;
	public DateTime RequestedAt { get; set; }
	public DateTime ExpiresAt { get; set; }
	public string Status { get; set; } = "Pending";
	public static StudentPortalEmailChangeState? Read(IdentityUser user)
	{
		try { return JsonSerializer.Deserialize<StudentPortalEmailChangeState>(user.GetProperty<string>(Key) ?? "null"); }
		catch (JsonException) { return null; }
	}
	public void Save(IdentityUser user) => user.SetProperty(Key, JsonSerializer.Serialize(this));
	public bool IsCurrent(IdentityUser user, DateTime now) => UserId == user.Id && ExpiresAt > now &&
		StudentPortalProfileEmail.Same(user.Email, PreviousEmail) && user.UserName == PreviousUserName && Fingerprint(user.SecurityStamp) == SecurityStampHash;
	internal static string Fingerprint(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
