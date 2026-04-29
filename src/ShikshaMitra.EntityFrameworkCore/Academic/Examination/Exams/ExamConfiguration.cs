using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.Examination.Exams;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Academic.Examination.Exams;

public class ExamConfiguration : IEntityTypeConfiguration<Exam>
{
    public void Configure(EntityTypeBuilder<Exam> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "Exams", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.Property(x => x.ExamName).IsRequired().HasMaxLength(256);
        builder.Property(x => x.ExamTypeId).IsRequired();
        builder.Property(x => x.StandardId).IsRequired();

        builder.HasMany(x => x.ExamDivisions)
            .WithOne()
            .HasForeignKey(x => x.ExamId)
            .IsRequired();

        builder.HasMany(x => x.ExamSubjects)
            .WithOne()
            .HasForeignKey(x => x.ExamId)
            .IsRequired();
    }
}

public class ExamDivisionConfiguration : IEntityTypeConfiguration<ExamDivision>
{
    public void Configure(EntityTypeBuilder<ExamDivision> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "ExamDivisions", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.HasKey(x => x.Id);
        builder.Property(x => x.DivisionId).IsRequired();
    }
}

public class ExamSubjectConfiguration : IEntityTypeConfiguration<ExamSubject>
{
    public void Configure(EntityTypeBuilder<ExamSubject> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "ExamSubjects", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.Property(x => x.SubjectId).IsRequired();
        builder.Property(x => x.ExamDate).IsRequired(false);
        builder.Property(x => x.StartTime).IsRequired(false);
        builder.Property(x => x.EndTime).IsRequired(false);
        builder.Property(x => x.TotalMarks).IsRequired(false);
        builder.Property(x => x.PassingMarks).IsRequired(false);
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasMany(x => x.RoomAssignments)
            .WithOne()
            .HasForeignKey(x => x.ExamSubjectId)
            .IsRequired();
    }
}

public class ExamSubjectDivisionConfiguration : IEntityTypeConfiguration<ExamSubjectDivision>
{
    public void Configure(EntityTypeBuilder<ExamSubjectDivision> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "ExamSubjectDivisions", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.Property(x => x.DivisionId).IsRequired();
        builder.Property(x => x.Capacity).IsRequired();

        // Storing RoomIds as JSON or simpler structure since it's a list of Guids
        // Assuming standard value conversion or handling elsewhere if specific DB provider supports arrays
        // For compatibility, we'll serialize to JSON string if needed, but here we can rely on provider support or Abp's handling if configured.
        // However, standard EF Core doesn't support primitive collections easily on all providers without conversion.
        // I will use a simple JSON conversion.

        builder.Property(e => e.RoomIds)
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions) null),
                v => System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<System.Guid>>(v, (System.Text.Json.JsonSerializerOptions) null)
            );
    }
}
