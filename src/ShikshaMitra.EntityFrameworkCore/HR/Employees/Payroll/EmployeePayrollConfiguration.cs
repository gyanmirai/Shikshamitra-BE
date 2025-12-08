using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.Employees.Payroll;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.HR.Employees.Payroll;

public class EmployeePayrollDetailsConfiguration : IEntityTypeConfiguration<EmployeePayrollDetails>
{
	public void Configure(EntityTypeBuilder<EmployeePayrollDetails> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "EmployeePayrollDetails", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.HasKey(x => x.Id);

		// Foreign key to Employee
		builder.HasOne(x => x.Employee)
			.WithOne()
			.HasForeignKey<EmployeePayrollDetails>(x => x.EmployeeId)
			.OnDelete(DeleteBehavior.Cascade);

		// Index for fast lookup by EmployeeId
		builder.HasIndex(x => x.EmployeeId).IsUnique();

		// Column configurations
		builder.Property(x => x.BankName).HasMaxLength(200);
		builder.Property(x => x.BranchName).HasMaxLength(200);
		builder.Property(x => x.AccountHolderName).HasMaxLength(200);
		builder.Property(x => x.AccountNumber).HasMaxLength(50);
		builder.Property(x => x.IfscCode).HasMaxLength(11);
		builder.Property(x => x.PfRule).HasMaxLength(100);
		builder.Property(x => x.EpfNumber).HasMaxLength(50);
		builder.Property(x => x.UanNumber).HasMaxLength(50);
		builder.Property(x => x.PunchId).HasMaxLength(50);

		builder.Property(x => x.BasicSalary).HasColumnType("decimal(18,2)");
		builder.Property(x => x.TdsPercentage).HasColumnType("decimal(5,2)");
		builder.Property(x => x.TdsAmount).HasColumnType("decimal(18,2)");
	}
}

public class EmployeePayrollComponentConfiguration : IEntityTypeConfiguration<EmployeePayrollComponent>
{
	public void Configure(EntityTypeBuilder<EmployeePayrollComponent> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "EmployeePayrollComponents", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.HasKey(x => x.Id);

		// Foreign key to Employee
		builder.HasOne(x => x.Employee)
			.WithMany()
			.HasForeignKey(x => x.EmployeeId)
			.OnDelete(DeleteBehavior.Cascade);

		// Indexes for fast queries
		builder.HasIndex(x => x.EmployeeId);
		builder.HasIndex(x => new { x.EmployeeId, x.IsEarning });

		// Column configurations
		builder.Property(x => x.ComponentName).IsRequired().HasMaxLength(200);
		builder.Property(x => x.Type).IsRequired();
		builder.Property(x => x.Amount).HasColumnType("decimal(18,2)");
		builder.Property(x => x.IsEarning).IsRequired();
	}
}
