using AutoMapper;
using ShikshaMitra.TenantSettings.AcademicSetups.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;

namespace ShikshaMitra.TenantSettings;

public class TenantSettingsMapperProfile : Profile
{
	public TenantSettingsMapperProfile()
	{
		#region Academic Setup
		// Mapping for AcademicYear
		CreateMap<AcademicYear, AcademicYearDto>();
        CreateMap<CreateUpdateAcademicYearDto, AcademicYear>();

        // Mapping for EducationBoard
        CreateMap<EducationBoard, EducationBoardDto>();
        CreateMap<CreateUpdateEducationBoardDto, EducationBoard>();

        // Mapping for EducationBoardStage
        CreateMap<EducationMediumStage, EducationMediumStageDto>();
        CreateMap<CreateUpdateEducationMediumStageDto, EducationMediumStage>();

        // Mapping for EducationMedium
        CreateMap<EducationMedium, EducationMediumDto>();
        CreateMap<CreateUpdateEducationMediumDto, EducationMedium>();

        // Mapping for Standard
        CreateMap<Standard, StandardDto>();
        CreateMap<CreateUpdateStandardDto, Standard>();

		// Mapping for Division
		CreateMap<Division, DivisionDto>();
        CreateMap<CreateUpdateDivisionDto, Division>();

		#endregion


		#region ShiftManagemetnt
		CreateMap<ShiftManagement, ShiftManagementDto>();
		CreateMap<CreateUpdateShiftManagementDto, ShiftManagement>();
		#endregion
	}
}
