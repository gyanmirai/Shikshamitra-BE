using System;
using System.Linq.Expressions;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;

namespace ShikshaMitra.TenantSettings.AcademicSetups;

internal static class AcademicHierarchyProjection
{
	internal static readonly Expression<Func<EducationBoard, EducationBoardDto>> Board = x => new EducationBoardDto {
		Id = x.Id, AcademicYearId = x.AcademicYearId, Type = x.Type,
		StartDate = x.StartDate, EndDate = x.EndDate, CreationTime = x.CreationTime, CreatorId = x.CreatorId,
		LastModificationTime = x.LastModificationTime, LastModifierId = x.LastModifierId, EducationMediums = new()
	};
	internal static readonly Expression<Func<EducationMedium, EducationMediumDto>> Medium = x => new EducationMediumDto {
		Id = x.Id, Name = x.Name, EducationBoardId = x.EducationBoardId, CreationTime = x.CreationTime, CreatorId = x.CreatorId,
		LastModificationTime = x.LastModificationTime, LastModifierId = x.LastModifierId, EducationMediumStages = new()
	};
	internal static readonly Expression<Func<EducationMediumStage, EducationMediumStageDto>> Stage = x => new EducationMediumStageDto {
		Id = x.Id, Name = x.Name, SortOrder = x.SortOrder, EducationMediumId = x.EducationMediumId, CreationTime = x.CreationTime, CreatorId = x.CreatorId,
		LastModificationTime = x.LastModificationTime, LastModifierId = x.LastModifierId, Standards = new()
	};
	internal static readonly Expression<Func<Standard, StandardDto>> Standard = x => new StandardDto {
		Id = x.Id, Name = x.Name, SortOrder = x.SortOrder, AgeLowerLimit = x.AgeLowerLimit, EducationMediumStageId = x.EducationMediumStageId, ShiftId = x.ShiftId,
		CreationTime = x.CreationTime, CreatorId = x.CreatorId, LastModificationTime = x.LastModificationTime, LastModifierId = x.LastModifierId,
		Shift = x.Shift == null ? new ShiftManagementDto() : new ShiftManagementDto {
			Id = x.Shift.Id, ShiftName = x.Shift.ShiftName, StartTime = x.Shift.StartTime, EndTime = x.Shift.EndTime }, Divisions = new()
	};
	internal static readonly Expression<Func<Division, DivisionDto>> Division = x => new DivisionDto {
		Id = x.Id, Name = x.Name, SortOrder = x.SortOrder, StandardId = x.StandardId, Strength = x.Strength, RoomNumber = x.RoomNumber,
		RoomId = x.RoomId.HasValue ? x.RoomId.Value.ToString().ToLower() : x.RoomNumber,
		CreationTime = x.CreationTime, CreatorId = x.CreatorId, LastModificationTime = x.LastModificationTime, LastModifierId = x.LastModifierId
	};
}
