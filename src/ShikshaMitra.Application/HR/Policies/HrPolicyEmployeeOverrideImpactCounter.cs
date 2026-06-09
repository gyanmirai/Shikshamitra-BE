using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Policies;

public class HrPolicyEmployeeOverrideImpactCounter(
	IRepository<HrPolicyAssignment, Guid> assignmentRepository,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	public async Task<int> CountActiveAsync(
		CreateUpdateHrPolicyAssignmentDto assignment,
		HrPolicyAssignmentTargetType targetType,
		string targetKey)
	{
		if (targetType != HrPolicyAssignmentTargetType.Employee)
		{
			return 1;
		}

		var effectiveDate = assignment.EffectiveFrom.Date;
		var query = await assignmentRepository.GetQueryableAsync();
		return await asyncExecuter.CountAsync(query.Where(x =>
			x.AcademicYearId == assignment.AcademicYearId &&
			x.TargetType == targetType &&
			x.TargetKey == targetKey &&
			x.IsActive &&
			x.EffectiveFrom <= effectiveDate &&
			(!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= effectiveDate)));
	}
}
