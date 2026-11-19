using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.Employees.Onboarding;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Employees.Onboarding;

public class EmployeeApplicationConfiguration : IEntityTypeConfiguration<EmployeeApplication>
{
	public void Configure(EntityTypeBuilder<EmployeeApplication> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "EmployeeApplications", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Ignore(x => x.WorkAssignmentDisplay);

		builder.Property(x => x.ApplicationNumber).IsRequired().HasMaxLength(40);
		builder.Property(x => x.TokenHash).IsRequired().HasMaxLength(128);
		builder.Property(x => x.EncryptedToken).HasMaxLength(1024);
		builder.Property(x => x.FirstName).HasMaxLength(256);
		builder.Property(x => x.MiddleName).HasMaxLength(256);
		builder.Property(x => x.LastName).HasMaxLength(256);
		builder.Property(x => x.Email).HasMaxLength(256);
		builder.Property(x => x.MobileNo).HasMaxLength(20);
		builder.Property(x => x.SecondaryMobileNo).HasMaxLength(20);
		builder.Property(x => x.PositionAppliedFor).HasMaxLength(256);
		builder.Property(x => x.DepartmentAppliedFor).HasMaxLength(256);
		builder.Property(x => x.WorkAssignmentMode).HasMaxLength(32);
		builder.Property(x => x.WorkAssignmentAcademicYearName).HasMaxLength(256);
		builder.Property(x => x.WorkAssignmentOrganizationIds).IsRequired();
		builder.Property(x => x.WorkAssignmentOrganizationLabels).IsRequired();
		builder.Property(x => x.Religion).HasMaxLength(128);
		builder.Property(x => x.FamilyCaste).HasMaxLength(128);
		builder.Property(x => x.QualificationSummary).HasMaxLength(2048);
		builder.Property(x => x.SkillsSummary).HasMaxLength(2048);
		builder.Property(x => x.Address1).HasMaxLength(512);
		builder.Property(x => x.Address2).HasMaxLength(512);
		builder.Property(x => x.Address3).HasMaxLength(512);
		builder.Property(x => x.City).HasMaxLength(128);
		builder.Property(x => x.PinCode).HasMaxLength(20);
		builder.Property(x => x.ResumeBlobName).HasMaxLength(512);
		builder.Property(x => x.ResumeFileName).HasMaxLength(256);
		builder.Property(x => x.ResumeContentType).HasMaxLength(128);
		builder.Property(x => x.PhotoBlobName).HasMaxLength(512);
		builder.Property(x => x.PhotoFileName).HasMaxLength(256);
		builder.Property(x => x.PhotoContentType).HasMaxLength(128);
		builder.Property(x => x.StatusReason).HasMaxLength(1024);
		builder.Property(x => x.InternalNotes).HasMaxLength(2048);

		builder.OwnsOne(x => x.PersonalInfo, personal =>
		{
			personal.ToJson();
			personal.OwnsOne(x => x.Country);
			personal.OwnsOne(x => x.State);
		});

		builder.OwnsOne(x => x.FamilyInfo, family =>
		{
			family.ToJson();
			family.OwnsOne(x => x.FatherDetails, member =>
			{
				member.OwnsOne(x => x.Country);
				member.OwnsOne(x => x.State);
			});
			family.OwnsOne(x => x.MotherDetails, member =>
			{
				member.OwnsOne(x => x.Country);
				member.OwnsOne(x => x.State);
			});
			family.OwnsOne(x => x.SpouseDetails, member =>
			{
				member.OwnsOne(x => x.Country);
				member.OwnsOne(x => x.State);
			});
		});

		builder.OwnsOne(x => x.AdditionalInfo, additional => additional.ToJson());

		builder.OwnsMany(x => x.WorkExperiences, ew => ew.ToJson());

		builder.OwnsOne(x => x.Country, cb =>
		{
			cb.Property(c => c.Name).HasColumnName("CountryName").HasMaxLength(128);
			cb.Property(c => c.IsoCode).HasColumnName("CountryIsoCode").HasMaxLength(3);
		});

		builder.OwnsOne(x => x.State, sb =>
		{
			sb.Property(s => s.Name).HasColumnName("StateName").HasMaxLength(128);
			sb.Property(s => s.IsoCode).HasColumnName("StateIsoCode").HasMaxLength(3);
		});

		builder.HasIndex(x => x.TokenHash)
			.IsUnique()
			.HasDatabaseName("IX_EmployeeApplications_TokenHash");

		builder.HasIndex(x => new { x.TenantId, x.ApplicationNumber })
			.IsUnique()
			.HasDatabaseName("IX_EmployeeApplications_TenantId_ApplicationNumber");

		builder.HasIndex(x => new { x.TenantId, x.Status })
			.HasDatabaseName("IX_EmployeeApplications_TenantId_Status");

		builder.HasIndex(x => new { x.TenantId, x.Email })
			.HasDatabaseName("IX_EmployeeApplications_TenantId_Email");
	}
}
