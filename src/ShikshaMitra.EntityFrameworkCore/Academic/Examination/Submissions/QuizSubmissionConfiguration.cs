using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.Examination.Submissions;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Academic.Examination.Submissions;

public class QuizSubmissionConfiguration : IEntityTypeConfiguration<QuizSubmission>
{
    public void Configure(EntityTypeBuilder<QuizSubmission> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "QuizSubmissions", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.Property(x => x.QuizId).IsRequired();
        builder.Property(x => x.StudentId).IsRequired();

        builder.HasMany(x => x.Answers)
            .WithOne()
            .HasForeignKey(x => x.QuizSubmissionId)
            .IsRequired();
    }
}

public class QuizSubmissionAnswerConfiguration : IEntityTypeConfiguration<QuizSubmissionAnswer>
{
    public void Configure(EntityTypeBuilder<QuizSubmissionAnswer> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "QuizSubmissionAnswers", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.Property(x => x.QuestionId).IsRequired();
        builder.Property(x => x.Answer).IsRequired();
    }
}
