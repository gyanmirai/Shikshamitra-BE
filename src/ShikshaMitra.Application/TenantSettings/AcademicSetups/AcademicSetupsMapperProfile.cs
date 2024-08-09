using AutoMapper;
using ShikshaMitra.Onboarding.AcademicYears;
using ShikshaMitra.Onboarding.Divisions;
using ShikshaMitra.Onboarding.EducationMediums;
using ShikshaMitra.Onboarding.EducationBoards;
using ShikshaMitra.Onboarding.Standards;

namespace ShikshaMitra.MapperProfile;

public class AcademicSetupsMapperProfile : Profile
{
    public AcademicSetupsMapperProfile()
    {
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
    }
}