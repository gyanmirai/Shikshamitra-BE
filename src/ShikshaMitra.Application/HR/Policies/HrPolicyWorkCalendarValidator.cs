using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Policies;

public class HrPolicyWorkCalendarValidator(
	IRepository<WorkCalendarPolicy, Guid> workCalendarRepository)
	: ITransientDependency
{
	public async Task EnsureUsableAsync(Guid id, Guid academicYearId, HrPolicyStatus policyStatus)
	{
		var workCalendar = await workCalendarRepository.GetAsync(id);
		if (workCalendar.AcademicYearId != academicYearId || workCalendar.Status == HrPolicyStatus.Archived)
		{
			throw new BusinessException(HRErrorCodes.HrPolicyInvalidWorkCalendar);
		}

		if (policyStatus == HrPolicyStatus.Active && workCalendar.Status != HrPolicyStatus.Active)
		{
			throw new BusinessException(HRErrorCodes.WorkCalendarPolicyMustBeActive);
		}
	}
}
