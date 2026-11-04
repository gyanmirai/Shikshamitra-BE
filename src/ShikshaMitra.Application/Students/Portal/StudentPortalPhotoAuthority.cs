using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.Portal;

/// <summary>Checks persisted portal authority without changing the acting principal or granting staff scope.</summary>
[RemoteService(false)]
public class StudentPortalPhotoAuthority(StudentPortalAccessService portal, IStudentRepository students) : ApplicationService, ITransientDependency
{
	internal async Task<Student> EnsureAsync(Guid studentId, bool write, Guid? requester = null)
	{
		var actor = requester ?? CurrentUser.Id ?? throw new AbpAuthorizationException();
		if (!(await portal.GetStudentsForUserAsync(actor)).Any(x => x.StudentId == studentId)) throw new AbpAuthorizationException();
		// Association resolution rechecks the persisted active account, tenant and revoked links.
		// Mobile photo access is independent of staff grants and Records editing policy.
		return await students.FindAsync(x => x.Id == studentId && x.TenantId == CurrentTenant.Id) ?? throw new AbpAuthorizationException();
	}
}
