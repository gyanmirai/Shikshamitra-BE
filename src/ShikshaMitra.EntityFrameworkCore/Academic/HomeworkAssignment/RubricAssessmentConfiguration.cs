using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class AssessmentRubricConfiguration : IEntityTypeConfiguration<AssessmentRubric>
{
	public void Configure(EntityTypeBuilder<AssessmentRubric> builder)
	{
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "AssessmentRubrics",
			ShikshaMitraConsts.DbSchema
		);
		builder.ConfigureByConvention();

		builder.Property(x => x.StandardId).IsRequired(false);
		builder.Property(x => x.SubjectId).IsRequired(false);
		builder.Property(x => x.IsTemplate).IsRequired().HasDefaultValue(false);
		builder.Property(x => x.AppliesToWorkType).HasConversion<int>().IsRequired(false);
		builder.Property(x => x.ScoringModel).HasConversion<int>().IsRequired().HasDefaultValue(RubricScoringModel.PointsBased);
		builder.Property(x => x.Visibility).HasConversion<int>().IsRequired().HasDefaultValue(RubricVisibility.Institution);
		builder.Property(x => x.Title).IsRequired().HasMaxLength(256);
		builder.Property(x => x.Description).HasMaxLength(1024);
		builder.Property(x => x.EvaluationInstructions).HasMaxLength(2048);
		builder.Property(x => x.StudentInstructions).HasMaxLength(2048);
		builder.Property(x => x.TagsJson);
		builder.Property(x => x.PerformanceLevelsJson);
		builder.Property(x => x.TotalMarks).HasPrecision(10, 2);
		builder.Property(x => x.Version).IsRequired().HasDefaultValue(1);
		builder.Property(x => x.Status).HasConversion<int>().IsRequired();

		builder
			.HasMany(x => x.Criteria)
			.WithOne()
			.HasForeignKey(x => x.RubricId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => new { x.StandardId, x.SubjectId });
		builder.HasIndex(x => x.AcademicMediumId);
		builder.HasIndex(x => x.HomeworkId);
		builder.HasIndex(x => x.AssignmentId);
		builder.HasIndex(x => x.HomeworkTypeId);
		builder.HasIndex(x => x.AssignmentTypeId);
		builder.HasIndex(x => x.SourceTemplateId);
		builder.HasIndex(x => x.IsTemplate);
		builder.HasIndex(x => x.ScoringModel);
		builder.HasIndex(x => x.Visibility);
		builder.HasIndex(x => x.Status);
	}
}

public class AssessmentRubricCriterionConfiguration : IEntityTypeConfiguration<AssessmentRubricCriterion>
{
	public void Configure(EntityTypeBuilder<AssessmentRubricCriterion> builder)
	{
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "AssessmentRubricCriteria",
			ShikshaMitraConsts.DbSchema
		);
		builder.ConfigureByConvention();

		builder.Property(x => x.RubricId).IsRequired();
		builder.Property(x => x.Title).IsRequired().HasMaxLength(256);
		builder.Property(x => x.Description).HasMaxLength(1024);
		builder.Property(x => x.StudentDescription).HasMaxLength(1024);
		builder.Property(x => x.TeacherGuidance).HasMaxLength(1024);
		builder.Property(x => x.IsRequired).IsRequired().HasDefaultValue(true);
		builder.Property(x => x.MaxMarks).HasPrecision(10, 2);
		builder.Property(x => x.DisplayOrder).IsRequired();

		builder.HasIndex(x => x.RubricId);
		builder.HasIndex(x => new { x.RubricId, x.DisplayOrder });
	}
}

public class RubricEvaluationConfiguration : IEntityTypeConfiguration<RubricEvaluation>
{
	public void Configure(EntityTypeBuilder<RubricEvaluation> builder)
	{
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "RubricEvaluations",
			ShikshaMitraConsts.DbSchema
		);
		builder.ConfigureByConvention();

		builder.Property(x => x.RubricId).IsRequired();
		builder.Property(x => x.StudentSubmissionId).IsRequired();
		builder.Property(x => x.StudentId).IsRequired();
		builder.Property(x => x.EvaluatorId).IsRequired();
		builder.Property(x => x.TotalMarks).HasPrecision(10, 2);
		builder.Property(x => x.Remarks).HasMaxLength(1024);
		builder.Property(x => x.Status).HasConversion<int>().IsRequired();

		builder
			.HasMany(x => x.Scores)
			.WithOne()
			.HasForeignKey(x => x.EvaluationId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => x.RubricId);
		builder.HasIndex(x => x.StudentSubmissionId);
		builder.HasIndex(x => new { x.RubricId, x.StudentSubmissionId }).IsUnique();
		builder.HasIndex(x => x.Status);
	}
}

public class RubricCriterionScoreConfiguration : IEntityTypeConfiguration<RubricCriterionScore>
{
	public void Configure(EntityTypeBuilder<RubricCriterionScore> builder)
	{
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "RubricCriterionScores",
			ShikshaMitraConsts.DbSchema
		);
		builder.ConfigureByConvention();

		builder.Property(x => x.EvaluationId).IsRequired();
		builder.Property(x => x.CriterionId).IsRequired();
		builder.Property(x => x.Marks).HasPrecision(10, 2);
		builder.Property(x => x.Comments).HasMaxLength(1024);

		builder.HasIndex(x => x.EvaluationId);
		builder.HasIndex(x => new { x.EvaluationId, x.CriterionId }).IsUnique();
	}
}
