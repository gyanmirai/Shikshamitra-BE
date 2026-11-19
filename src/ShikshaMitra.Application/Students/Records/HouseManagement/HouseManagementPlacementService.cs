using System;
using System.Threading.Tasks;
using ShikshaMitra.Students.Records.Settings;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.Divisions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records.HouseManagement;

/// <summary>Maintains only source memberships during an already-authorized same-year placement change.</summary>
[RemoteService(false)]
public class HouseManagementPlacementService(IRepository<HouseSettings, Guid> houses, IRepository<Division, Guid> divisions)
	: ApplicationService, ITransientDependency
{
	public async Task DepartAsync(StudentEnrollment enrollment, Guid targetMediumId, Guid targetDivisionId)
	{
		if (enrollment.TenantId != CurrentTenant.Id)
			throw new BusinessException(HouseManagementErrorCodes.InvalidInput);
		if (enrollment.HouseId.HasValue)
		{
			var house = await houses.FindAsync(h => h.Id == enrollment.HouseId && h.TenantId == CurrentTenant.Id);
			if (house == null || house.AcademicMediumId != targetMediumId)
			{
				if (house != null)
				{
					house.MemberDeparted(enrollment.Id);
					await houses.UpdateAsync(house);
				}

				enrollment.AssignHouse(null);
			}
		}

		if (enrollment.DivisionId.HasValue && enrollment.DivisionId != targetDivisionId)
		{
			var division = await divisions.FindAsync(d => d.Id == enrollment.DivisionId && d.TenantId == CurrentTenant.Id);
			if (division?.LeaderEnrollmentId == enrollment.Id)
			{
				division.ReplaceLeader(null, division.ConcurrencyStamp);
				await divisions.UpdateAsync(division);
			}
		}
	}
}
