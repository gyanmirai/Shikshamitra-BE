using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;

namespace ShikshaMitra.TenantSettings.SchoolIdentity;

public class SchoolIdentityLegacyMigrationContributor(
	ICurrentTenant currentTenant, ITenantRepository tenants,
	IRepository<SchoolIdentityProfile, Guid> profiles,
	IRepository<SchoolIdentityAsset, Guid> assets,
	IRepository<SchoolIdentityRevision, Guid> revisions,
	IBlobContainer blobs, IGuidGenerator guids,
	ILogger<SchoolIdentityLegacyMigrationContributor> logger) : IDataSeedContributor, ITransientDependency
{
	[UnitOfWork]
	public virtual async Task SeedAsync(DataSeedContext context)
	{
		if (!context.TenantId.HasValue) return;
		using var tenantScope = currentTenant.Change(context.TenantId);
		if (await profiles.AnyAsync(x => x.TenantId == context.TenantId && x.ScopeKey == "school")) return;
		var tenant = await tenants.GetAsync(context.TenantId.Value);
		var logo = await ImportAsync(tenant, "Logo", SchoolIdentityAssetKind.Logo);
		var banner = await ImportAsync(tenant, "Banner", SchoolIdentityAssetKind.Banner);
		var signature = await ImportAsync(tenant, "PrincipleSign", SchoolIdentityAssetKind.Signature);
		if (logo == null && banner == null && signature == null) return;
		var profile = new SchoolIdentityProfile(guids.Create(), tenant.Id);
		profile.Replace(new()
		{
			Logo = Choice(logo), Banner = Choice(banner),
			Signature = Choice(signature) with { PrincipalName = signature == null ? null : "Principal", Designation = signature == null ? null : "Principal" }
		}, profile.ConcurrencyStamp);
		await profiles.InsertAsync(profile, autoSave: true);
		await revisions.InsertAsync(new(guids.Create(), profile, "Imported existing institute assets; review principal details"));
	}

	private static SchoolIdentitySelection Choice(Guid? id) => id.HasValue
		? new() { Mode = SchoolIdentityAssetMode.Custom, AssetId = id }
		: new();

	private async Task<Guid?> ImportAsync(Tenant tenant, string property, SchoolIdentityAssetKind kind)
	{
		byte[]? bytes;
		(string ContentType, int Width, int Height) image;
		try
		{
			bytes = LegacyBytes(tenant.GetProperty(property));
			if (bytes == null || bytes.Length == 0) return null;
			image = SchoolIdentityImageValidation.ValidateLegacy(kind, bytes);
		}
		catch (Exception failure) when (failure is BusinessException or FormatException or OverflowException or InvalidCastException or InvalidOperationException)
		{
			logger.LogWarning("Existing {AssetKind} for tenant {TenantId} could not be imported. The original tenant property is preserved.", kind, tenant.Id);
			return null;
		}
		var id = guids.Create();
		var blob = $"school-identity/{tenant.Id:N}/{id:N}";
		await using var stream = new MemoryStream(bytes);
		await blobs.SaveAsync(blob, stream, overrideExisting: false);
		var asset = new SchoolIdentityAsset(id, tenant.Id, kind, blob,
			$"existing-{kind.ToString().ToLowerInvariant()}.{(image.ContentType == "image/png" ? "png" : "jpg")}",
			image.ContentType, bytes.LongLength, image.Width, image.Height, null, null);
		asset.Retain();
		await assets.InsertAsync(asset);
		return id;
	}

	private static byte[]? LegacyBytes(object? value) => value switch
	{
		null => null,
		byte[] bytes => bytes,
		string base64 => Convert.FromBase64String(base64),
		JsonElement { ValueKind: JsonValueKind.String } element => element.GetBytesFromBase64(),
		JsonElement { ValueKind: JsonValueKind.Array } element => element.EnumerateArray().Select(item => item.GetByte()).ToArray(),
		IEnumerable values => values.Cast<object>().Select(Convert.ToByte).ToArray(),
		_ => throw new BusinessException(SchoolIdentityErrorCodes.ImageInvalid)
	};
}
