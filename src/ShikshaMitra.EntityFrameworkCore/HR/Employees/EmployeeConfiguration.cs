using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.Employees;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.Identity;

namespace ShikshaMitra.Employees;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
	public void Configure(EntityTypeBuilder<Employee> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "Employees", ShikshaMitraConsts.DbSchema);
		// Apply default configurations based on ABP conventions.
		builder.ConfigureByConvention();

		// Configure value objects as complex properties
		builder.ComplexProperty(e => e.FatherDetails);
		builder.ComplexProperty(e => e.MotherDetails);
		builder.ComplexProperty(e => e.SpouseDetails);
		builder.ComplexProperty(e => e.BankDetails);
		// Note: AdditionalDetails is now a separate entity with its own table (see EmployeeAdditionalDetailsConfiguration)

		// Configure owned collections (stored as JSON)
		builder.OwnsMany(e => e.WorkExperiences, ew => ew.ToJson());
		builder.OwnsMany(e => e.Documents, doc =>
		{
			doc.ToTable(ShikshaMitraConsts.DbTablePrefix + "EmployeeDocuments", ShikshaMitraConsts.DbSchema);
			doc.WithOwner().HasForeignKey("EmployeeId");
			doc.Property<Guid>("Id");
			doc.HasKey("Id");
		});

		// Configure owned value objects (stored in same table)
		builder.OwnsOne(e => e.Country, cb =>
		{
			cb.Property(c => c.Name).HasColumnName("CountryName").HasMaxLength(128);
			cb.Property(c => c.IsoCode).HasColumnName("CountryIsoCode").HasMaxLength(3);
		});

		builder.OwnsOne(e => e.State, sb =>
		{
			sb.Property(s => s.Name).HasColumnName("StateName").HasMaxLength(128);
			sb.Property(s => s.IsoCode).HasColumnName("StateIsoCode").HasMaxLength(3);
		});

		// Configure navigation properties with foreign keys
		builder.HasOne<IdentityUser>()
			.WithMany()
			.HasForeignKey(e => e.UserId)
			.OnDelete(DeleteBehavior.Restrict)
			.IsRequired(false);

		builder.HasOne(e => e.Role)
			.WithMany()
			.HasForeignKey(e => e.RoleId)
			.OnDelete(DeleteBehavior.Restrict)
			.IsRequired();

		builder.HasOne(e => e.PayrollStructure)
			.WithMany()
			.HasForeignKey(e => e.PayrollStructureId)
			.OnDelete(DeleteBehavior.Restrict)
			.IsRequired(false);

		builder.HasOne(e => e.Shift)
			.WithMany()
			.HasForeignKey(e => e.ShiftId)
			.OnDelete(DeleteBehavior.Restrict)
			.IsRequired(false);

		// Self-referencing relationship for reporting manager
		builder.HasOne(e => e.ReportingManager)
			.WithMany()
			.HasForeignKey(e => e.ReportingManagerId)
			.OnDelete(DeleteBehavior.Restrict)
			.IsRequired(false);

		// Configure property constraints
		builder.Property(e => e.EmployeeCode)
			.IsRequired()
			.HasMaxLength(50);

		builder.Property(e => e.FirstName)
			.HasMaxLength(256);

		builder.Property(e => e.LastName)
			.HasMaxLength(256);

		builder.Property(e => e.Email)
			.HasMaxLength(256);

		builder.Property(e => e.MobileNo)
			.HasMaxLength(20);

		builder.Property(e => e.ProfilePictureBlobName)
			.HasMaxLength(512);

		// Configure indexes for frequently queried fields
		builder.HasIndex(e => e.UserId)
			.HasDatabaseName("IX_Employees_UserId");

		builder.HasIndex(e => e.EmployeeCode)
			.IsUnique()
			.HasDatabaseName("IX_Employees_EmployeeCode_Unique");

		builder.HasIndex(e => e.Email)
			.HasDatabaseName("IX_Employees_Email");

		builder.HasIndex(e => e.RoleId)
			.HasDatabaseName("IX_Employees_RoleId");

		builder.HasIndex(e => new { e.TenantId, e.EmployeeStatusType })
			.HasDatabaseName("IX_Employees_TenantId_Status");

		builder.HasIndex(e => e.ReportingManagerId)
			.HasDatabaseName("IX_Employees_ReportingManagerId");

		// Configure eager loading strategy for navigation properties
		// AutoInclude ensures these are always loaded to prevent N+1 queries
		builder.Navigation(e => e.Role).AutoInclude();
		builder.Navigation(e => e.Shift).AutoInclude();
		// Note: PayrollStructure is optional and might be large, so we don't auto-include it
		// It can be included explicitly when needed using .Include() in queries
		// Note: ReportingManager is not auto-included to avoid deep loading chains
	}
}