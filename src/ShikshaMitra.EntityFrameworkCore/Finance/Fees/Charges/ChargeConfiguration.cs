using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Fees.Charges;

public class ChargeConfiguration : IEntityTypeConfiguration<Charge>
{
	public void Configure(EntityTypeBuilder<Charge> builder)
	{
		builder.ToTable("Charges", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		// Configure properties
		builder.Property(x => x.PurposeDescription)
			.IsRequired()
			.HasMaxLength(1000);

		builder.Property(x => x.Amount)
			.HasColumnType("decimal(18,2)")
			.IsRequired();

		builder.Property(x => x.AdditionalNotes)
			.HasMaxLength(2000);

		builder.Property(x => x.ApprovalNotes)
			.HasMaxLength(2000);

		builder.Property(x => x.DueDate)
			.IsRequired();

		builder.Property(x => x.AppliedOn)
			.IsRequired();

		builder.Property(x => x.Priority)
			.IsRequired();

		builder.Property(x => x.Status)
			.IsRequired();

		// Configure indexes for better query performance
		builder.HasIndex(x => new { x.AcademicYearId, x.Status })
			.HasDatabaseName("IX_Charges_AcademicYearId_Status");

		builder.HasIndex(x => x.StudentId)
			.HasDatabaseName("IX_Charges_StudentId");

		builder.HasIndex(x => x.ChargeCategoryId)
			.HasDatabaseName("IX_Charges_ChargeCategoryId");

		builder.HasIndex(x => x.DueDate)
			.HasDatabaseName("IX_Charges_DueDate");
	}
}
