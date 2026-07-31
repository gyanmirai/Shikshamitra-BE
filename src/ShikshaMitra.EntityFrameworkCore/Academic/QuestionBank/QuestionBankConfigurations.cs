using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.QuestionBank;

public class QuestionBankSettingConfiguration : IEntityTypeConfiguration<QuestionBankSetting>
{
    public void Configure(EntityTypeBuilder<QuestionBankSetting> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "QuestionBankSettings", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
        builder.HasIndex(x => x.AcademicYearId);
        builder.HasIndex(x => new { x.TenantId, x.AcademicYearId }).IsUnique();
    }
}

public class QuestionBankItemConfiguration : IEntityTypeConfiguration<QuestionBankItem>
{
    public void Configure(EntityTypeBuilder<QuestionBankItem> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "QuestionBankItems", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
        builder.Property(x => x.QuestionText).IsRequired();
        builder.Property(x => x.TopicName).HasMaxLength(256);
        builder.Property(x => x.PlainTextSearchContent).HasMaxLength(2048);
        builder.Property(x => x.Marks).HasPrecision(10, 2);
        builder.Property(x => x.NegativeMarks).HasPrecision(10, 2);
        builder.Property(x => x.AnswerKey).HasMaxLength(4000);
        builder.Property(x => x.LearningOutcome).HasMaxLength(1000);
        builder.Property(x => x.Explanation).HasMaxLength(4000);
        builder.Property(x => x.MarkingScheme).HasMaxLength(4000);
        builder.Property(x => x.ReviewComment).HasMaxLength(1000);
        builder.HasMany(x => x.Options).WithOne().HasForeignKey(x => x.QuestionBankItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Attachments).WithOne().HasForeignKey(x => x.QuestionBankItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Tags).WithOne().HasForeignKey(x => x.QuestionBankItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.AcademicYearId);
        builder.HasIndex(x => x.StandardId);
        builder.HasIndex(x => x.SubjectId);
        builder.HasIndex(x => x.ChapterId);
        builder.HasIndex(x => x.TopicId);
        builder.HasIndex(x => x.TopicName);
        builder.HasIndex(x => x.QuestionType);
        builder.HasIndex(x => x.DifficultyLevel);
        builder.HasIndex(x => x.BloomLevel);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.Visibility);
        builder.HasIndex(x => x.CreatedByTeacherId);
    }
}

public class QuestionOptionConfiguration : IEntityTypeConfiguration<QuestionOption>
{
    public void Configure(EntityTypeBuilder<QuestionOption> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "QuestionOptions", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
        builder.Property(x => x.OptionText).IsRequired().HasMaxLength(2000);
        builder.HasIndex(x => new { x.QuestionBankItemId, x.SortOrder }).IsUnique();
    }
}

public class QuestionAttachmentConfiguration : IEntityTypeConfiguration<QuestionAttachment>
{
    public void Configure(EntityTypeBuilder<QuestionAttachment> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "QuestionAttachments", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
        builder.Property(x => x.FileName).IsRequired().HasMaxLength(500);
        builder.Property(x => x.FileUrl).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.MimeType).IsRequired().HasMaxLength(200);
    }
}

public class QuestionTagConfiguration : IEntityTypeConfiguration<QuestionTag>
{
    public void Configure(EntityTypeBuilder<QuestionTag> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "QuestionTags", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
        builder.Property(x => x.TagName).IsRequired().HasMaxLength(100);
        builder.HasIndex(x => new { x.QuestionBankItemId, x.TagName }).IsUnique();
    }
}

public class QuestionUsageLogConfiguration : IEntityTypeConfiguration<QuestionUsageLog>
{
    public void Configure(EntityTypeBuilder<QuestionUsageLog> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "QuestionUsageLogs", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
        builder.HasIndex(x => x.QuestionBankItemId);
        builder.HasIndex(x => new { x.Source, x.SourceEntityId });
    }
}
