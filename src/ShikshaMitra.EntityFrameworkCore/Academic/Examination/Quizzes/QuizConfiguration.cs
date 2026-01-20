using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.Examination.Quizzes;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Academic.Examination.Quizzes;

public class QuizConfiguration : IEntityTypeConfiguration<Quiz>
{
    public void Configure(EntityTypeBuilder<Quiz> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "Quizzes", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.Property(x => x.Title).IsRequired().HasMaxLength(256);
        builder.Property(x => x.ClassId).IsRequired();
        builder.Property(x => x.SubjectId).IsRequired();
        builder.Property(x => x.Duration).IsRequired();
        builder.Property(x => x.TotalMarks).IsRequired();
        builder.Property(x => x.PassingMarks).IsRequired();

        builder.HasMany(x => x.Divisions)
            .WithOne()
            .HasForeignKey(x => x.QuizId)
            .IsRequired();

        builder.HasMany(x => x.Chapters)
            .WithOne()
            .HasForeignKey(x => x.QuizId)
            .IsRequired();

        builder.HasMany(x => x.Questions)
            .WithOne()
            .HasForeignKey(x => x.QuizId)
            .IsRequired();
    }
}

public class QuizDivisionConfiguration : IEntityTypeConfiguration<QuizDivision>
{
    public void Configure(EntityTypeBuilder<QuizDivision> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "QuizDivisions", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
        
        builder.Property(x => x.DivisionId).IsRequired();
    }
}

public class QuizChapterConfiguration : IEntityTypeConfiguration<QuizChapter>
{
    public void Configure(EntityTypeBuilder<QuizChapter> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "QuizChapters", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
        
        builder.Property(x => x.ChapterId).IsRequired();
    }
}

public class QuizQuestionConfiguration : IEntityTypeConfiguration<QuizQuestion>
{
    public void Configure(EntityTypeBuilder<QuizQuestion> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "QuizQuestions", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.Property(x => x.QuestionText).IsRequired();
        
        builder.HasMany(x => x.Options)
            .WithOne()
            .HasForeignKey(x => x.QuizQuestionId)
            .IsRequired();
    }
}

public class QuizQuestionOptionConfiguration : IEntityTypeConfiguration<QuizQuestionOption>
{
    public void Configure(EntityTypeBuilder<QuizQuestionOption> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "QuizQuestionOptions", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.Property(x => x.OptionText).IsRequired();
    }
}
