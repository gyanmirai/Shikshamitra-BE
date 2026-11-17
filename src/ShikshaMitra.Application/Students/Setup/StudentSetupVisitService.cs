using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.Setup;

[RemoteService(false)]
public class StudentSetupVisitService(StudentSetupAccess access, ITenantRepository tenants, IUnitOfWorkManager units) : ApplicationService
{
	public async Task<StudentSetupVisitStatusDto> GetAsync()
	{
		var tenantId = await access.RequireIdentityAsync();
		var canAccess = (await access.ReadPermissionsAsync()).Any(x => x.CanView);
		if (!canAccess) return new();
		var entry = StudentSetupVisit.Read(await tenants.GetAsync(tenantId));
		return new() { CanAccessSetup = true, HasVisitedConfiguration = entry != null, FirstVisitedAt = entry?.VisitedAt };
	}

	public async Task<StudentSetupVisitDto> RecordAsync(StudentSetupVisitInput input)
	{
		for (var attempt = 0; ; attempt++)
		{
			using var unit = units.Begin(requiresNew: true, isTransactional: true);
			try
			{
				var tenantId = await access.RequireIdentityAsync();
				await access.RequireVisitPermissionAsync(input.SettingKey);
				var tenant = await tenants.GetAsync(tenantId);
				var entry = StudentSetupVisit.Read(tenant);
				var first = entry == null;
				if (entry == null)
				{
					entry = StudentSetupVisit.Record(tenant, CurrentUser.Id!.Value, Clock.Now);
					await tenants.UpdateAsync(tenant, autoSave: true);
				}
				await unit.CompleteAsync();
				return new() { FirstVisit = first, HasVisitedConfiguration = true, FirstVisitedAt = entry.VisitedAt };
			}
			catch (AbpDbConcurrencyException) when (attempt < 2)
			{
				// The retry uses a fresh unit of work so the losing writer observes the immutable winner.
			}
		}
	}
}
