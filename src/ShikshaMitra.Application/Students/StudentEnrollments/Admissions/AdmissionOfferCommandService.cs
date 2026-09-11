using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionOfferCommandService(
	IRepository<AdmissionApplication, Guid> applications,
	IRepository<AdmissionApplicationDecisionHistory, Guid> history,
	IRepository<AdmissionActivity, Guid> activities,
	AdmissionOfferAccessService access,
	AdmissionOfferReadService reader,
	AdmissionReviewDecisionGuard reviewGuard) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionOfferStageDto> IssueAsync(Guid applicationId, Guid stageId, IssueAdmissionOfferDto input)
	{
		var context = await ManagedAsync(applicationId, stageId);
		var key = Key(input.IdempotencyKey);
		var reason = Optional(input.Reason, 512, "Offer note cannot exceed 512 characters.");
		var fingerprint = Fingerprint("issue", new { ExpiresAt = input.ExpiresAt?.ToUniversalTime(), Reason = reason });
		if (await IsRetryAsync(context, key, "issue", fingerprint)) return await reader.GetAsync(applicationId, stageId);
		AdmissionOfferAccessService.EnsureConcurrency(context.Application, input.ConcurrencyStamp);
		if (context.Application.Status != AdmissionApplicationStatus.Approved)
			throw Invalid("Approve the application before issuing its offer.");
		await reviewGuard.EnsureApplicationCompleteAsync(context.Application);
		var expiresAt = Expiry(context.Policy!, input.ExpiresAt);
		context.Application.IssueOffer(expiresAt, CurrentUser.Id, reason);
		return await SaveAsync(context, "issue", key, fingerprint, reason, new { expiresAt, VersionNumber = 1 });
	}

	public async Task<AdmissionOfferStageDto> RespondAsync(Guid applicationId, Guid stageId, RespondAdmissionOfferDto input)
	{
		var context = await ManagedAsync(applicationId, stageId);
		var key = Key(input.IdempotencyKey);
		var policy = context.Policy!;
		var reason = !input.Accepted && policy.RequireDeclineReason
			? Required(input.Reason, 512, "Enter the reason for declining the offer.")
			: Optional(input.Reason, 512, "Offer response note cannot exceed 512 characters.");
		var action = input.Accepted ? "accept" : "decline";
		var fingerprint = Fingerprint(action, new
		{
			input.Accepted, Reason = reason, input.ConfirmTermsAccepted, input.ConfirmConditionsSatisfied
		});
		if (await IsRetryAsync(context, key, action, fingerprint)) return await reader.GetAsync(applicationId, stageId);
		AdmissionOfferAccessService.EnsureConcurrency(context.Application, input.ConcurrencyStamp);
		if (policy.RequireTermsAcceptance && input.Accepted && !input.ConfirmTermsAccepted)
			throw new BusinessException(AdmissionOfferStageErrorCodes.ConfirmationRequired, "Confirm that the offer terms were accepted.");
		if (policy.RequireConditionsConfirmation && input.Accepted && !input.ConfirmConditionsSatisfied)
			throw new BusinessException(AdmissionOfferStageErrorCodes.ConfirmationRequired, "Confirm that the conditional offer requirements were satisfied.");
		if (input.Accepted) context.Application.AcceptOffer(CurrentUser.Id, reason);
		else context.Application.DeclineOffer(CurrentUser.Id, reason ?? "Offer declined.");
		return await SaveAsync(context, action, key, fingerprint, reason,
			new { input.ConfirmTermsAccepted, input.ConfirmConditionsSatisfied });
	}

	public async Task<AdmissionOfferStageDto> ExtendAsync(Guid applicationId, Guid stageId, ExtendAdmissionOfferDto input)
	{
		var context = await ManagedAsync(applicationId, stageId);
		var key = Key(input.IdempotencyKey);
		var reason = Optional(input.Reason, 512, "Extension note cannot exceed 512 characters.");
		var fingerprint = Fingerprint("extend", new { ExpiresAt = input.ExpiresAt.ToUniversalTime(), Reason = reason });
		if (await IsRetryAsync(context, key, "extend", fingerprint)) return await reader.GetAsync(applicationId, stageId);
		AdmissionOfferAccessService.EnsureConcurrency(context.Application, input.ConcurrencyStamp);
		var state = await reader.GetAsync(applicationId, stageId);
		if (context.Policy?.AllowExtension != true || !state.CanExtend)
			throw new BusinessException(AdmissionOfferStageErrorCodes.ExtensionNotAllowed, "This offer cannot be extended again.");
		var expiresAt = Expiry(context.Policy, input.ExpiresAt);
		context.Application.ExtendOffer(expiresAt);
		return await SaveAsync(context, "extend", key, fingerprint, reason, new { expiresAt });
	}

	public async Task<AdmissionOfferStageDto> WithdrawAsync(Guid applicationId, Guid stageId, AdmissionOfferReasonDto input)
	{
		var context = await ManagedAsync(applicationId, stageId);
		var key = Key(input.IdempotencyKey);
		var reason = Required(input.Reason, 512, "Enter the reason for withdrawing the offer.");
		var fingerprint = Fingerprint("withdraw", new { Reason = reason });
		if (await IsRetryAsync(context, key, "withdraw", fingerprint)) return await reader.GetAsync(applicationId, stageId);
		AdmissionOfferAccessService.EnsureConcurrency(context.Application, input.ConcurrencyStamp);
		context.Application.WithdrawOffer(CurrentUser.Id, reason);
		return await SaveAsync(context, "withdraw", key, fingerprint, reason, null);
	}

	public async Task<AdmissionOfferStageDto> ReissueAsync(Guid applicationId, Guid stageId, IssueAdmissionOfferDto input)
	{
		var context = await ManagedAsync(applicationId, stageId);
		var key = Key(input.IdempotencyKey);
		var reason = Optional(input.Reason, 512, "Reissue note cannot exceed 512 characters.");
		var fingerprint = Fingerprint("reissue", new { ExpiresAt = input.ExpiresAt?.ToUniversalTime(), Reason = reason });
		if (await IsRetryAsync(context, key, "reissue", fingerprint)) return await reader.GetAsync(applicationId, stageId);
		AdmissionOfferAccessService.EnsureConcurrency(context.Application, input.ConcurrencyStamp);
		var state = await reader.GetAsync(applicationId, stageId);
		if (context.Policy?.AllowReissue != true || !state.CanReissue)
			throw new BusinessException(AdmissionOfferStageErrorCodes.ReissueNotAllowed, "This offer cannot be reissued again.");
		var expiresAt = Expiry(context.Policy, input.ExpiresAt);
		context.Application.ReissueOffer(expiresAt, CurrentUser.Id, reason);
		return await SaveAsync(context, "reissue", key, fingerprint, reason,
			new { expiresAt, VersionNumber = state.VersionNumber + 1 });
	}

	public async Task<bool> TryIssueLegacyAsync(Guid applicationId, AdmissionApplicationDecisionDto input)
	{
		var context = await access.FindCurrentManagedAsync(applicationId);
		if (context == null) return false;
		await IssueAsync(applicationId, context.Stage.Id, new IssueAdmissionOfferDto
		{
			ExpiresAt = context.Policy!.AllowExpiryOverride ? input.OfferExpiresAt : null,
			Reason = input.Reason, ConcurrencyStamp = input.ConcurrencyStamp,
			IdempotencyKey = LegacyKey("issue", input)
		});
		return true;
	}

	public async Task<bool> TryRespondLegacyAsync(Guid applicationId, AdmissionApplicationDecisionDto input, bool accepted)
	{
		var context = await access.FindCurrentManagedAsync(applicationId);
		if (context == null) return false;
		await RespondAsync(applicationId, context.Stage.Id, new RespondAdmissionOfferDto
		{
			Accepted = accepted, Reason = input.Reason, ConcurrencyStamp = input.ConcurrencyStamp,
			ConfirmTermsAccepted = input.ConfirmTermsAccepted,
			ConfirmConditionsSatisfied = input.ConfirmConditionsSatisfied,
			IdempotencyKey = LegacyKey(accepted ? "accept" : "decline", input)
		});
		return true;
	}

	private async Task<AdmissionOfferStageDto> SaveAsync(
		AdmissionOfferContext context, string action, string key, string fingerprint, string? reason, object? metadata)
	{
		var metadataJson = JsonSerializer.Serialize(new
		{
			RequestFingerprint = fingerprint, Offer = context.Policy, Details = metadata
		});
		await applications.UpdateAsync(context.Application, false);
		await history.InsertAsync(new AdmissionApplicationDecisionHistory(GuidGenerator.Create(), CurrentTenant.Id,
			context.Application.Id, context.Application.Status, reason, CurrentUser.Id, action, context.Stage.Id, key, metadataJson), false);
		await activities.InsertAsync(new AdmissionActivity(GuidGenerator.Create(), CurrentTenant.Id,
			$"AdmissionOffer{char.ToUpperInvariant(action[0])}{action[1..]}", $"{context.Stage.Name}: offer {action} recorded.",
			context.Application.ApplicantId, context.Application.Id, metadataJson: metadataJson), false);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await reader.GetAsync(context.Application.Id, context.Stage.Id);
	}

	private async Task<AdmissionOfferContext> ManagedAsync(Guid applicationId, Guid stageId)
	{
		var context = await access.GetContextAsync(applicationId, stageId);
		if (context.Policy == null || !context.IsActive) throw Invalid("The managed Offer stage must be active.");
		return context;
	}

	private async Task<bool> IsRetryAsync(
		AdmissionOfferContext context, string key, string action, string fingerprint)
	{
		var existing = await history.FindAsync(item => item.ApplicationId == context.Application.Id &&
			item.StageInstanceId == context.Stage.Id && item.IdempotencyKey == key);
		if (existing == null) return false;
		var storedFingerprint = ReadFingerprint(existing.MetadataJson);
		if (existing.Action == action && storedFingerprint == fingerprint) return true;
		throw new BusinessException(AdmissionOfferStageErrorCodes.IdempotencyConflict);
	}

	private DateTime Expiry(AdmissionOfferStagePolicy policy, DateTime? requested)
	{
		var now = Clock.Now;
		var expiresAt = requested?.ToUniversalTime() ?? now.AddDays(policy.ValidityDays);
		var days = (expiresAt - now).TotalDays;
		if (expiresAt <= now || days < policy.MinimumValidityDays - 0.01 || days > policy.MaximumValidityDays + 0.01)
			throw new BusinessException(AdmissionOfferStageErrorCodes.ExpiryInvalid,
				$"Choose an expiry between {policy.MinimumValidityDays} and {policy.MaximumValidityDays} days from now.");
		if (!policy.AllowExpiryOverride && requested.HasValue && Math.Abs(days - policy.ValidityDays) > 0.01)
			throw new BusinessException(AdmissionOfferStageErrorCodes.ExpiryInvalid, "This stage uses its configured offer validity.");
		return expiresAt;
	}

	private static string LegacyKey(string action, AdmissionApplicationDecisionDto input) =>
		string.IsNullOrWhiteSpace(input.IdempotencyKey) ? $"legacy:{action}:{input.ConcurrencyStamp}" : input.IdempotencyKey;
	private static string Key(string? value) => Required(value, 80, "Enter an idempotency key.");
	private static string? Optional(string? value, int max, string message) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length > max ? throw Invalid(message) : value.Trim();
	private static string Fingerprint(string action, object payload) => Convert.ToHexString(
		SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { action, payload })))).ToLowerInvariant();
	private static string? ReadFingerprint(string? metadata)
	{
		if (string.IsNullOrWhiteSpace(metadata)) return null;
		using var json = JsonDocument.Parse(metadata);
		return json.RootElement.TryGetProperty("RequestFingerprint", out var value) ? value.GetString() : null;
	}
	private static string Required(string? value, int max, string message) =>
		string.IsNullOrWhiteSpace(value) || value.Trim().Length > max ? throw Invalid(message) : value.Trim();
	private static BusinessException Invalid(string message) => new(AdmissionOfferStageErrorCodes.InvalidState, message);
}
