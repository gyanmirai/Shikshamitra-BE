using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Finance.Fees.Collections;

public class LateFeeAdmissionDateReader(
	IRepository<AdmissionApplication, Guid> applications,
	IRepository<AdmissionOffering, Guid> offerings,
	IRepository<AdmissionCycle, Guid> cycles,
	AdmissionJoiningDateReader joiningDates,
	IAsyncQueryableExecuter executer) : ILateFeeAdmissionDateReader, ITransientDependency
{
	public async Task<DateTime?> ReadAsync(Guid? studentId, Guid? applicationId, Guid academicYearId)
	{
		var applicationQuery = await applications.GetQueryableAsync();
		var offeringQuery = await offerings.GetQueryableAsync();
		var cycleQuery = await cycles.GetQueryableAsync();
		var matches = await executer.ToListAsync(from application in applicationQuery
			join offering in offeringQuery on application.AdmissionOfferingId equals offering.Id
			join cycle in cycleQuery on offering.AdmissionCycleId equals cycle.Id
			where cycle.AcademicYearId == academicYearId &&
				(applicationId.HasValue ? application.Id == applicationId.Value :
					studentId.HasValue && (application.StudentId == studentId || application.EnrolledStudentId == studentId))
			select application);
		if (matches.Count > 1) throw new BusinessException(AssignmentErrorCodes.SourceUnavailable);
		return matches.Count == 0 ? null : (await joiningDates.ReadAsync(matches[0]))?.Date;
	}
}
