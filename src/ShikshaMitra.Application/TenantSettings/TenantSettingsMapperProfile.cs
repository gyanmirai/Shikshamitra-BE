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
using ShikshaMitra.TenantSettings.RoomsManagement.Buildings;
using ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using System.Linq;
using System.Collections.Generic;

namespace ShikshaMitra.TenantSettings;

public class TenantSettingsMapperProfile : Profile
{
	public TenantSettingsMapperProfile()
	{
		#region Academic Setup
		// Mapping for AcademicYear
		CreateMap<AcademicYear, AcademicYearDto>()
			.ForMember(dest => dest.StartYear, opt => opt.MapFrom(src => src.StartDate))
			.ForMember(dest => dest.EndYear, opt => opt.MapFrom(src => src.EndDate));
        CreateMap<CreateUpdateAcademicYearDto, AcademicYear>();

        // Mapping for EducationBoard
        CreateMap<EducationBoard, EducationBoardDto>();
        CreateMap<CreateUpdateEducationBoardDto, EducationBoard>();

        // Mapping for EducationBoardStage
        CreateMap<EducationMediumStage, EducationMediumStageDto>()
			.ForMember(dest => dest.Standards, opt => opt.MapFrom(src => (src.Standards ?? new List<Standard>()).OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ThenBy(x => x.Id)));
        CreateMap<CreateUpdateEducationMediumStageDto, EducationMediumStage>().ForMember(dest => dest.SortOrder, opt => opt.Ignore());

        // Mapping for EducationMedium
        CreateMap<EducationMedium, EducationMediumDto>()
			.ForMember(dest => dest.EducationMediumStages, opt => opt.MapFrom(src => (src.EducationMediumStages ?? new List<EducationMediumStage>()).OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ThenBy(x => x.Id)));
        CreateMap<CreateUpdateEducationMediumDto, EducationMedium>();

        // Mapping for Standard
        CreateMap<Standard, StandardDto>()
			.ForMember(dest => dest.Divisions, opt => opt.MapFrom(src => (src.Divisions ?? new List<Division>()).OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ThenBy(x => x.Id)));
        CreateMap<CreateUpdateStandardDto, Standard>().ForMember(dest => dest.SortOrder, opt => opt.Ignore());

		// Mapping for Division
		CreateMap<Division, DivisionDto>()
			.ForMember(dest => dest.RoomId, opt => opt.MapFrom(src => src.RoomNumber));
        CreateMap<CreateUpdateDivisionDto, Division>().ForMember(dest => dest.SortOrder, opt => opt.Ignore());

		#endregion


		#region ShiftManagemetnt
		CreateMap<ShiftManagement, ShiftManagementDto>();
		CreateMap<CreateUpdateShiftManagementDto, ShiftManagement>();
		#endregion

		#region Rooms Management - Building
		CreateMap<Building, BuildingDto>();
		CreateMap<CreateBuildingDto, Building>();
		CreateMap<UpdateBuildingDto, Building>();

		// Floor mappings
		CreateMap<Floor, FloorDto>().ForMember(dest => dest.RoomCount, opt => opt.Ignore());
		CreateMap<CreateFloorDto, Floor>();
		CreateMap<UpdateFloorDto, Floor>();
		#endregion

		#region Rooms Management - Room Type
		CreateMap<RoomType, RoomTypeDto>();
		CreateMap<CreateUpdateRoomTypeDto, RoomType>();
		#endregion

		#region Rooms Management - Room
		CreateMap<Room, RoomDto>();
		CreateMap<CreateUpdateRoomDto, Room>();
		#endregion
	}
}
