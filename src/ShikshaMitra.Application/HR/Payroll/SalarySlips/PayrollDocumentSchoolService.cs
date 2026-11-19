using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.InitialSetup;
using ShikshaMitra.TenantSettings.SchoolIdentity;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.TenantManagement;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

public sealed record PayrollDocumentSchool(string Name, string Address);

// Document callers enforce payroll permission and employee scope before resolving school details.
[RemoteService(false)]
public class PayrollDocumentSchoolService(ITenantRepository tenants, SchoolIdentityDocumentService identity) : ApplicationService
{
	public async Task<PayrollDocumentSchool> ReadAsync()
	{
		var tenantId = CurrentTenant.Id ?? throw new BusinessException(HRErrorCodes.PayslipSchoolDetailsRequired);
		return FromTenant(await tenants.GetAsync(tenantId));
	}

	public static PayrollDocumentSchool FromTenant(Tenant tenant)
	{
		var name = InitialSetupStatusReader.ReadTenantText(tenant, "InstituteName", tenant.Name).Trim();
		var address = string.Join(", ", new[] { "Address1", "Address2", "Address3", "City", "State", "PostalCode", "Country" }
			.Select(key => InitialSetupStatusReader.ReadTenantText(tenant, key).Trim()).Where(value => !string.IsNullOrWhiteSpace(value)));
		return new(name, string.IsNullOrWhiteSpace(InitialSetupStatusReader.ReadTenantText(tenant, "Address1")) ? string.Empty : address);
	}

	public async Task<byte[]?> LogoAsync()
	{
		var snapshot = await identity.CaptureAsync(null, null, null, signature: false);
		return (await identity.LoadAsync(snapshot)).Logo;
	}
}
