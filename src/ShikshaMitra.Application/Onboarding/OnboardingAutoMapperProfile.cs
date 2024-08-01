using AutoMapper;
using ShikshaMitra.Onboarding.AcademicYears;
using ShikshaMitra.Onboarding.Divisions;
using ShikshaMitra.Onboarding.EducationalMediums;
using ShikshaMitra.Onboarding.EducationBoards;
using ShikshaMitra.Onboarding.Standards;

namespace ShikshaMitra.MapperProfile;

public class OnboardingAutoMapperProfile : Profile
{
    public OnboardingAutoMapperProfile()
    {
        // Mapping for AcademicYear
        CreateMap<AcademicYear, AcademicYearDto>();
        CreateMap<CreateUpdateAcademicYearDto, AcademicYear>();

        // Mapping for EducationBoard
        CreateMap<EducationBoard, EducationBoardDto>();
        CreateMap<CreateUpdateEducationBoardDto, EducationBoard>();

        // Mapping for EducationBoardStage
        CreateMap<EducationBoardStage, EducationBoardStageDto>();
        CreateMap<CreateUpdateEducationBoardStageDto, EducationBoardStage>();

        // Mapping for EducationalMedium
        CreateMap<EducationalMedium, EducationalMediumDto>();
        CreateMap<CreateUpdateEducationalMediumDto, EducationalMedium>();

        // Mapping for Standard
        CreateMap<Standard, StandardDto>();
        CreateMap<CreateUpdateStandardDto, Standard>();

        // Mapping for Division
        CreateMap<Division, DivisionDto>();
        CreateMap<CreateUpdateDivisionDto, Division>();
    }
}