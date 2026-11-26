using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments;
using Volo.Abp;

namespace ShikshaMitra.Students.Records.HouseManagement;

public partial class HouseManagementCommandService
{
	public Task ReplaceHouseLeaderAsync(Guid id, ReplaceStudentLeaderRequest input) => TransactionAsync(async () =>
	{
		await access.GetAsync(input.AcademicMediumId, manage: true);
		var house = await HouseAsync(id, input.AcademicMediumId);
		house.EnsureVersion(input.ConcurrencyStamp);
		var enrollment = await LeaderAsync(input);
		house.ReplaceLeader(enrollment);
		await houses.UpdateAsync(house);
		return true;
	});

	public Task ReplaceDivisionLeaderAsync(Guid id, ReplaceStudentLeaderRequest input) => TransactionAsync(async () =>
	{
		await access.GetAsync(input.AcademicMediumId);
		var division = await ScopedDivisionAsync(id, input.AcademicMediumId, "HouseManagement.DivisionLeader");
		var enrollment = await LeaderAsync(input);
		division.ReplaceLeader(enrollment, input.ConcurrencyStamp);
		await divisions.UpdateAsync(division);
		return true;
	});

	private async Task<StudentEnrollment?> LeaderAsync(ReplaceStudentLeaderRequest input)
	{
		if (!input.EnrollmentId.HasValue)
			return null;
		var q = await queries.StudentsAsync(input.AcademicMediumId);
		if (!await AsyncExecuter.AnyAsync(q.Where(s => s.EnrollmentId == input.EnrollmentId)))
			throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		return await enrollments.GetAsync(e => e.Id == input.EnrollmentId && e.TenantId == CurrentTenant.Id);
	}
}
