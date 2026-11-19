using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.TenantSettings.Reports;

public class InstituteReportApproval(IDataProtectionProvider protection) : ITransientDependency
{
	private readonly IDataProtector protector = protection.CreateProtector("InstituteSetup.Reports.Approval.v1");
	public string Create(Guid user, Guid tenant, InstituteReportRequest plan) => protector.Protect(JsonSerializer.Serialize(new Approval(user, tenant, Hash(plan), DateTime.UtcNow)));
	public void Validate(string token, Guid user, Guid tenant, InstituteReportRequest plan)
	{
		try
		{
			var value = JsonSerializer.Deserialize<Approval>(protector.Unprotect(token));
			if (value == null || value.User != user || value.Tenant != tenant || value.Created > DateTime.UtcNow || value.Created < DateTime.UtcNow.AddMinutes(-30) ||
				!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(value.Hash), Convert.FromHexString(Hash(plan)))) throw Invalid();
		}
		catch (Exception exception) when (exception is CryptographicException or FormatException or JsonException or ArgumentException) { throw Invalid(); }
	}
	private static string Hash(InstituteReportRequest plan) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(plan))));
	private static BusinessException Invalid() => new("InstituteReports:ApprovalInvalid", "The report plan changed or approval expired. Review the plan again.");
	private sealed record Approval(Guid User, Guid Tenant, string Hash, DateTime Created);
}
