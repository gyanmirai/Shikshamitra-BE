using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.TenantSettings.SchoolIdentity;

[RemoteService(false)]
public class SchoolIdentityReadService(IRepository<SchoolIdentityProfile, Guid> profiles,
	IRepository<SchoolIdentityAsset, Guid> assets) : ApplicationService, ITransientDependency
{
	public async Task<SchoolIdentityProfile?> FindAsync(Guid? boardId, Guid? mediumId, Guid? organizationId = null)
	{
		var key = SchoolIdentityProfile.Key(boardId, mediumId, organizationId);
		return await profiles.FindAsync(x => x.TenantId == CurrentTenant.Id && x.ScopeKey == key);
	}

	public async Task<SchoolIdentityProfileDto> ReadAsync(Guid? boardId, Guid? mediumId, Guid? yearId, bool signature, SchoolIdentityChoices? draft = null, Guid? organizationId = null)
	{
		if (!CurrentTenant.Id.HasValue) throw new BusinessException(SchoolIdentityErrorCodes.InvalidContext);
		var keys = new[] { "school", SchoolIdentityProfile.Key(boardId, null), SchoolIdentityProfile.Key(boardId, mediumId), SchoolIdentityProfile.Key(boardId, mediumId, organizationId) };
		var rows = await profiles.GetListAsync(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(keys, x.ScopeKey));
		var defaults = rows.Find(x => x.ScopeKey == "school");
		var board = boardId.HasValue ? rows.Find(x => x.ScopeKey == keys[1]) : null;
		var medium = mediumId.HasValue ? rows.Find(x => x.ScopeKey == keys[2]) : null;
		var organization = organizationId.HasValue ? rows.Find(x => x.ScopeKey == keys[3]) : null;
		var selected = organizationId.HasValue ? organization : mediumId.HasValue ? medium : boardId.HasValue ? board : defaults;
		var defaultChoices = defaults?.Choices;
		var boardChoices = board?.Choices;
		var mediumChoices = medium?.Choices;
		var organizationChoices = organization?.Choices;
		if (draft != null)
		{
			if (organizationId.HasValue) organizationChoices = draft;
			else if (mediumId.HasValue) mediumChoices = draft;
			else if (boardId.HasValue) boardChoices = draft;
			else defaultChoices = draft;
		}
		var result = new SchoolIdentityProfileDto
		{
			Id = selected?.Id, AcademicYearId = yearId, BoardId = boardId, MediumId = mediumId, OrganizationId = organizationId,
			ConcurrencyStamp = selected?.ConcurrencyStamp ?? "",
			Choices = SchoolIdentityMapping.Dto(draft ?? selected?.Choices ?? new(), signature), CanAccessSignature = signature
		};
		foreach (var kind in Enum.GetValues<SchoolIdentityAssetKind>())
		{
			if (kind == SchoolIdentityAssetKind.Signature && !signature) continue;
			var resolved = SchoolIdentityResolver.Resolve(kind, defaultChoices, boardChoices, mediumChoices, organizationChoices);
			result.Effective[kind.ToString().ToLowerInvariant()] = new() { Selection = SchoolIdentityMapping.Dto(resolved.Selection), Source = resolved.Source };
		}
		var ids = result.Effective.Values.Select(x => x.Selection.AssetId).Where(x => x.HasValue).Select(x => x!.Value).ToArray();
		var files = ids.Length > 0 ? await assets.GetListAsync(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(ids, x.Id)) : [];
		foreach (var item in result.Effective.Values)
		{
			var file = files.Find(x => x.Id == item.Selection.AssetId);
			if (item.Selection.Mode == SchoolIdentityAssetMode.Custom && file == null)
				throw new BusinessException(SchoolIdentityErrorCodes.AssetMissing);
			item.Asset = file == null ? null : SchoolIdentityMapping.Dto(file);
		}
		return result;
	}
}
