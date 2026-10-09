using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Data;
using Volo.Abp.Identity;

namespace ShikshaMitra.TenantSettings.InitialSetup;

public partial class InitialSetupAppService
{
	[DisableAuditing]
	public async Task<InitialSetupDto> CreateUserAsync(CreateSetupUserInput input)
	{
		reader.RequireTenant();
		await AuthorizationService.CheckAsync(IdentityPermissions.Users.Create);
		var progress = await reader.ReadAsync();
		var completedRequests = JsonSerializer.Deserialize<List<SetupUserSummary>>(progress.GetProperty<string>("CreatedUsers") ?? "[]")!;
		var existing = completedRequests.FirstOrDefault(x => x.RequestId == input.RequestId);
		if (existing != null)
		{
			if (existing.UserName != input.UserName || existing.Email != input.Email)
				throw new BusinessException(InitialSetupErrorCodes.UserRequestReused);
			if (!await reader.CanManageAsync()) throw new Volo.Abp.Authorization.AbpAuthorizationException();
			return await reader.MapAsync(progress);
		}
		if (input.RequestId == Guid.Empty || completedRequests.Count >= 32)
			throw new BusinessException(InitialSetupErrorCodes.UserLimitReached);
		progress = await EditableAsync(new SetupConcurrencyInput { ConcurrencyStamp = input.ConcurrencyStamp });
		progress.SaveDraft(4, progress.DraftJson);
		// The standard identity service owns role validation, credentials and exact Create enforcement.
		var user = await identityUsers.CreateAsync(input);
		completedRequests.Add(new SetupUserSummary { RequestId = input.RequestId, Id = user.Id, UserName = user.UserName, Email = user.Email });
		progress.SetProperty("CreatedUsers", JsonSerializer.Serialize(completedRequests));
		return await SaveAsync(progress);
	}
}
