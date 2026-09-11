using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Divisions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionInquiryPeopleService(
	IRepository<IdentityUser, Guid> users,
	IUserScopeService scopes,
	IRepository<AdmissionOffering, Guid> offerings,
	IRepository<AdmissionCycle, Guid> cycles,
	IRepository<Division, Guid> divisions) : ApplicationService, ITransientDependency
{
	public async Task EnsureCanAccessOfferingAsync(Guid userId, Guid offeringId)
	{
		if (!await CanAccessOfferingAsync(userId, offeringId))
			throw new BusinessException(AdmissionErrorCodes.InquiryCounsellorInvalid,
				"The counsellor must be active and have access to this admission offering.");
	}

	public async Task<bool> CanAccessHierarchyAsync(Guid userId, AdmissionCycle cycle, AdmissionOfferingWriteDto input)
	{
		if (!await IsActiveAsync(userId)) return false;
		var candidate = new AdmissionOffering(Guid.Empty, CurrentTenant.Id, cycle.Id, null, input.BoardId,
			input.MediumId, input.StageId, input.StandardId, "INQUIRY_SCOPE", true, null, null);
		var standardDivisions = await divisions.GetListAsync(item => item.StandardId == input.StandardId);
		return AdmissionOfferingScopeQuery.Apply(new[] { candidate }.AsQueryable(), new[] { cycle }.AsQueryable(),
			standardDivisions.AsQueryable(), await scopes.GetUserScopeAsync(userId)).Any();
	}

	public async Task<List<AdmissionInquiryCounsellorDto>> GetCounsellorsAsync(string? filter)
	{
		if (filter?.Length > 128) throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidPolicy);
		var query = (await users.GetQueryableAsync()).Where(user => user.IsActive);
		if (!string.IsNullOrWhiteSpace(filter))
			query = query.Where(user => user.UserName.Contains(filter) || user.Name.Contains(filter) || user.Surname.Contains(filter));
		var rows = await AsyncExecuter.ToListAsync(query.OrderBy(user => user.UserName).Take(50));
		var result = new List<AdmissionInquiryCounsellorDto>();
		foreach (var user in rows)
			if ((await scopes.GetUserScopeAsync(user.Id)).HasAnyAccess)
				result.Add(new() { Id = user.Id, UserName = user.UserName, Name = Display(user) });
		return result;
	}

	private async Task<bool> CanAccessOfferingAsync(Guid userId, Guid offeringId)
	{
		if (!await IsActiveAsync(userId)) return false;
		var query = AdmissionOfferingScopeQuery.Apply(await offerings.GetQueryableAsync(), await cycles.GetQueryableAsync(),
			await divisions.GetQueryableAsync(), await scopes.GetUserScopeAsync(userId));
		return await AsyncExecuter.AnyAsync(query.Where(item => item.Id == offeringId));
	}

	private async Task<bool> IsActiveAsync(Guid userId) => (await users.FindAsync(userId))?.IsActive == true;
	private static string Display(IdentityUser user) => string.IsNullOrWhiteSpace(user.Name) && string.IsNullOrWhiteSpace(user.Surname)
		? user.UserName : $"{user.Name} {user.Surname}".Trim();
}
