using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.ProfessionalManagement;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.HR.ProfessionalManagement;

public class PerformanceTemplateConfiguration : IEntityTypeConfiguration<PerformanceTemplate>
{
	public void Configure(EntityTypeBuilder<PerformanceTemplate> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "PerformanceTemplates", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Name).IsRequired().HasMaxLength(160);
		builder.Property(x => x.RoleName).HasMaxLength(120);
		builder.Property(x => x.Description).HasMaxLength(1000);
		builder.HasIndex(x => new { x.TenantId, x.StaffCategory, x.Status });
	}
}

public class PerformanceTemplateSectionConfiguration : IEntityTypeConfiguration<PerformanceTemplateSection>
{
	public void Configure(EntityTypeBuilder<PerformanceTemplateSection> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "PerformanceTemplateSections", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Name).IsRequired().HasMaxLength(160);
		builder.Property(x => x.Weight).HasPrecision(7, 2);
		builder.HasIndex(x => new { x.TenantId, x.TemplateId, x.DisplayOrder });
	}
}

public class PerformanceTemplateCriterionConfiguration : IEntityTypeConfiguration<PerformanceTemplateCriterion>
{
	public void Configure(EntityTypeBuilder<PerformanceTemplateCriterion> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "PerformanceTemplateCriteria", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
		builder.Property(x => x.Description).HasMaxLength(1000);
		builder.Property(x => x.Weight).HasPrecision(7, 2);
		builder.HasIndex(x => new { x.TenantId, x.TemplateId, x.SectionId, x.DisplayOrder });
	}
}

public class PerformanceReviewAssignmentConfiguration : IEntityTypeConfiguration<PerformanceReviewAssignment>
{
	public void Configure(EntityTypeBuilder<PerformanceReviewAssignment> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "PerformanceReviewAssignments", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);
		builder.Property(x => x.FinalScore).HasPrecision(7, 2);
		builder.Property(x => x.FinalRating).HasMaxLength(80);
		builder.HasIndex(x => new { x.TenantId, x.CycleId, x.EmployeeId }).IsUnique();
		builder.HasIndex(x => new { x.TenantId, x.Status });
	}
}

public class PerformanceReviewStageConfiguration : IEntityTypeConfiguration<PerformanceReviewStage>
{
	public void Configure(EntityTypeBuilder<PerformanceReviewStage> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "PerformanceReviewStages", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);
		builder.Property(x => x.SummaryComment).HasMaxLength(2000);
		builder.HasIndex(x => new { x.TenantId, x.ReviewAssignmentId, x.StageType }).IsUnique();
	}
}

public class PerformanceReviewResponseConfiguration : IEntityTypeConfiguration<PerformanceReviewResponse>
{
	public void Configure(EntityTypeBuilder<PerformanceReviewResponse> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "PerformanceReviewResponses", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Comment).HasMaxLength(2000);
		builder.HasIndex(x => new { x.TenantId, x.StageId, x.CriterionId }).IsUnique();
	}
}

public class ClassroomObservationConfiguration : IEntityTypeConfiguration<ClassroomObservation>
{
	public void Configure(EntityTypeBuilder<ClassroomObservation> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "ClassroomObservations", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);
		builder.Property(x => x.ClassName).HasMaxLength(120);
		builder.Property(x => x.SubjectName).HasMaxLength(120);
		builder.Property(x => x.Notes).HasMaxLength(2000);
		builder.HasIndex(x => new { x.TenantId, x.ReviewAssignmentId, x.ObservationDate });
	}
}

public class PerformanceEvidenceConfiguration : IEntityTypeConfiguration<PerformanceEvidence>
{
	public void Configure(EntityTypeBuilder<PerformanceEvidence> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "PerformanceEvidence", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Title).IsRequired().HasMaxLength(160);
		builder.Property(x => x.FileName).HasMaxLength(260);
		builder.Property(x => x.Url).HasMaxLength(1000);
		builder.Property(x => x.Notes).HasMaxLength(1000);
		builder.HasIndex(x => new { x.TenantId, x.ReviewAssignmentId });
	}
}

public class PerformanceFeedbackConfiguration : IEntityTypeConfiguration<PerformanceFeedback>
{
	public void Configure(EntityTypeBuilder<PerformanceFeedback> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "PerformanceFeedback", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);
		builder.Property(x => x.ProviderName).HasMaxLength(160);
		builder.Property(x => x.FeedbackText).IsRequired().HasMaxLength(2000);
		builder.HasIndex(x => new { x.TenantId, x.ReviewAssignmentId, x.Source });
	}
}

public class PerformanceOutcomeConfiguration : IEntityTypeConfiguration<PerformanceOutcome>
{
	public void Configure(EntityTypeBuilder<PerformanceOutcome> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "PerformanceOutcomes", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);
		builder.Property(x => x.FinalScore).HasPrecision(7, 2);
		builder.Property(x => x.FinalRating).IsRequired().HasMaxLength(80);
		builder.Property(x => x.Strengths).HasMaxLength(2000);
		builder.Property(x => x.ImprovementAreas).HasMaxLength(2000);
		builder.Property(x => x.RecommendationNotes).HasMaxLength(2000);
		builder.HasIndex(x => new { x.TenantId, x.ReviewAssignmentId }).IsUnique();
	}
}

public class DevelopmentPlanConfiguration : IEntityTypeConfiguration<DevelopmentPlan>
{
	public void Configure(EntityTypeBuilder<DevelopmentPlan> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "DevelopmentPlans", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Goal).IsRequired().HasMaxLength(240);
		builder.Property(x => x.ActionPlan).HasMaxLength(2000);
		builder.HasIndex(x => new { x.TenantId, x.ReviewAssignmentId, x.Status });
	}
}

public class PerformanceAuditEventConfiguration : IEntityTypeConfiguration<PerformanceAuditEvent>
{
	public void Configure(EntityTypeBuilder<PerformanceAuditEvent> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "PerformanceAuditEvents", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);
		builder.Property(x => x.EventType).IsRequired().HasMaxLength(120);
		builder.Property(x => x.Summary).IsRequired().HasMaxLength(500);
		builder.Property(x => x.MetadataJson).HasMaxLength(4000);
		builder.HasIndex(x => new { x.TenantId, x.ReviewAssignmentId, x.CreationTime });
	}
}
