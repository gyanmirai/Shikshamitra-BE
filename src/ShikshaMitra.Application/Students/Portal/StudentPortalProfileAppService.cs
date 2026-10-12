using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using ShikshaMitra.Academic.Timetable.Portal;
using Volo.Abp;
using Volo.Abp.Account;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.Portal;

[Authorize]
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IProfileAppService), typeof(ProfileAppService), typeof(StudentPortalProfileAppService))]
[RemoteService(false)]
public class StudentPortalProfileAppService(
	IdentityUserManager userManager,
	IOptions<IdentityOptions> identityOptions,
	StudentPortalAccessService portalAccess,
	IPortalTimetableAppService timetable)
	: ProfileAppService(userManager, identityOptions)
{
	public override async Task<ProfileDto> GetAsync()
	{
		var profile = await base.GetAsync();
		var contexts = new List<PortalTimetableContextDto>();
		profile.ExtraProperties["studentContexts"] = contexts;
		if (!CurrentTenant.Id.HasValue) return profile;

		try
		{
			var students = await portalAccess.GetMyStudentsAsync();
			foreach (var studentId in students.Select(student => student.StudentId).Distinct())
				contexts.Add(await timetable.GetContextAsync(studentId));
		}
		catch (AbpAuthorizationException)
		{
			// Reading identity does not grant portal access to unconfirmed or revoked links.
			contexts.Clear();
		}
		return profile;
	}
}
