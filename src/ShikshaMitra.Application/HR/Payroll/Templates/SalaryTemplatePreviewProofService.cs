using System;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Users;

namespace ShikshaMitra.HR.Payroll.Templates;

public sealed class SalaryTemplatePreviewProofService(
	IDataProtectionProvider provider, ICurrentTenant tenant, ICurrentUser user, IClock clock) : ITransientDependency
{
	private readonly IDataProtector _protector = provider.CreateProtector("Payroll.SalaryTemplatePreview.v1");
	private sealed record Proof(Guid? TenantId, Guid? UserId, string Inputs, string Results, DateTime ExpiresAt);

	internal SalaryTemplatePreviewDto Issue(object inputs, SalaryTemplateCalculationDto calculation)
	{
		var expires = clock.Now.AddMinutes(30);
		return new SalaryTemplatePreviewDto
		{
			Calculation = calculation, ExpiresAt = expires,
			PreviewToken = _protector.Protect(JsonSerializer.Serialize(
				new Proof(tenant.Id, user.Id, Hash(inputs), ResultHash(calculation), expires)))
		};
	}

	internal void Validate(string token, object inputs, SalaryTemplateCalculationDto? calculation = null, bool replay = false)
	{
		Proof? proof;
		try
		{
			proof = string.IsNullOrWhiteSpace(token) ? null : JsonSerializer.Deserialize<Proof>(_protector.Unprotect(token));
		}
		catch (Exception exception) when (exception is CryptographicException or FormatException or JsonException)
		{
			throw Invalid();
		}
		if (proof == null || proof.TenantId != tenant.Id || proof.UserId != user.Id || proof.Inputs != Hash(inputs))
			throw Invalid();
		if (!replay && proof.ExpiresAt <= clock.Now)
			throw new BusinessException("HR:Payroll:SalaryTemplatePreviewExpired")
				.WithData("message", "The sample preview expired. Calculate again before saving.");
		if (calculation != null && proof.Results != ResultHash(calculation))
			throw new BusinessException("HR:Payroll:SalaryTemplatePreviewChanged")
				.WithData("message", "Payroll configuration changed the sample result. Calculate again before saving.");
	}

	private static BusinessException Invalid() => new BusinessException("HR:Payroll:SalaryTemplatePreviewRequired")
		.WithData("message", "Calculate a fresh sample for these rules before saving.");
	private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
	private static string ResultHash(SalaryTemplateCalculationDto calculation)
	{
		// Generation time is presentation metadata, not part of the calculation result.
		var copy = JsonSerializer.SerializeToNode(calculation)!;
		copy.AsObject().Remove(nameof(SalaryTemplateCalculationDto.GeneratedAt));
		return Hash(copy);
	}
}
