using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.Examination.Results;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Academic.Examination.Results;

public class ExamResultHeaderConfiguration : IEntityTypeConfiguration<ExamResultHeader>
{
    public void Configure(EntityTypeBuilder<ExamResultHeader> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "ExamResultHeaders", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.Property(x => x.ExamId).IsRequired();
        builder.Property(x => x.DivisionId).IsRequired();
        builder.Property(x => x.Status).IsRequired().HasDefaultValue(ExamResultStatus.Generated);
        builder.Property(x => x.Remarks).HasMaxLength(500).IsRequired(false);

        builder.HasIndex(x => new { x.ExamId, x.DivisionId }).IsUnique();
    }
}

public class ExamResultDetailConfiguration : IEntityTypeConfiguration<ExamResultDetail>
{
    public void Configure(EntityTypeBuilder<ExamResultDetail> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "ExamResultDetails", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.Property(x => x.ResultHeaderId).IsRequired();
        builder.Property(x => x.ExamId).IsRequired();
        builder.Property(x => x.StudentId).IsRequired();
        builder.Property(x => x.DivisionId).IsRequired();
        builder.Property(x => x.TotalMaxMarks).IsRequired();
        builder.Property(x => x.TotalObtainedMarks).IsRequired();
        builder.Property(x => x.Percentage).IsRequired();
        builder.Property(x => x.Grade).HasMaxLength(10).IsRequired();
        builder.Property(x => x.ClassRank).IsRequired(false);
        builder.Property(x => x.SectionRank).IsRequired(false);
        builder.Property(x => x.ResultStatus).IsRequired();

        builder.HasIndex(x => new { x.ResultHeaderId, x.StudentId }).IsUnique();
        builder.HasIndex(x => x.StudentId);
    }
}

public class ExamResultAuditLogConfiguration : IEntityTypeConfiguration<ExamResultAuditLog>
{
    public void Configure(EntityTypeBuilder<ExamResultAuditLog> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "ExamResultAuditLogs", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.Property(x => x.ExamId).IsRequired();
        builder.Property(x => x.DivisionId).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(50).IsRequired();
        builder.Property(x => x.PerformedById).IsRequired();
        builder.Property(x => x.Remarks).HasMaxLength(500).IsRequired(false);

        builder.HasIndex(x => new { x.ExamId, x.DivisionId });
    }
}
