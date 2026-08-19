using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.QuestionPapers;

public class QuestionPaperSettingConfiguration : IEntityTypeConfiguration<QuestionPaperSetting>
{
    public void Configure(EntityTypeBuilder<QuestionPaperSetting> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "QuestionPaperSettings", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
        builder.Property(x => x.DistributionTolerancePercentage).HasPrecision(5, 2);
        builder.HasIndex(x => new { x.TenantId, x.AcademicYearId }).IsUnique();
    }
}

public class QuestionPaperConfiguration : IEntityTypeConfiguration<QuestionPaper>
{
    public void Configure(EntityTypeBuilder<QuestionPaper> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "QuestionPapers", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
        builder.Property(x => x.PaperTitle).IsRequired().HasMaxLength(500);
        builder.Property(x => x.CreationMode).IsRequired().HasMaxLength(50);
        builder.Property(x => x.TotalMarks).HasPrecision(10, 2);
        builder.HasMany(x => x.Sections).WithOne().HasForeignKey(x => x.QuestionPaperId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Questions).WithOne().HasForeignKey(x => x.QuestionPaperId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.AcademicYearId);
        builder.HasIndex(x => x.ExamId);
        builder.HasIndex(x => x.ExamScheduleId);
        builder.HasIndex(x => x.StandardId);
        builder.HasIndex(x => x.SubjectId);
        builder.HasIndex(x => x.Status);
    }
}

public class QuestionPaperSectionConfiguration : IEntityTypeConfiguration<QuestionPaperSection>
{
    public void Configure(EntityTypeBuilder<QuestionPaperSection> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "QuestionPaperSections", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
        builder.Property(x => x.SectionName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Marks).HasPrecision(10, 2);
        // Section labels are presentation data and may legitimately repeat (for example,
        // two separate "Section B" groups with different mark values).
        builder.HasIndex(x => x.QuestionPaperId);
    }
}

public class QuestionPaperQuestionConfiguration : IEntityTypeConfiguration<QuestionPaperQuestion>
{
    public void Configure(EntityTypeBuilder<QuestionPaperQuestion> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "QuestionPaperQuestions", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
        builder.Property(x => x.Marks).HasPrecision(10, 2);
        builder.Property(x => x.ChoiceGroupCode).HasMaxLength(50);
        builder.HasIndex(x => new { x.QuestionPaperId, x.QuestionPaperSectionId, x.QuestionNumber }).IsUnique();
        builder.HasIndex(x => x.QuestionBankItemId);
    }
}

public class QuestionPaperBlueprintConfiguration : IEntityTypeConfiguration<QuestionPaperBlueprint>
{
    public void Configure(EntityTypeBuilder<QuestionPaperBlueprint> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "QuestionPaperBlueprints", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
        builder.Property(x => x.TotalMarks).HasPrecision(10, 2);
        builder.HasMany(x => x.ChapterRules).WithOne().HasForeignKey(x => x.QuestionPaperBlueprintId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.DifficultyRules).WithOne().HasForeignKey(x => x.QuestionPaperBlueprintId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.BloomRules).WithOne().HasForeignKey(x => x.QuestionPaperBlueprintId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.QuestionPaperId).IsUnique();
    }
}

public class QuestionPaperBlueprintChapterRuleConfiguration : IEntityTypeConfiguration<QuestionPaperBlueprintChapterRule>
{
    public void Configure(EntityTypeBuilder<QuestionPaperBlueprintChapterRule> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "QuestionPaperBlueprintChapterRules", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
        builder.Property(x => x.Marks).HasPrecision(10, 2);
    }
}

public class QuestionPaperBlueprintDifficultyRuleConfiguration : IEntityTypeConfiguration<QuestionPaperBlueprintDifficultyRule>
{
    public void Configure(EntityTypeBuilder<QuestionPaperBlueprintDifficultyRule> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "QuestionPaperBlueprintDifficultyRules", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
        builder.Property(x => x.Percentage).HasPrecision(5, 2);
    }
}

public class QuestionPaperBlueprintBloomRuleConfiguration : IEntityTypeConfiguration<QuestionPaperBlueprintBloomRule>
{
    public void Configure(EntityTypeBuilder<QuestionPaperBlueprintBloomRule> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "QuestionPaperBlueprintBloomRules", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
        builder.Property(x => x.Percentage).HasPrecision(5, 2);
    }
}
