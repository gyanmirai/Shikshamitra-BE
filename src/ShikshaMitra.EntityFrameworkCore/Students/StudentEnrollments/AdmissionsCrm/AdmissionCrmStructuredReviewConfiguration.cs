using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmReviewConfigurationConfiguration : IEntityTypeConfiguration<AdmissionCrmReviewConfiguration>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmReviewConfiguration> builder)
	{
		builder.ToTable("AppAdmissionCrmReviewConfigurations");
		builder.ConfigureByConvention();
		builder.Property(x => x.Title).IsRequired().HasMaxLength(AdmissionCrmConsts.MaxDescriptionLength);
		builder.HasMany(x => x.Criteria).WithOne().HasForeignKey(x => x.ConfigurationId).OnDelete(DeleteBehavior.Cascade);
		builder.Navigation(x => x.Criteria).HasField("_criteria").UsePropertyAccessMode(PropertyAccessMode.Field);
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId }).IsUnique();
	}
}

public class AdmissionCrmReviewConfigurationCriterionConfiguration : IEntityTypeConfiguration<AdmissionCrmReviewConfigurationCriterion>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmReviewConfigurationCriterion> builder)
	{
		builder.ToTable("AppAdmissionCrmReviewConfigurationCriteria");
		builder.ConfigureByConvention();
		builder.Property(x => x.Key).IsRequired().HasMaxLength(64);
		builder.Property(x => x.Label).IsRequired().HasMaxLength(AdmissionCrmConsts.MaxDescriptionLength);
		builder.HasIndex(x => new { x.TenantId, x.ConfigurationId, x.Key }).IsUnique();
	}
}

public class AdmissionCrmStructuredReviewConfiguration : IEntityTypeConfiguration<AdmissionCrmStructuredReview>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmStructuredReview> builder)
	{
		builder.ToTable("AppAdmissionCrmStructuredReviews");
		builder.ConfigureByConvention();
		builder.Property(x => x.Title).IsRequired().HasMaxLength(AdmissionCrmConsts.MaxDescriptionLength);
		builder.Property(x => x.Status).HasConversion<int>().IsRequired();
		builder.Property(x => x.ClosureReason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.Ignore(x => x.SubmittedReviewCount);
		builder.HasMany(x => x.Criteria).WithOne().HasForeignKey(x => x.ReviewId).OnDelete(DeleteBehavior.Cascade);
		builder.Navigation(x => x.Criteria).HasField("_criteria").UsePropertyAccessMode(PropertyAccessMode.Field);
		builder.HasMany(x => x.Assignments).WithOne().HasForeignKey(x => x.ReviewId).OnDelete(DeleteBehavior.Cascade);
		builder.Navigation(x => x.Assignments).HasField("_assignments").UsePropertyAccessMode(PropertyAccessMode.Field);
		builder.HasIndex(x => new { x.TenantId, x.LeadId }).IsUnique();
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Status });
	}
}

public class AdmissionCrmStructuredReviewCriterionConfiguration : IEntityTypeConfiguration<AdmissionCrmStructuredReviewCriterion>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmStructuredReviewCriterion> builder)
	{
		builder.ToTable("AppAdmissionCrmStructuredReviewCriteria");
		builder.ConfigureByConvention();
		builder.Property(x => x.Key).IsRequired().HasMaxLength(64);
		builder.Property(x => x.Label).IsRequired().HasMaxLength(AdmissionCrmConsts.MaxDescriptionLength);
		builder.HasIndex(x => new { x.TenantId, x.ReviewId, x.Key }).IsUnique();
	}
}

public class AdmissionCrmReviewAssignmentConfiguration : IEntityTypeConfiguration<AdmissionCrmReviewAssignment>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmReviewAssignment> builder)
	{
		builder.ToTable("AppAdmissionCrmReviewAssignments");
		builder.ConfigureByConvention();
		builder.Property(x => x.Status).HasConversion<int>().IsRequired();
		builder.Property(x => x.Recommendation).HasConversion<int?>();
		builder.Property(x => x.ConflictReason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.Property(x => x.ConfidentialRecommendation).HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.Property(x => x.ScoresJson).HasColumnType("jsonb");
		builder.Property(x => x.CancellationReason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.HasIndex(x => new { x.TenantId, x.ReviewerUserId, x.Status, x.DueAt });
		builder.HasIndex(x => new { x.TenantId, x.ReviewId, x.ReviewerUserId });
	}
}
