using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records;

public class StudentRecordReportAuthority(IUserScopeAccessService scopes, StudentRecordsAccessService access,
	IPermissionChecker permissions, IRepository<StudentRecordsConfiguration, Guid> configurations) : ApplicationService, ITransientDependency
{
	public async Task EnsureAsync(string type)
	{
		await access.EnsureActiveActorAsync();
		if (!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Records_Reports.View)) throw new AbpAuthorizationException();
		var required = type switch
		{
			"Directory" => new[] { ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View },
			"Changes" => [ShikshaMitraSubmodulePermissions.Students.Records_Changes.View],
			"Documents" => [ShikshaMitraSubmodulePermissions.Students.Records_Certificates.View, ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.View],
			"Transitions" => [ShikshaMitraSubmodulePermissions.Students.Records_Transitions.View],
			_ => throw new Volo.Abp.BusinessException("Students:Records:InvalidReport")
		};
		foreach (var grant in required) if (!await permissions.IsGrantedAsync(grant)) throw new AbpAuthorizationException();
		var scope = await scopes.GetCurrentUserScopeAsync();
		if (!CurrentTenant.Id.HasValue || scope.TenantId != CurrentTenant.Id || !scope.HasAnyAccess) throw new AbpAuthorizationException();
	}
	public async Task<string> CaptureAsync(string type)
	{
		await EnsureAsync(type);
		var node = JsonSerializer.SerializeToNode(await scopes.GetCurrentUserScopeAsync())!.AsObject();
		node.Remove("CalculatedAt"); node.Remove("FromCache");
		foreach (var property in node.ToArray())
			if (property.Value is JsonArray array) node[property.Key] = new JsonArray(array.OrderBy(x => x!.ToJsonString(), StringComparer.Ordinal).Select(x => x?.DeepClone()).ToArray());
		var policyVersions = (await configurations.GetListAsync(x => x.TenantId == CurrentTenant.Id))
			.Where(x => x.PublishedVersionId.HasValue).OrderBy(x => x.Id).Select(x => x.PublishedVersionId).ToList();
		return StudentRecordPayloadService.Hash(JsonSerializer.Serialize(new { scope = node, roles = (await access.GetDatabaseRoleNamesAsync()).OrderBy(x => x, StringComparer.Ordinal), policyVersions }));
	}
}
