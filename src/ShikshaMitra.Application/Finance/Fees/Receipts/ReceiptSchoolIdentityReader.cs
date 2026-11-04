using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.InitialSetup;
using ShikshaMitra.TenantSettings.SchoolIdentity;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace ShikshaMitra.Application.Finance.Fees.Receipts;

public interface IReceiptSchoolIdentityReader
{
	Task<ReceiptSchoolIdentity> ReadAsync(Guid? boardId, Guid? mediumId, Guid? yearId,
		Guid? organizationId, CancellationToken cancellationToken = default);
}

/// <summary>Document callers retain their own Finance authorization and payer scope.</summary>
public class ReceiptSchoolIdentityReader(ITenantRepository tenants, ICurrentTenant currentTenant,
	SchoolIdentityDocumentService identity) : IReceiptSchoolIdentityReader, ITransientDependency
{
	public async Task<ReceiptSchoolIdentity> ReadAsync(Guid? boardId, Guid? mediumId, Guid? yearId,
		Guid? organizationId, CancellationToken cancellationToken = default)
	{
		var tenant = await tenants.GetAsync(currentTenant.GetId(), cancellationToken: cancellationToken);
		string Read(string key) => InitialSetupStatusReader.ReadTenantText(tenant, key).Trim();
		var address = string.Join(", ", new[] { Read("Address1"), Read("Address2"), Read("Address3"),
			Read("City"), Read("State"), Read("PostalCode"), Read("Country") }.Where(value => value.Length > 0));
		var snapshot = await identity.CaptureAsync(boardId, mediumId, yearId, false, organizationId);
		var images = await identity.LoadAsync(snapshot);
		var schoolName = Read("InstituteName");
		return new(schoolName.Length > 0 ? schoolName : tenant.Name, address, images.Logo);
	}
}
