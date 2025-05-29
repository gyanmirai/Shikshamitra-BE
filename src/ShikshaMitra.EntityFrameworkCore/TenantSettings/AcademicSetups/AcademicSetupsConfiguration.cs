using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.TenantSettings.AcademicSetups.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Configurations;

public class AcademicSetupsConfiguration :
	IEntityTypeConfiguration<EducationBoard>,
	IEntityTypeConfiguration<EducationBoardPermission>,
	IEntityTypeConfiguration<EducationMediumStage>,
	IEntityTypeConfiguration<EducationMediumStagePermission>,
	IEntityTypeConfiguration<EducationMedium>,
	IEntityTypeConfiguration<EducationMediumPermission>,
	IEntityTypeConfiguration<AcademicYear>,
	IEntityTypeConfiguration<AcademicYearPermission>,
	IEntityTypeConfiguration<Standard>,
	IEntityTypeConfiguration<StandardPermission>,
	IEntityTypeConfiguration<Division>,
	IEntityTypeConfiguration<DivisionPermission>
{
	// Configure the AcademicYear entity
	public void Configure(EntityTypeBuilder<AcademicYear> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AcademicYears", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		// Make the Name property required with a max length
		builder.Property(x => x.Name).IsRequired().HasMaxLength(AcademicYearConsts.MaxNameLength);

		// Create an index on the Name, TenantId, StartDate, and EndDate properties
		builder.HasIndex(x => new { x.Name, x.TenantId, x.StartDate, x.EndDate }).IsUnique(); // Ensuring uniqueness
	}

	// Configure the EducationBoard entity
	public void Configure(EntityTypeBuilder<EducationBoard> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "EducationBoards", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		// Make the Name property required with a max length
		builder.Navigation(x => x.EducationMediums).AutoInclude();

		// Define many-to-one relationship:
		// Many Education can belong to one AcademicYear
		builder.HasOne(x => x.AcademicYear).WithMany().HasForeignKey(x => x.AcademicYearId);
	}

	// Configure the EducationMedium entity
	public void Configure(EntityTypeBuilder<EducationMedium> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "EducationMediums", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		// Make the Name property required with a max length
		builder.Property(x => x.Name).IsRequired().HasMaxLength(EducationMediumConsts.MaxNameLength);

		builder.Navigation(x => x.EducationMediumStages).AutoInclude();
	}

	// Configure the EducationBoardStage entity
	public void Configure(EntityTypeBuilder<EducationMediumStage> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "EducationMediumStages", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		// Make the Name property required with a max length
		builder.Property(x => x.Name).IsRequired().HasMaxLength(EducationMediumStageConsts.MaxNameLength);

		// Define one-to-many relationship:
		// One EducationMedium can have many EducationMediumStages
		builder.HasOne(x => x.EducationMedium).WithMany(x => x.EducationMediumStages)
			.HasForeignKey(x => x.EducationMediumId);

		builder.Navigation(x => x.Standards).AutoInclude();
	}

	// Configure the Standard entity
	public void Configure(EntityTypeBuilder<Standard> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "Standards", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		// Make the Name property required with a max length
		builder.Property(x => x.Name).IsRequired().HasMaxLength(StandardConsts.MaxNameLength);

		// Define many-to-one relationship:
		// Many Standards can belong to one EducationBoardStage
		builder.HasOne(x => x.EducationMediumStage).WithMany(x => x.Standards)
			.HasForeignKey(x => x.EducationMediumStageId);

		builder.Navigation(x => x.Divisions).AutoInclude();

		builder.Navigation(x => x.Shift).AutoInclude();
	}

	// Configure the Division entity
	public void Configure(EntityTypeBuilder<Division> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "Divisions", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		// Make the Name property required with a max length
		builder.Property(x => x.Name).IsRequired().HasMaxLength(DivisionConsts.MaxNameLength);

		// Make the Strength property required with a range
		builder.Property(x => x.Strength)
			.IsRequired();

		// Define many-to-one relationship:
		// Many Divisions can belong to one Standard
		builder.HasOne(x => x.Standard).WithMany(x => x.Divisions).HasForeignKey(x => x.StandardId);
	}

	public void Configure(EntityTypeBuilder<EducationBoardPermission> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "EducationBoardPermissions", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
	}

	public void Configure(EntityTypeBuilder<EducationMediumStagePermission> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "EducationMediumStagePermissions",
			ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
	}

	public void Configure(EntityTypeBuilder<EducationMediumPermission> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "EducationMediumPermissions", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
	}

	public void Configure(EntityTypeBuilder<AcademicYearPermission> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AcademicYearPermissions", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
	}

	public void Configure(EntityTypeBuilder<StandardPermission> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StandardPermissions", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
	}

	public void Configure(EntityTypeBuilder<DivisionPermission> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "DivisionPermissions", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
	}
}