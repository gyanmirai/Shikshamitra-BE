using System;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class PerformanceReviewAssignmentManager(
	IGuidGenerator guidGenerator,
	ICurrentTenant currentTenant)
	: DomainService
{
	public virtual PerformanceReviewAssignment CreateAssignment(
		ProfessionalReviewCycle cycle,
		Guid templateId,
		Guid employeeId,
		Guid? reportingManagerEmployeeId,
		Guid? hodEmployeeId,
		Guid? principalEmployeeId)
	{
		return new PerformanceReviewAssignment(
			guidGenerator.Create(),
			currentTenant.Id,
			cycle.Id,
			templateId,
			employeeId,
			reportingManagerEmployeeId,
			hodEmployeeId,
			principalEmployeeId);
	}
}
