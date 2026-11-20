using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Fees.Charges;

public class ChargeConfiguration : IEntityTypeConfiguration<Charge>
{
	public void Configure(EntityTypeBuilder<Charge> builder)
	{
		builder.ToTable("Finance_Charges", ShikshaMitraConsts.DbSchema);
        builder.Property(x=>x.IncomeCategoryName).HasMaxLength(500);
		builder.ConfigureByConvention();

		// Configure properties
		builder.Property(x => x.ChargeName)
			.IsRequired()
			.HasMaxLength(ChargeRequest.MaxChargeNameLength);

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

		builder.Property(x => x.DueDate).IsRequired(false);
		builder.HasIndex(x => x.EmployeeId);
		builder.HasOne<ShikshaMitra.HR.Employees.Employee>().WithMany()
			.HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
		builder.ToTable(table => table.HasCheckConstraint("CK_FinanceCharges_Payer",
			"(\"StudentId\" IS NOT NULL AND \"EmployeeId\" IS NULL) OR (\"StudentId\" IS NULL AND \"EmployeeId\" IS NOT NULL)"));

		builder.Property(x => x.AppliedOn)
			.IsRequired();

		builder.Property(x => x.Priority)
			.IsRequired();

		builder.Property(x => x.Status)
			.IsRequired();

		// Configure indexes for better query performance
		builder.HasIndex(x => x.ChargeRequestId)
			.HasDatabaseName("IX_Charges_ChargeRequestId");

		builder.HasIndex(x => new { x.AcademicYearId, x.Status })
			.HasDatabaseName("IX_Charges_AcademicYearId_Status");

		builder.HasIndex(x => x.StudentId)
			.HasDatabaseName("IX_Charges_StudentId");

		builder.HasIndex(x => x.ChargeCategoryId)
			.HasDatabaseName("IX_Charges_ChargeCategoryId");

		builder.HasIndex(x => x.DueDate)
			.HasDatabaseName("IX_Charges_DueDate");

		// Maker-checker inbox: find charges created by a user awaiting their own review
		builder.HasIndex(x => new { x.CreatorId, x.Status })
			.HasDatabaseName("IX_Charges_CreatorId_Status");
	}
}
