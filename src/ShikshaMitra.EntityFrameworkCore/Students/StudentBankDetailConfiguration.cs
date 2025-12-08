using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students;

public class StudentBankDetailConfiguration : IEntityTypeConfiguration<StudentBankDetail>
{
	public void Configure(EntityTypeBuilder<StudentBankDetail> builder)
	{
		builder.ToTable("StudentBankDetails");

		builder.ConfigureByConvention();

		// Configure scalar properties
		builder.Property(x => x.BankName).HasMaxLength(200);
		builder.Property(x => x.AccountNo).HasMaxLength(50);
		builder.Property(x => x.IFSCCode).HasMaxLength(20);
		builder.Property(x => x.BranchName).HasMaxLength(200);
		builder.Property(x => x.AccountHolderName).HasMaxLength(200);
		builder.Property(x => x.AccountType).HasMaxLength(50);
		builder.Property(x => x.BankAddress).HasMaxLength(500);
		builder.Property(x => x.MICRCode).HasMaxLength(20);
		builder.Property(x => x.SwiftCode).HasMaxLength(20);

		// Add index on account number for quick lookups
		builder.HasIndex(x => x.AccountNo);
		builder.HasIndex(x => x.IFSCCode);
	}
}
