using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Fees.Settings.PaymentTerms;

public class PaymentTermConfiguration : IEntityTypeConfiguration<PaymentTerm>
{
	public void Configure(EntityTypeBuilder<PaymentTerm> builder)
	{
		builder.ToTable("Finance_PaymentTerms", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
		builder.Property(x => x.Code).IsRequired().HasMaxLength(24);
		builder.Property(x => x.Description).HasMaxLength(500);
		builder.Property(x => x.ArchiveReason).HasMaxLength(500);

		builder.Property(x => x.Status).HasConversion<int>();
		builder.Property(x => x.HolidayShiftingMethod).HasConversion<int>();
		builder.Property(x => x.DemandGenerationRule).HasConversion<int>();
		builder.Property(x => x.InitialDemandStatus).HasConversion<int>();

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Code }).IsUnique();
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.BoardId })
			.IsUnique()
			.HasFilter("\"IsDeleted\" = false AND \"IsDefault\" = true")
			.HasDatabaseName("IX_Finance_PaymentTerms_OneDefaultPerBoard");

		builder.HasMany(x => x.Versions)
			.WithOne()
			.HasForeignKey(x => x.PaymentTermId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.Navigation(x => x.Versions).AutoInclude();
	}
}
