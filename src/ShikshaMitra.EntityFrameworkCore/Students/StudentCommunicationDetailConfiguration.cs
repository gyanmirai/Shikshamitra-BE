using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students;

public class StudentCommunicationDetailConfiguration : IEntityTypeConfiguration<StudentCommunicationDetail>
{
	public void Configure(EntityTypeBuilder<StudentCommunicationDetail> builder)
	{
		builder.ToTable("StudentCommunicationDetails");

		builder.ConfigureByConvention();

		// Configure Local Address as owned entity (complex type)
		builder.OwnsOne(x => x.LocalAddress, la =>
		{
			la.Property(a => a.Address1).HasMaxLength(100).HasColumnName("LocalAddress_BuildingNo");
			la.Property(a => a.Address2).HasMaxLength(200).HasColumnName("LocalAddress_AreaLocalityRoadName");
			la.Property(a => a.Address3).HasMaxLength(200).HasColumnName("LocalAddress_Landmark");
			la.Property(a => a.City).HasMaxLength(100).HasColumnName("LocalAddress_City").IsRequired();
			la.Property(a => a.Pincode).HasMaxLength(10).HasColumnName("LocalAddress_Pincode");
			
			// Configure State as owned entity
			la.OwnsOne(a => a.State, s =>
			{
				s.Property(st => st.Name).HasMaxLength(100).HasColumnName("LocalAddress_State_Name");
				s.Property(st => st.IsoCode).HasMaxLength(10).HasColumnName("LocalAddress_State_IsoCode");
			});
			
			// Configure Country as owned entity
			la.OwnsOne(a => a.Country, c =>
			{
				c.Property(ct => ct.Name).HasMaxLength(100).HasColumnName("LocalAddress_Country_Name");
				c.Property(ct => ct.IsoCode).HasMaxLength(10).HasColumnName("LocalAddress_Country_IsoCode");
			});
			la.Navigation(a => a.State).IsRequired();
		});
		builder.Navigation(x => x.LocalAddress).IsRequired(false);

		// Configure Permanent Address as owned entity (complex type)
		builder.OwnsOne(x => x.PermanentAddress, pa =>
		{
			pa.Property(a => a.Address1).HasMaxLength(100).HasColumnName("PermanentAddress_BuildingNo");
			pa.Property(a => a.Address2).HasMaxLength(200).HasColumnName("PermanentAddress_AreaLocalityRoadName");
			pa.Property(a => a.Address3).HasMaxLength(200).HasColumnName("PermanentAddress_Landmark");
			pa.Property(a => a.City).HasMaxLength(100).HasColumnName("PermanentAddress_City").IsRequired();
			pa.Property(a => a.Pincode).HasMaxLength(10).HasColumnName("PermanentAddress_Pincode");
			
			// Configure State as owned entity
			pa.OwnsOne(a => a.State, s =>
			{
				s.Property(st => st.Name).HasMaxLength(100).HasColumnName("PermanentAddress_State_Name");
				s.Property(st => st.IsoCode).HasMaxLength(10).HasColumnName("PermanentAddress_State_IsoCode");
			});
			
			// Configure Country as owned entity
			pa.OwnsOne(a => a.Country, c =>
			{
				c.Property(ct => ct.Name).HasMaxLength(100).HasColumnName("PermanentAddress_Country_Name");
				c.Property(ct => ct.IsoCode).HasMaxLength(10).HasColumnName("PermanentAddress_Country_IsoCode");
			});
			pa.Navigation(a => a.State).IsRequired();
		});
		builder.Navigation(x => x.PermanentAddress).IsRequired(false);

		// Configure Emergency Address as owned entity (complex type)
		builder.OwnsOne(x => x.EmergencyAddress, ea =>
		{
			ea.Property(a => a.Address1).HasMaxLength(100).HasColumnName("EmergencyAddress_BuildingNo");
			ea.Property(a => a.Address2).HasMaxLength(200).HasColumnName("EmergencyAddress_AreaLocalityRoadName");
			ea.Property(a => a.Address3).HasMaxLength(200).HasColumnName("EmergencyAddress_Landmark");
			ea.Property(a => a.City).HasMaxLength(100).HasColumnName("EmergencyAddress_City").IsRequired();
			ea.Property(a => a.Pincode).HasMaxLength(10).HasColumnName("EmergencyAddress_Pincode");
			
			// Configure State as owned entity
			ea.OwnsOne(a => a.State, s =>
			{
				s.Property(st => st.Name).HasMaxLength(100).HasColumnName("EmergencyAddress_State_Name");
				s.Property(st => st.IsoCode).HasMaxLength(10).HasColumnName("EmergencyAddress_State_IsoCode");
			});
			
			// Configure Country as owned entity
			ea.OwnsOne(a => a.Country, c =>
			{
				c.Property(ct => ct.Name).HasMaxLength(100).HasColumnName("EmergencyAddress_Country_Name");
				c.Property(ct => ct.IsoCode).HasMaxLength(10).HasColumnName("EmergencyAddress_Country_IsoCode");
			});
			ea.Navigation(a => a.State).IsRequired();
		});
		builder.Navigation(x => x.EmergencyAddress).IsRequired(false);

		// Configure scalar properties
		builder.Property(x => x.IsPermanentSameAsLocal).IsRequired();
		builder.Property(x => x.IsEmergencySameAsLocal).IsRequired();
		builder.Property(x => x.EmergencyContactName).HasMaxLength(200);
		builder.Property(x => x.EmergencyContactPhone).HasMaxLength(20);
		builder.Property(x => x.EmergencyContactRelation).HasMaxLength(100);
	}
}
