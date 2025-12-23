using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.Employees;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Employees;

/// <summary>
/// Entity Framework Core configuration for EmployeeAdditionalDetails entity.
/// </summary>
public class EmployeeAdditionalDetailsConfiguration : IEntityTypeConfiguration<EmployeeAdditionalDetails>
{
	public void Configure(EntityTypeBuilder<EmployeeAdditionalDetails> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "EmployeeAdditionalDetails", ShikshaMitraConsts.DbSchema);

		// Apply default ABP conventions (audit fields, etc.)
		builder.ConfigureByConvention();

		// Configure primary key
		builder.HasKey(e => e.Id);

		// Configure relationship with Employee (1:1)
		builder.HasOne(e => e.Employee)
			.WithOne(emp => emp.AdditionalDetails)
			.HasForeignKey<EmployeeAdditionalDetails>(e => e.EmployeeId)
			.OnDelete(DeleteBehavior.Cascade)
			.IsRequired();

		// Configure properties
		builder.Property(e => e.BplCardNo)
			.HasMaxLength(50)
			.IsRequired(false);

		builder.Property(e => e.PersonWithDisability)
			.IsRequired();

		builder.Property(e => e.SecondLanguage)
			.HasMaxLength(50)
			.IsRequired(false);

		builder.Property(e => e.LibraryMember)
			.IsRequired();

		builder.Property(e => e.TransportMember)
			.IsRequired();

		// Configure unique index on EmployeeId (ensures 1:1 relationship)
		builder.HasIndex(e => e.EmployeeId)
			.IsUnique()
			.HasDatabaseName("IX_EmployeeAdditionalDetails_EmployeeId_Unique");

		// Configure tenant index
		builder.HasIndex(e => e.TenantId)
			.HasDatabaseName("IX_EmployeeAdditionalDetails_TenantId");
	}
}



