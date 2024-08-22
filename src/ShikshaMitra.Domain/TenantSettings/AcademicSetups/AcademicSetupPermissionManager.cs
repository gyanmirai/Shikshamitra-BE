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
	IGuidGenerator guidGenerator)
	: DomainService
{
	public AcademicYearPermission CreateAcademicYearPermission(Guid userId, Guid academicYearId) => new AcademicYearPermission(
			id: guidGenerator.Create(),
			tenantId: CurrentTenant.Id,
			userId: userId,
			academicYearId: academicYearId
		);

	public List<AcademicYearPermission> CreateAcademicYearPermissions(Guid userId, List<Guid> academicYearIds) =>
		academicYearIds.ConvertAll(academicYearId => CreateAcademicYearPermission(userId, academicYearId));

	public EducationBoardPermission CreateEducationBoardPermission(Guid userId, Guid educationBoardId) => new(
			id: guidGenerator.Create(),
			tenantId: CurrentTenant.Id,
			userId: userId,
			educationBoardId: educationBoardId
		);

	public List<EducationBoardPermission> CreateEducationBoardPermissions(Guid userId, List<Guid> educationBoardIds) =>
		educationBoardIds.ConvertAll(educationBoardIds => CreateEducationBoardPermission(userId, educationBoardIds));

	public EducationMediumPermission CreateEducationMediumPermission(Guid userId, Guid educationMediumId) => new(
			id: guidGenerator.Create(),
			tenantId: CurrentTenant.Id,
			userId: userId,
			educationMediumId: educationMediumId
		);

	public List<EducationMediumPermission> CreateEducationMediumPermissions(Guid userId, List<Guid> educationMediumIds) =>
		educationMediumIds.ConvertAll(educationMediumId => CreateEducationMediumPermission(userId, educationMediumId));

	public EducationMediumStagePermission CreateEducationMediumStagePermission(Guid userId, Guid educationMediumStageId) => new(
			id: guidGenerator.Create(),
			tenantId: CurrentTenant.Id,
			userId: userId,
			educationMediumStageId: educationMediumStageId
		);

	public List<EducationMediumStagePermission> CreateEducationMediumStagePermissions(Guid userId, List<Guid> educationMediumStageIds) =>
		educationMediumStageIds.ConvertAll(educationMediumStageId => CreateEducationMediumStagePermission(userId, educationMediumStageId));

	public StandardPermission CreateStandardPermission(Guid userId, Guid standardId) => new(
			id: guidGenerator.Create(),
			tenantId: CurrentTenant.Id,
			userId: userId,
			standardId: standardId
		);

	public List<StandardPermission> CreateStandardPermissions(Guid userId, List<Guid> standardIds) =>
		standardIds.ConvertAll(standardId => CreateStandardPermission(userId, standardId));

	public DivisionPermission CreateDivisionPermission(Guid userId, Guid divisionId) => new(
			id: guidGenerator.Create(),
			tenantId: CurrentTenant.Id,
			userId: userId,
			divisionId: divisionId
		);

	public List<DivisionPermission> CreateDivisionPermissions(Guid userId, List<Guid> divisionIds) =>
		divisionIds.ConvertAll(divisionId => CreateDivisionPermission(userId, divisionId));
}