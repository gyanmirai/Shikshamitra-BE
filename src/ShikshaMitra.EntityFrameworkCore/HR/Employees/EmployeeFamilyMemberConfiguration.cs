using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.Employees;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Employees;

/// <summary>
/// EF Core configuration for EmployeeFamilyMember entity
/// </summary>
public class EmployeeFamilyMemberConfiguration : IEntityTypeConfiguration<EmployeeFamilyMember>
{
	public void Configure(EntityTypeBuilder<EmployeeFamilyMember> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "EmployeeFamilyMembers", ShikshaMitraConsts.DbSchema);

		builder.ConfigureByConvention();

		// Configure foreign key to Employee explicitly to avoid shadow properties
		builder.HasOne(fm => fm.Employee)
			.WithMany(e => e.FamilyMembers)
			.HasForeignKey(fm => fm.EmployeeId)
			.OnDelete(DeleteBehavior.Cascade) // When employee is deleted, family members are deleted
			.IsRequired();

		// Configure property constraints
		builder.Property(fm => fm.RelationType)
			.IsRequired();

		builder.Property(fm => fm.Name)
			.HasMaxLength(256);

		builder.Property(fm => fm.Occupation)
			.HasMaxLength(256);

		builder.Property(fm => fm.ContactNo)
			.HasMaxLength(20);

		builder.Property(fm => fm.Address1)
			.HasMaxLength(512);

		builder.Property(fm => fm.Address2)
			.HasMaxLength(512);

		builder.Property(fm => fm.Address3)
			.HasMaxLength(512);

		builder.Property(fm => fm.City)
			.HasMaxLength(128);

		builder.Property(fm => fm.StateName)
			.HasMaxLength(128);

		builder.Property(fm => fm.StateIsoCode)
			.HasMaxLength(3);

		builder.Property(fm => fm.CountryName)
			.HasMaxLength(128);

		builder.Property(fm => fm.CountryIsoCode)
			.HasMaxLength(3);

		builder.Property(fm => fm.PinCode)
			.HasMaxLength(10);

		// Create composite unique index to ensure one record per employee per relation type
		builder.HasIndex(fm => new { fm.EmployeeId, fm.RelationType })
			.IsUnique()
			.HasDatabaseName("IX_EmployeeFamilyMembers_EmployeeId_RelationType_Unique");

		// Index for querying by employee
		builder.HasIndex(fm => fm.EmployeeId)
			.HasDatabaseName("IX_EmployeeFamilyMembers_EmployeeId");
	}
}
