using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Onboarding.AcademicYears;
using ShikshaMitra.Onboarding.Divisions;
using ShikshaMitra.Onboarding.EducationalMediums;
using ShikshaMitra.Onboarding.EducationBoards;
using ShikshaMitra.Onboarding.Standards;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Configurations;


public class AcademicSetupsConfiguration :
   IEntityTypeConfiguration<EducationBoard>,
    IEntityTypeConfiguration<EducationBoardStage>,
    IEntityTypeConfiguration<EducationalMedium>,
    IEntityTypeConfiguration<AcademicYear>,
    IEntityTypeConfiguration<Standard>,
    IEntityTypeConfiguration<Division>
{
    // Configure the AcademicYear entity
    public void Configure(EntityTypeBuilder<AcademicYear> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AcademicYears", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        // Make the Name property required with a max length
        builder.Property(x => x.Name).IsRequired().HasMaxLength(AcademicYearConsts.MaxNameLength);

        // Create an index on the TenantId, StartDate, and EndDate properties
        builder.HasIndex(x => new { x.TenantId, x.StartDate, x.EndDate }).IsUnique(); // Ensuring uniqueness
    }


    // Configure the EducationBoard entity
    public void Configure(EntityTypeBuilder<EducationBoard> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "EducationBoards", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        // Make the Name property required with a max length
        builder.Property(x => x.Name).IsRequired().HasMaxLength(EducationBoardConsts.MaxNameLength);
    }

    // Configure the EducationBoardStage entity
    public void Configure(EntityTypeBuilder<EducationBoardStage> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "EducationBoardStages", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        // Make the Name property required with a max length
        builder.Property(x => x.Name).IsRequired().HasMaxLength(EducationBoardStageConsts.MaxNameLength);

        // Define one-to-many relationship:
        // One EducationBoard can have many EducationBoardStages
        builder.HasOne(x => x.EducationBoard).WithMany(x => x.EducationBoardStages)
            .HasForeignKey(x => x.EducationBoardId);
    }

    // Configure the EducationalMedium entity
    public void Configure(EntityTypeBuilder<EducationalMedium> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "EducationalMediums", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        // Make the Name property required with a max length
        builder.Property(x => x.Name).IsRequired().HasMaxLength(EducationalMediumConsts.MaxNameLength);

    }

    // Configure the Standard entity
    public void Configure(EntityTypeBuilder<Standard> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "Standards", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        // Make the Name property required with a max length
        builder.Property(x => x.Name).IsRequired().HasMaxLength(StandardConsts.MaxNameLength);

        // Create an index on the TenantId, AcademicYearId, EducationBoardId, EducationBoardStageId, EducationalMediumId, and Name properties
        builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.EducationBoardId, x.EducationBoardStageId, x.EducationalMediumId, x.Name }).IsUnique();

        // Define many-to-one relationship:
        // Many Standards can belong to one AcademicYear
        builder.HasOne(x => x.AcademicYear).WithMany().HasForeignKey(x => x.AcademicYearId);

        // Define many-to-one relationship:
        // Many Standards can belong to one EducationalMedium
        builder.HasOne(x => x.EducationalMedium).WithMany(x => x.Standards).HasForeignKey(x => x.EducationalMediumId);

        // Define many-to-one relationship:
        // Many Standards can belong to one EducationBoard
        builder.HasOne(x => x.EducationBoard).WithMany().HasForeignKey(x => x.EducationBoardId);

        // Define many-to-one relationship:
        // Many Standards can belong to one EducationBoardStage
        builder.HasOne(x => x.EducationBoardStage).WithMany().HasForeignKey(x => x.EducationBoardStageId);


    }

    // Configure the Division entity
    public void Configure(EntityTypeBuilder<Division> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "Divisions", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        // Make the Name property required with a max length
        builder.Property(x => x.Name).IsRequired().HasMaxLength(DivisionConsts.MaxNameLength);

        // Define many-to-one relationship:
        // Many Divisions can belong to one Standard
        builder.HasOne(x => x.Standard).WithMany(x => x.Divisions).HasForeignKey(x => x.StandardId);
    }
}