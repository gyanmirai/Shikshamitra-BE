using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPublicOtpWorkflow(
	AdmissionCrmPublicAccess accessWorkflow,
	AdmissionCrmPublicFamilyDirectory familyDirectory,
	AdmissionCrmPublicTokenService tokenService,
	AdmissionCrmPublicOtpDeliveryWorkflow deliveryWorkflow,
	AdmissionCrmPublicStatusComposer statusComposer,
	ILogger<AdmissionCrmPublicOtpWorkflow> logger) : ApplicationService, ITransientDependency
{
	private const int OtpExpiryMinutes = 10;
	private const int OtpResendCooldownSeconds = 60;
	private const int AccessSessionMinutes = 30;

	public async Task<AdmissionApplicantStatusOtpChallengeDto> IssueStatusAsync(Guid tenantId, string token)
	{
		using (CurrentTenant.Change(tenantId))
		{
			var (access, lead) = await accessWorkflow.GetAsync(token);
			var now = Clock.Now;
			access.EnsureCanIssueOtp(now, TimeSpan.FromSeconds(OtpResendCooldownSeconds));
			var otp = AdmissionCrmPublicTokenService.CreateOtpCode();
			access.IssueOtp(
				AdmissionCrmPublicTokenService.HashSecret($"{token.Trim()}:{otp}"),
				tokenService.EncryptRequired(otp), now, now.AddMinutes(OtpExpiryMinutes));
			var delivery = await deliveryWorkflow.DispatchAsync(lead, otp, now);
			await accessWorkflow.SaveAsync(access);
			return Challenge(access, AdmissionCrmPublicTokenService.MaskDestination(lead), delivery);
		}
	}

	public async Task<AdmissionApplicantStatusOtpChallengeDto> IssueFamilyAsync(
		Guid tenantId, StartAdmissionFamilyAccessDto input)
	{
		using (CurrentTenant.Change(tenantId))
		{
			var now = Clock.Now;
			var response = new AdmissionApplicantStatusOtpChallengeDto
			{
				ChallengeId = GuidGenerator.Create(),
				MaskedDestination = AdmissionCrmPublicFamilyDirectory.MaskSubmittedContact(input.Contact),
				OtpExpiresAt = now.AddMinutes(OtpExpiryMinutes), DeliveryConfigured = true,
				DeliveryStatus = "If active applications match this contact, an OTP has been sent."
			};
			var entry = await familyDirectory.FindLoginEntryAsync(input.Contact, now);
			if (entry == null) return response;
			response.ChallengeId = entry.Access.Id;
			try
			{
				entry.Access.EnsureCanIssueOtp(now, TimeSpan.FromSeconds(OtpResendCooldownSeconds));
				var statusToken = tokenService.DecryptRequired(entry.Access.EncryptedToken!);
				var otp = AdmissionCrmPublicTokenService.CreateOtpCode();
				entry.Access.IssueFamilyOtp(
					AdmissionCrmPublicTokenService.HashSecret($"{statusToken.Trim()}:{otp}"),
					tokenService.EncryptRequired(otp), now, now.AddMinutes(OtpExpiryMinutes), entry.GuardianPersonId);
				try { await deliveryWorkflow.DispatchAsync(entry.Lead, otp, now, entry.DeliveryContact); }
				catch (Exception exception) when (exception is not OperationCanceledException)
				{
					logger.LogWarning(exception, "Family access OTP delivery failed for challenge {ChallengeId}.", entry.Access.Id);
				}
				await accessWorkflow.SaveAsync(entry.Access);
			}
			catch (BusinessException exception) when (exception.Code == "AdmissionCrm:StatusOtpResendTooSoon") { }
			return response;
		}
	}

	[UnitOfWork(IsDisabled = true)]
	public async Task<AdmissionApplicantStatusDto> VerifyStatusAsync(
		Guid tenantId, string token, VerifyAdmissionApplicantStatusOtpDto input)
	{
		using (CurrentTenant.Change(tenantId))
		using (var uow = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true }, requiresNew: true))
		{
			try
			{
				var (access, lead) = await accessWorkflow.GetAsync(token);
				var status = await VerifyAsync(
					access, lead, token, input.OtpCode, input.Language, ensureFamilyAuthorization: false);
				await uow.CompleteAsync();
				return status;
			}
			catch (BusinessException exception) when (exception.Code == "AdmissionCrm:InvalidStatusOtp")
			{
				await uow.CompleteAsync();
				throw;
			}
		}
	}

	[UnitOfWork(IsDisabled = true)]
	public async Task<AdmissionApplicantStatusDto> VerifyFamilyAsync(
		Guid tenantId, VerifyAdmissionFamilyAccessDto input)
	{
		using (CurrentTenant.Change(tenantId))
		using (var uow = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true }, requiresNew: true))
		{
			try
			{
				var (access, lead) = await accessWorkflow.GetChallengeAsync(input.ChallengeId);
				var statusToken = tokenService.DecryptRequired(access.EncryptedToken!);
				var status = await VerifyAsync(
					access, lead, statusToken, input.OtpCode, input.Language, ensureFamilyAuthorization: true);
				await uow.CompleteAsync();
				return status;
			}
			catch (BusinessException exception) when (exception.Code == "AdmissionCrm:InvalidStatusOtp")
			{
				await uow.CompleteAsync();
				throw;
			}
		}
	}

	private async Task<AdmissionApplicantStatusDto> VerifyAsync(
		AdmissionApplicantStatusAccess access,
		AdmissionInquiryLead lead,
		string statusToken,
		string otpCode,
		string? language,
		bool ensureFamilyAuthorization)
	{
		var now = Clock.Now;
		access.EnsureCanVerify(now);
		var providedHash = AdmissionCrmPublicTokenService.HashSecret($"{statusToken.Trim()}:{otpCode.Trim()}");
		if (!string.Equals(access.OtpCodeHash, providedHash, StringComparison.Ordinal))
		{
			access.RecordOtpFailure(now);
			await accessWorkflow.SaveAsync(access);
			throw new BusinessException("AdmissionCrm:InvalidStatusOtp");
		}

		access.MarkOtpVerified(now);
		var sessionToken = tokenService.CreateRawToken();
		var sessionExpiresAt = now.AddMinutes(AccessSessionMinutes);
		var sessionHash = AdmissionCrmPublicTokenService.HashSecret(sessionToken);
		access.IssueAccessSession(sessionHash, sessionExpiresAt, now);
		await accessWorkflow.SaveAsync(access);
		if (ensureFamilyAuthorization) await accessWorkflow.EnsureFamilyAuthorizationAsync(access, lead, now);
		var applications = await familyDirectory.ShareSessionAsync(lead, access, sessionHash, sessionExpiresAt, now);
		var status = await statusComposer.ComposeAsync(lead, language);
		status.AccessSessionToken = sessionToken;
		status.AccessSessionExpiresAt = sessionExpiresAt;
		status.FamilyApplications = applications;
		return status;
	}

	private static AdmissionApplicantStatusOtpChallengeDto Challenge(
		AdmissionApplicantStatusAccess access,
		string maskedDestination,
		AdmissionCrmPublicOtpDeliveryResult delivery) => new()
	{
		MaskedDestination = maskedDestination,
		OtpExpiresAt = access.OtpExpiresAt!.Value,
		DeliveryConfigured = delivery.DeliveryConfigured,
		DeliveryStatus = delivery.DeliveryStatus
	};
}
