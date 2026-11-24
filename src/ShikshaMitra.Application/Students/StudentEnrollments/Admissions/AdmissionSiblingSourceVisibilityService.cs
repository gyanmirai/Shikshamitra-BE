using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>The batched equivalent of sibling confirmation's exact linked-Student scope check.</summary>
[RemoteService(false)]
public class AdmissionSiblingSourceVisibilityService(IStudentRepository students, StudentScopeAccessService studentScope)
	: ApplicationService, ITransientDependency
{
	internal async Task<IQueryable<AdmissionApplicant>> ApplyAsync(IQueryable<AdmissionApplicant> applicants)
	{
		var linkedStudents = (await students.GetQueryableWithoutAutoIncludesAsync()).Where(student => student.TenantId == CurrentTenant.Id);
		var allowedStudents = await studentScope.ApplyCurrentUserScopeAsync(linkedStudents);
		return applicants.Where(applicant =>
			!applicant.ExistingStudentId.HasValue && !linkedStudents.Any(student => student.Id == applicant.ReservedStudentId) ||
			allowedStudents.Any(student => student.Id == (applicant.ExistingStudentId ?? applicant.ReservedStudentId)));
	}
}
