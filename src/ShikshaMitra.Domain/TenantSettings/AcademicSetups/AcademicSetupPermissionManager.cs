using ShikshaMitra.TenantSettings.AcademicSetups.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;
using Volo.Abp.Users;

namespace ShikshaMitra.TenantSettings.AcademicSetups;

public class AcademicSetupPermissionManager(
	ICurrentUser currentUser,
	IGuidGenerator guidGenerator)
	: DomainService
{
	public AcademicYearPermission CreateAcademicYearPermission(Guid academicYearId) => new AcademicYearPermission(
			id: guidGenerator.Create(),
			tenantId: CurrentTenant.Id,
			userId: currentUser.Id,
			academicYearId: academicYearId
		);

	public List<AcademicYearPermission> CreateAcademicYearPermissions(List<Guid> academicYearIds) =>
		academicYearIds.ConvertAll(CreateAcademicYearPermission);

	public EducationBoardPermission CreateEducationBoardPermission(Guid educationBoardId) => new(
			id: guidGenerator.Create(),
			tenantId: CurrentTenant.Id,
			userId: currentUser.Id,
			educationBoardId: educationBoardId
		);

	public List<EducationBoardPermission> CreateEducationBoardPermissions(List<Guid> educationBoardIds) =>
		educationBoardIds.ConvertAll(CreateEducationBoardPermission);

	public EducationMediumPermission CreateEducationMediumPermission(Guid educationMediumId) => new(
			id: guidGenerator.Create(),
			tenantId: CurrentTenant.Id,
			userId: currentUser.Id,
			educationMediumId: educationMediumId
		);

	public List<EducationMediumPermission> CreateEducationMediumPermissions(List<Guid> educationMediumIds) =>
		educationMediumIds.ConvertAll(CreateEducationMediumPermission);

	public EducationMediumStagePermission CreateEducationMediumStagePermission(Guid educationMediumStageId)
	{
		return new EducationMediumStagePermission(
			id: guidGenerator.Create(),
			tenantId: CurrentTenant.Id,
			userId: currentUser.Id,
			educationMediumStageId: educationMediumStageId
		);
	}

	public List<EducationMediumStagePermission> CreateEducationMediumStagePermissions(
		List<Guid> educationMediumStageIds) =>
		educationMediumStageIds.ConvertAll(CreateEducationMediumStagePermission);

	public StandardPermission CreateStandardPermission(Guid standardId)
	{
		return new StandardPermission(
			id: guidGenerator.Create(),
			tenantId: CurrentTenant.Id,
			userId: currentUser.Id,
			standardId: standardId
		);
	}

	public List<StandardPermission> CreateStandardPermissions(List<Guid> standardIds) =>
		standardIds.ConvertAll(CreateStandardPermission);

	public DivisionPermission CreateDivisionPermission(Guid divisionId)
	{
		return new DivisionPermission(
			id: guidGenerator.Create(),
			tenantId: CurrentTenant.Id,
			userId: currentUser.Id,
			divisionId: divisionId
		);
	}

	public List<DivisionPermission> CreateDivisionPermissions(List<Guid> divisionIds) =>
		divisionIds.ConvertAll(CreateDivisionPermission);
}