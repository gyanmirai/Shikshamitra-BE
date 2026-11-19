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

/// <summary>Maintains source memberships and roles within authorized enrollment changes.</summary>
[RemoteService(false)]
public class HouseManagementPlacementService(IRepository<HouseSettings, Guid> houses, IRepository<Division, Guid> divisions)
	: ApplicationService, ITransientDependency
{
	public async Task DepartAsync(StudentEnrollment enrollment, Guid targetMediumId, Guid targetDivisionId)
	{
		EnsureTenant(enrollment);
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

		if (enrollment.DivisionId != targetDivisionId)
			await ReleaseLeadershipAsync(enrollment);
	}

	public async Task DeactivateAsync(StudentEnrollment enrollment, DateTime? effectiveDate)
	{
		EnsureTenant(enrollment);
		if (enrollment.HouseId.HasValue &&
			await houses.FindAsync(h => h.Id == enrollment.HouseId && h.TenantId == CurrentTenant.Id) is { } house)
		{
			house.MemberDeparted(enrollment.Id);
			await houses.UpdateAsync(house);
		}
		await ReleaseLeadershipAsync(enrollment);
		enrollment.Deactivate(effectiveDate);
	}

	private async Task ReleaseLeadershipAsync(StudentEnrollment enrollment)
	{
		if (!enrollment.DivisionId.HasValue)
			return;
		var division = await divisions.FindAsync(d => d.Id == enrollment.DivisionId && d.TenantId == CurrentTenant.Id);
		if (division == null)
			return;
		division.RosterMemberDeparted(enrollment.Id);
		await divisions.UpdateAsync(division);
	}

	private void EnsureTenant(StudentEnrollment enrollment)
	{
		if (enrollment.TenantId != CurrentTenant.Id)
			throw new BusinessException(HouseManagementErrorCodes.InvalidInput);
	}
}
