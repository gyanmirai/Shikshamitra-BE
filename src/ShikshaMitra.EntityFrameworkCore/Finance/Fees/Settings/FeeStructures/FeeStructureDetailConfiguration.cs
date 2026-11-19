using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Fees.Settings.FeeStructures;

public class FeeStructureDetailConfiguration : IEntityTypeConfiguration<FeeStructureDetail>
{
	public void Configure(EntityTypeBuilder<FeeStructureDetail> builder)
	{
		builder.ToTable("Finance_FeeStructureDetails", ShikshaMitraConsts.DbSchema, table =>
            table.HasCheckConstraint("CK_FeeStructureDetail_Pricing", "(\"IsVariable\" AND \"Amount\" IS NULL) OR (NOT \"IsVariable\" AND \"Amount\" IS NOT NULL AND \"Amount\" >= 0 AND \"Amount\" <= 9999999999.99)"));
		builder.ConfigureByConvention();
		builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.IsVariable).HasDefaultValue(false);

		// add navigation for fee head
		builder.HasOne(x => x.FeeHead)
			.WithMany()
			.HasForeignKey(x => x.FeeHeadId)
			.OnDelete(DeleteBehavior.Restrict);
		builder.Navigation(x => x.FeeHead).AutoInclude();

		// Payment Term attachment fields
		builder.Property(x => x.ScheduleMode).HasConversion<int>();
		builder.Property(x => x.PaymentTermId);
		builder.Property(x => x.PaymentTermVersionId);

		// Schedule overrides navigation (child entity)
		builder.HasMany(x => x.ScheduleOverrides)
			.WithOne()
			.HasForeignKey(x => x.FeeStructureDetailId)
			.OnDelete(DeleteBehavior.Cascade);
		builder.Navigation(x => x.ScheduleOverrides).AutoInclude();
	}
}
