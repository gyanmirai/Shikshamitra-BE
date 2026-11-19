using System;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records.HouseManagement;

[RemoteService(false)]
public class HouseDeletionGuard(
	IRepository<StudentEnrollment, Guid> enrollments,
	IRepository<StudentAdditionalDetail, Guid> legacyDetails)
	: ApplicationService, ITransientDependency
{
	public async Task EnsureEmptyAsync(Guid houseId)
	{
		if (await enrollments.AnyAsync(e => e.TenantId == CurrentTenant.Id && e.HouseId == houseId) || await legacyDetails.AnyAsync(e => e.TenantId == CurrentTenant.Id && e.HouseId == houseId))
			throw new BusinessException(HouseManagementErrorCodes.HousePopulated);
	}
}
