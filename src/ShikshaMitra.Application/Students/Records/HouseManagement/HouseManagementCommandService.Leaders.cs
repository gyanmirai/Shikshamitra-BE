using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments;
using Volo.Abp;

namespace ShikshaMitra.Students.Records.HouseManagement;

public partial class HouseManagementCommandService
{
	public Task ReplaceDivisionHouseLeaderAsync(Guid id, ReplaceStudentLeaderRequest input) =>
		ReplaceLeaderAsync(id, input, houseLeader: true);

	public Task ReplaceDivisionLeaderAsync(Guid id, ReplaceStudentLeaderRequest input) =>
		ReplaceLeaderAsync(id, input, houseLeader: false);

	private Task ReplaceLeaderAsync(Guid id, ReplaceStudentLeaderRequest input, bool houseLeader) => TransactionAsync(async () =>
	{
		await access.GetAsync(input.AcademicMediumId);
		await scope.EnsureCanAccessDivisionAsync(id, houseLeader ? "HouseManagement.HouseLeader" : "HouseManagement.DivisionLeader");
		var dq =
			from d in await divisions.GetQueryableAsync()
			join c in await standards.GetQueryableAsync() on d.StandardId equals c.Id
			join stage in await stages.GetQueryableAsync() on c.EducationMediumStageId equals stage.Id
			where d.Id == id && d.TenantId == CurrentTenant.Id && c.TenantId == CurrentTenant.Id &&
				stage.TenantId == CurrentTenant.Id && stage.EducationMediumId == input.AcademicMediumId
			select d;
		var division = await AsyncExecuter.SingleOrDefaultAsync(dq) ?? throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		var enrollment = await LeaderAsync(input, houseLeader);
		if (houseLeader)
			division.ReplaceHouseLeader(enrollment, input.ConcurrencyStamp);
		else
			division.ReplaceLeader(enrollment, input.ConcurrencyStamp);
		await divisions.UpdateAsync(division);
		return true;
	});

	private async Task<StudentEnrollment?> LeaderAsync(ReplaceStudentLeaderRequest input, bool houseLeader)
	{
		if (!input.EnrollmentId.HasValue)
			return null;
		var q = await queries.StudentsAsync(input.AcademicMediumId);
		var candidate = await AsyncExecuter.SingleOrDefaultAsync(q.Where(s => s.EnrollmentId == input.EnrollmentId));
		if (candidate == null)
			throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		if (houseLeader && !candidate.HouseId.HasValue)
			throw new BusinessException(HouseManagementErrorCodes.IneligibleLeader);
		return await enrollments.GetAsync(e => e.Id == input.EnrollmentId && e.TenantId == CurrentTenant.Id);
	}
}
