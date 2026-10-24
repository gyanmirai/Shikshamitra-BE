using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Portal;

/// <summary>Transactional email intent. Secrets are encrypted only until delivery is accepted.</summary>
public class StudentCredentialEmailRequest : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid CaseId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid LinkId { get; private set; }
	public Guid UserId { get; private set; }
	public Guid? RequestedByUserId { get; private set; }
	public string AccountType { get; private set; } = string.Empty;
	public string Email { get; private set; } = string.Empty;
	public string Status { get; private set; } = "Pending";
	public string? Message { get; private set; }
	public string? EncryptedInitialPassword { get; private set; }
	public int AttemptCount { get; private set; }
	public DateTime RequestedAt { get; private set; }
	public DateTime NextAttemptAt { get; private set; }
	public DateTime? SentAt { get; private set; }
	public DateTime? ClaimedAt { get; private set; }
	public bool RequiresSavedOwnerValidation() => ExtraProperties.TryGetValue("SavedOwnerCredentialsRequested", out var value) &&
		bool.TryParse(value?.ToString(), out var required) && required;
	protected StudentCredentialEmailRequest() { }
	public StudentCredentialEmailRequest(Guid id, Guid tenantId, Guid caseId, Guid studentId, Guid linkId,
		Guid userId, string accountType, string email, Guid? actor, DateTime now) : base(id)
	{
		if (accountType is not ("student" or "guardian")) throw new ArgumentException("Invalid account type.", nameof(accountType));
		if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Email is required.", nameof(email));
		TenantId = tenantId; CaseId = caseId; StudentId = studentId; LinkId = linkId; UserId = userId;
		AccountType = accountType; Email = email; RequestedByUserId = actor; RequestedAt = NextAttemptAt = now;
	}
	public bool IsDue(DateTime now) => Status == "Pending" && NextAttemptAt <= now;
	public void RequireSavedOwnerValidation(Guid? actor = null)
	{
		ExtraProperties["SavedOwnerCredentialsRequested"] = true;
		if (actor.HasValue) RequestedByUserId = actor;
	}
	public bool CanRequest(DateTime now) => Status is not ("Pending" or "Processing") && RequestedAt.AddMinutes(2) <= now;
	public void DeferUntilCompletion(DateTime now)
	{
		if (Status != "Pending") throw new InvalidOperationException("Only a pending email can wait for onboarding completion.");
		NextAttemptAt = now.AddMinutes(1);
		Message = "Credentials are queued for onboarding completion.";
	}
	public void Requeue(Guid? actor, DateTime now)
	{
		if (!CanRequest(now)) throw new InvalidOperationException("An email is already pending or was requested recently.");
		RequestedByUserId = actor; RequestedAt = NextAttemptAt = now; Status = "Pending"; Message = null;
		// This is a new intent. Only the authorized one-click command may mark its
		// request as explicit manual delivery again; legacy resend keeps school flags.
		ExtraProperties.Remove("SavedOwnerCredentialsRequested");
	}
	public void Claim(DateTime now)
	{
		if (!IsDue(now)) throw new InvalidOperationException("Email is not due.");
		Status = "Processing"; ClaimedAt = now; AttemptCount++;
	}
	/// <summary>A new explicit intent after an owner-verified address change; never resend stale secrets.</summary>
	public void RequeueForRecipient(Guid? actor, DateTime now, string email)
	{
		if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Email is required.", nameof(email));
		var previousEmail = Email;
		Requeue(actor, now);
		if (!string.Equals(previousEmail, email.Trim(), StringComparison.OrdinalIgnoreCase))
		{
			ExtraProperties["PreviousRecipientEmail"] = previousEmail;
			Email = email.Trim();
			ClearInitialPassword();
		}
	}
	public void SetInitialPassword(string encryptedPassword) => EncryptedInitialPassword = encryptedPassword;
	public void ClearInitialPassword() => EncryptedInitialPassword = null;
	public void Sent(DateTime now)
	{
		Status = "Sent"; SentAt = now; Message = "Email accepted by the configured sender.";
		EncryptedInitialPassword = null; ClaimedAt = null;
	}
	public void Failed(DateTime now)
	{
		// SMTP exceptions may contain recipient/token data; persist a fixed safe message.
		Status = "Failed"; Message = "Email could not be sent. Review school email configuration and retry."; ClaimedAt = null;
		NextAttemptAt = now.AddMinutes(2);
	}
	public void Cancel(string reason)
	{
		Status = "Cancelled"; Message = reason; EncryptedInitialPassword = null; ClaimedAt = null;
	}
	public void RecoverInterrupted(DateTime now)
	{
		if (Status != "Processing" || !ClaimedAt.HasValue || ClaimedAt.Value.AddMinutes(10) > now) return;
		Status = "Failed"; Message = "Sending was interrupted. Check the recipient mailbox before retrying."; ClaimedAt = null;
	}
}
