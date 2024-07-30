using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Onboarding.Divisions;
using ShikshaMitra.Onboarding.EducationalMediums;
using ShikshaMitra.Onboarding.EducationBoards;
using ShikshaMitra.Onboarding.Standards;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Configurations;

// This class combines the configurations for entities related to the onboarding process.
// Implements IEntityTypeConfiguration for each onboarding entity: EducationBoard, EducationalMedium, Standard, and Division.
// This class combines the configurations for entities related to the onboarding process.
// Implements IEntityTypeConfiguration for each onboarding entity: EducationBoard, EducationalMedium, Standard, and Division.
public class OnboardingConfiguration :
    IEntityTypeConfiguration<EducationBoard>,
    IEntityTypeConfiguration<EducationalMedium>,
    IEntityTypeConfiguration<Standard>,
    IEntityTypeConfiguration<Division>
{
    // Configure the EducationBoard entity
    public void Configure(EntityTypeBuilder<EducationBoard> builder)
    {
        // Set the table name and schema
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "EducationBoards", ShikshaMitraConsts.DbSchema);
        // Configure the entity according to conventions
        builder.ConfigureByConvention();
        // Make the Name property required with a max length
        builder.Property(x => x.Name).IsRequired().HasMaxLength(EducationBoardConsts.MaxNameLength);
        // Define one-to-many relationship:
        // One EducationBoard can have many EducationalMediums
        builder.HasMany(x => x.EducationalMediums).WithOne(x => x.EducationBoard)
            .HasForeignKey(x => x.EducationBoardId);
        // Auto-include EducationalMediums in queries
        builder.Navigation(x => x.EducationalMediums).AutoInclude();
    }

    // Configure the EducationalMedium entity
    public void Configure(EntityTypeBuilder<EducationalMedium> builder)
    {
        // Set the table name and schema
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "EducationalMediums", ShikshaMitraConsts.DbSchema);
        // Configure the entity according to conventions
        builder.ConfigureByConvention();
        // Make the Name property required with a max length
        builder.Property(x => x.Name).IsRequired().HasMaxLength(EducationalMediumConsts.MaxNameLength);
        // Define one-to-many relationship:
        // One EducationalMedium can have many Standards
        builder.HasMany(x => x.Standards).WithOne(x => x.EducationalMedium)
            .HasForeignKey(x => x.EducationalMediumId);
    }

    // Configure the Standard entity
    public void Configure(EntityTypeBuilder<Standard> builder)
    {
        // Set the table name and schema
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "Standards", ShikshaMitraConsts.DbSchema);
        // Configure the entity according to conventions
        builder.ConfigureByConvention();
        // Make the Name property required with a max length
        builder.Property(x => x.Name).IsRequired().HasMaxLength(StandardConsts.MaxNameLength);
        // Create an index on the TenantId property
        builder.HasIndex(x => x.TenantId);
        // Define many-to-one relationship:
        // Many Standards can belong to one EducationalMedium
        builder.HasOne(x => x.EducationalMedium).WithMany(x => x.Standards)
            .HasForeignKey(x => x.EducationalMediumId);
    }

    // Configure the Division entity
    public void Configure(EntityTypeBuilder<Division> builder)
    {
        // Set the table name and schema
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "Divisions", ShikshaMitraConsts.DbSchema);
        // Configure the entity according to conventions
        builder.ConfigureByConvention();
        // Make the Name property required with a max length
        builder.Property(x => x.Name).IsRequired().HasMaxLength(DivisionConsts.MaxNameLength);
        // Create an index on the TenantId property
        builder.HasIndex(x => x.TenantId);
        // Define many-to-one relationship:
        // Many Divisions can belong to one Standard
        builder.HasOne(x => x.Standard).WithMany(x => x.Divisions)
            .HasForeignKey(x => x.StandardId);
    }
}