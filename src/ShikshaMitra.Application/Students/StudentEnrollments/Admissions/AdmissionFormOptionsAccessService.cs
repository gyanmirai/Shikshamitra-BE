using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionFormOptionsAccessService(IPermissionChecker permissions) : ITransientDependency
{
	public async Task EnsureReadAsync(string sourceKey)
	{
		if (await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.View)) return;
		if (AdmissionAcademicFormOptions.IsAcademic(sourceKey) &&
			(await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.View) ||
			 await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View))) return;
		throw new AbpAuthorizationException();
	}
}
