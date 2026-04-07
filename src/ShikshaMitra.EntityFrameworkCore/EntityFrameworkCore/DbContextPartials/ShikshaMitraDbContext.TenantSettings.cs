using Microsoft.EntityFrameworkCore;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.TenantSettings.AcademicSetups.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.RoomsManagement.Buildings;
using ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.Standards;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
    // ───────────────────────────────────────────────
    // Tenant Settings — Academic Setups
    // ───────────────────────────────────────────────
    public DbSet<AcademicYear> AcademicYears { get; set; }
    public DbSet<AcademicYearPermission> AcademicYearPermissions { get; set; }
    public DbSet<EducationBoard> EducationBoards { get; set; }
    public DbSet<EducationBoardPermission> EducationBoardPermissions { get; set; }
    public DbSet<EducationMediumStage> EducationBoardStages { get; set; }
    public DbSet<EducationMediumStagePermission> EducationBoardStagePermissions { get; set; }
    public DbSet<EducationMedium> EducationMediums { get; set; }
    public DbSet<EducationMediumPermission> EducationMediumPermissions { get; set; }
    public DbSet<Standard> Standards { get; set; }
    public DbSet<StandardPermission> StandardPermissions { get; set; }
    public DbSet<Division> Divisions { get; set; }
    public DbSet<DivisionPermission> DivisionPermissions { get; set; }

    // ───────────────────────────────────────────────
    // Tenant Settings — General
    // ───────────────────────────────────────────────
    public DbSet<ShiftManagement> ShiftManagements { get; set; }
    public DbSet<InstituteSetupExtraProperties> InstituteSetupExtraProperties { get; set; }

    // ───────────────────────────────────────────────
    // Tenant Settings — Rooms Management
    // ───────────────────────────────────────────────
    public DbSet<Building> Buildings { get; set; }
    public DbSet<Floor> Floors { get; set; }
    public DbSet<RoomType> RoomTypes { get; set; }
    public DbSet<Room> Rooms { get; set; }
}
