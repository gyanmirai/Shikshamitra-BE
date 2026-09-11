using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Policies;
using Volo.Abp.EntityFrameworkCore.Modeling;

public class FeeStructureConfiguration : IEntityTypeConfiguration<FeeStructure>
{
	public void Configure(EntityTypeBuilder<FeeStructure> builder)
	{
		builder.ToTable("Finance_FeeStructures", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.RevisionNumber).HasDefaultValue(1);
		builder.Property(x => x.NormalizedName).HasMaxLength(256).IsRequired();
		builder.HasIndex(x => new { x.StandardId, x.NormalizedName }).IsUnique()
			.HasFilter("\"PreviousRevisionId\" IS NULL AND NOT \"IsDeleted\"");
		builder.HasIndex(x => new { x.RevisionRootId, x.RevisionNumber }).IsUnique();
		builder.HasOne<FeeStructure>().WithMany().HasForeignKey(x => x.PreviousRevisionId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Finance_FeeStructures_PreviousRevisionId");
		builder.HasOne<ReceiptBook>().WithMany().HasForeignKey(x => x.ReceiptBookId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Finance_FeeStructures_ReceiptBookId");

		// FeeStructureDetail is a child entity of FeeStructure aggregate root.
		// Cascade delete ensures details are removed when the parent structure is deleted.
		builder.HasMany(x => x.FeeStructureDetails)
			.WithOne()
			.HasForeignKey(x => x.FeeStructureId)
			.OnDelete(DeleteBehavior.Cascade);
		builder.Navigation(x => x.FeeStructureDetails).AutoInclude();
		builder.HasIndex(x => x.ConcessionPolicyId)
			.HasDatabaseName("IX_Finance_FeeStructures_ConcessionPolicyId");
		builder.HasOne<RefundPolicy>()
			.WithMany()
			.HasForeignKey(x => x.RefundPolicyId)
			.OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<FinancePolicyVersion>()
			.WithMany()
			.HasForeignKey(x => x.RefundPolicyVersionId)
			.OnDelete(DeleteBehavior.Restrict);
		builder.HasIndex(x => x.RefundPolicyId)
			.HasDatabaseName("IX_Finance_FeeStructures_RefundPolicyId");
		builder.HasIndex(x => x.RefundPolicyVersionId)
			.HasDatabaseName("IX_Finance_FeeStructures_RefundPolicyVersionId");
		builder.HasOne<ProrationPolicy>()
			.WithMany()
			.HasForeignKey(x => x.ProrationPolicyId)
			.OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<FinancePolicyVersion>()
			.WithMany()
			.HasForeignKey(x => x.ProrationPolicyVersionId)
			.OnDelete(DeleteBehavior.Restrict);
		builder.HasIndex(x => x.ProrationPolicyId)
			.HasDatabaseName("IX_Finance_FeeStructures_ProrationPolicyId");
		builder.HasIndex(x => x.ProrationPolicyVersionId)
			.HasDatabaseName("IX_Finance_FeeStructures_ProrationPolicyVersionId");
		builder.Property(x => x.ConcessionApplicableFeeHeadIdsJson).HasColumnType("text").IsRequired();

		builder.HasOne<CollectionPolicy>()
			.WithMany()
			.HasForeignKey(x => x.CollectionPolicyId)
			.OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<FinancePolicyVersion>()
			.WithMany()
			.HasForeignKey(x => x.CollectionPolicyVersionId)
			.OnDelete(DeleteBehavior.Restrict);
		builder.HasIndex(x => x.CollectionPolicyId)
			.HasDatabaseName("IX_Finance_FeeStructures_CollectionPolicyId");
		builder.HasIndex(x => x.CollectionPolicyVersionId)
			.HasDatabaseName("IX_Finance_FeeStructures_CollectionPolicyVersionId");
	}
}
