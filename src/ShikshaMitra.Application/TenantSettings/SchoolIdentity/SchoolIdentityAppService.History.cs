using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace ShikshaMitra.TenantSettings.SchoolIdentity;

public partial class SchoolIdentityAppService
{
	public async Task<List<SchoolIdentityRevisionDto>> GetHistoryAsync(SchoolIdentityContextDto input)
	{
		await AuthorizationService.CheckAsync(SchoolIdentityPermissions.View);
		await access.EnsureAsync(input.BoardId, input.MediumId, organizationId: input.OrganizationId);
		var profile = await reader.FindAsync(input.BoardId, input.MediumId, input.OrganizationId);
		if (profile == null) return [];
		var signature = await permissionChecker.IsGrantedAsync(SchoolIdentityPermissions.AccessSignature);
		var query = await revisions.GetQueryableAsync();
		var rows = await AsyncExecuter.ToListAsync(query.Where(x => x.TenantId == CurrentTenant.Id && x.ProfileId == profile.Id)
			.OrderByDescending(x => x.CreationTime).ThenByDescending(x => x.Id).Take(100));
		var creatorIds = rows.Where(x => x.CreatorId.HasValue).Select(x => x.CreatorId!.Value).Distinct().ToArray();
		var users = await identityUsers.GetQueryableAsync();
		var creators = await AsyncExecuter.ToListAsync(users.Where(user => user.TenantId == CurrentTenant.Id
			&& Enumerable.Contains(creatorIds, user.Id)).Select(user => new { user.Id, user.UserName }));
		var names = creators.ToDictionary(user => user.Id, user => user.UserName);
		return rows.Select(x => new SchoolIdentityRevisionDto
		{
			Id = x.Id, CreationTime = x.CreationTime, CreatorId = x.CreatorId, Reason = x.Reason,
			CreatorName = x.CreatorId.HasValue ? names.GetValueOrDefault(x.CreatorId.Value, "Deleted user") : "System import",
			Choices = SchoolIdentityMapping.Dto(JsonSerializer.Deserialize<SchoolIdentityChoices>(x.ChoicesJson)!, signature)
		}).ToList();
	}
}
