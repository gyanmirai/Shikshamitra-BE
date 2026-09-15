using Microsoft.AspNetCore.DataProtection;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Academic.Reports;

public class AcademicAiReportApprovalService(IDataProtectionProvider dataProtectionProvider) : ITransientDependency
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(30);
	private readonly IDataProtector protector = dataProtectionProvider.CreateProtector(
		"ShikshaMitra.Academic.Reports.AiApproval.v1");

	public string CreateToken(
		Guid userId,
		Guid? tenantId,
		AcademicAiReportCatalogScope scope,
		AcademicDynamicReportSpecDto plan)
	{
		var payload = new ApprovalPayload
		{
			UserId = userId,
			TenantId = tenantId,
			AcademicYearId = scope.AcademicYearId,
			EducationBoardId = scope.EducationBoardId,
			EducationMediumId = scope.EducationMediumId,
			PlanHash = ComputePlanHash(plan),
			IssuedAtUtc = DateTime.UtcNow
		};
		return protector.Protect(JsonSerializer.Serialize(payload, JsonOptions));
	}

	public void ValidateToken(
		string? token,
		Guid userId,
		Guid? tenantId,
		AcademicAiReportCatalogScope scope,
		AcademicDynamicReportSpecDto plan)
	{
		if (string.IsNullOrWhiteSpace(token))
		{
			throw InvalidApproval();
		}

		try
		{
			var payload = JsonSerializer.Deserialize<ApprovalPayload>(protector.Unprotect(token), JsonOptions)
				?? throw InvalidApproval();
			var matchesContext = payload.UserId == userId &&
				payload.TenantId == tenantId &&
				payload.AcademicYearId == scope.AcademicYearId &&
				payload.EducationBoardId == scope.EducationBoardId &&
				payload.EducationMediumId == scope.EducationMediumId;
			if (!matchesContext || DateTime.UtcNow - payload.IssuedAtUtc > TokenLifetime ||
				!CryptographicOperations.FixedTimeEquals(
					Convert.FromHexString(payload.PlanHash),
					Convert.FromHexString(ComputePlanHash(plan))))
			{
				throw InvalidApproval();
			}
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception exception) when (
			exception is CryptographicException or FormatException or JsonException)
		{
			throw InvalidApproval();
		}
	}

	private static string ComputePlanHash(AcademicDynamicReportSpecDto plan)
	{
		var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(plan, JsonOptions)));
		return Convert.ToHexString(bytes);
	}

	private static BusinessException InvalidApproval() =>
		new("Academic:Reports:AiApprovalInvalid");

	private sealed class ApprovalPayload
	{
		public Guid UserId { get; set; }
		public Guid? TenantId { get; set; }
		public Guid AcademicYearId { get; set; }
		public Guid EducationBoardId { get; set; }
		public Guid EducationMediumId { get; set; }
		public string PlanHash { get; set; } = string.Empty;
		public DateTime IssuedAtUtc { get; set; }
	}
}
