using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Volo.Abp.Identity;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.Records;

public partial class StudentRecordTransitionAppService
{
	public Task<StudentRecordRenewalPortalOptionsDto> GetRenewalPortalOptionsAsync(Guid id) => renewalPortals.GetOptionsAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Create)]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	[Authorize(IdentityPermissions.Users.Create)]
	public async Task<StudentPortalAccountDto> ProvisionRenewalPortalAsync(Guid id, ProvisionStudentPortalAccountDto input)
	{
		using var work = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = System.Data.IsolationLevel.Serializable }, requiresNew: true);
		var result = await renewalPortals.ProvisionAsync(id, input);
		await work.CompleteAsync(); return result;
	}
}
