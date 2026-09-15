using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Domain.Finance.Policies;
using Volo.Abp.EntityFrameworkCore.Modeling;

public class StudentFeeStructureConfiguration : IEntityTypeConfiguration<StudentFeeStructure>
{
	public void Configure(EntityTypeBuilder<StudentFeeStructure> builder)
	{
		builder.ToTable("Finance_StudentFeeStructures", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.HasIndex(x => x.StudentId)
			.HasDatabaseName("IX_StudentFeeStructures_StudentId");
		builder.HasIndex(x => new { x.TenantId, x.StudentId, x.AcademicYearId })
			.IsUnique().HasFilter("\"IsDeleted\" = false")
			.HasDatabaseName("IX_StudentFeeStructures_Tenant_Student_Year");
		builder.Property(x => x.AcademicYearId).IsRequired();
		builder.Property(x => x.ConcessionPolicyId).IsRequired(false);
		builder.Property(x => x.ConcessionPolicyVersionId).IsRequired(false);
		builder.Property(x => x.AssignmentSnapshotJson).HasColumnType("text");
		builder.Property(x => x.ConcessionApplicableFeeHeadIdsJson).HasColumnType("text").IsRequired();
		builder.HasIndex(x => x.ConcessionPolicyId)
			.HasDatabaseName("IX_StudentFeeStructures_ConcessionPolicyId");
		builder.HasIndex(x => x.CollectionPolicyId)
			.HasDatabaseName("IX_StudentFeeStructures_CollectionPolicyId");
		builder.HasIndex(x => x.CollectionPolicyVersionId)
			.HasDatabaseName("IX_StudentFeeStructures_CollectionPolicyVersionId");
		builder.HasIndex(x => x.RefundPolicyId)
			.HasDatabaseName("IX_StudentFeeStructures_RefundPolicyId");
		builder.HasIndex(x => x.RefundPolicyVersionId)
			.HasDatabaseName("IX_StudentFeeStructures_RefundPolicyVersionId");
		builder.HasIndex(x => x.ProrationPolicyId)
			.HasDatabaseName("IX_StudentFeeStructures_ProrationPolicyId");
		builder.HasIndex(x => x.ProrationPolicyVersionId)
			.HasDatabaseName("IX_StudentFeeStructures_ProrationPolicyVersionId");
		builder.Property(x => x.ChargePeriodSource).HasConversion<int>();
		builder.Property(x => x.ChargePeriodOverrideReason).HasMaxLength(500);
		builder.Property(x => x.AssignmentRequestFingerprint).HasMaxLength(64);
		builder.HasOne<CollectionPolicy>()
			.WithMany()
			.HasForeignKey(x => x.CollectionPolicyId)
			.OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<FinancePolicyVersion>()
			.WithMany()
			.HasForeignKey(x => x.CollectionPolicyVersionId)
			.OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<RefundPolicy>()
			.WithMany()
			.HasForeignKey(x => x.RefundPolicyId)
			.OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<FinancePolicyVersion>()
			.WithMany()
			.HasForeignKey(x => x.RefundPolicyVersionId)
			.OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<ProrationPolicy>()
			.WithMany()
			.HasForeignKey(x => x.ProrationPolicyId)
			.OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<FinancePolicyVersion>()
			.WithMany()
			.HasForeignKey(x => x.ProrationPolicyVersionId)
			.OnDelete(DeleteBehavior.Restrict);
	}
}
