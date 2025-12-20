using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students;

public class StudentFamilyDetailConfiguration : IEntityTypeConfiguration<StudentFamilyDetail>
{
	public void Configure(EntityTypeBuilder<StudentFamilyDetail> builder)
	{
		builder.ToTable("StudentFamilyDetails");

		builder.ConfigureByConvention();

		// Configure Father Details as owned entity (complex type)
		builder.OwnsOne(x => x.FatherDetails, fd =>
		{
			fd.Property(p => p.FullName).HasMaxLength(200).IsRequired();
			fd.Property(p => p.Occupation).HasMaxLength(100);
			fd.Property(p => p.Email).HasMaxLength(256);
			fd.Property(p => p.ContactNo).HasMaxLength(20);
			fd.Property(p => p.OfficeNo).HasMaxLength(20);
			fd.Property(p => p.Qualification).HasMaxLength(100);
			fd.Property(p => p.YearOfPassing).HasMaxLength(10);
			fd.Property(p => p.Designation).HasMaxLength(100);
			fd.Property(p => p.Organization).HasMaxLength(200);
			fd.Property(p => p.AadharNo).HasMaxLength(20);

			// Configure Office Address
			fd.OwnsOne(p => p.OfficeAddress, oa =>
			{
				oa.Property(a => a.Address1).HasMaxLength(100).HasColumnName("Father_OfficeAddress_BuildingNo");
				oa.Property(a => a.Address2).HasMaxLength(200).HasColumnName("Father_OfficeAddress_AreaLocalityRoadName");
				oa.Property(a => a.Address3).HasMaxLength(200).HasColumnName("Father_OfficeAddress_Landmark");
				oa.Property(a => a.City).HasMaxLength(100).HasColumnName("Father_OfficeAddress_City").IsRequired();
				oa.Property(a => a.Pincode).HasMaxLength(10).HasColumnName("Father_OfficeAddress_Pincode");
				
				// Configure State as owned entity
				oa.OwnsOne(a => a.State, s =>
				{
					s.Property(st => st.Name).HasMaxLength(100).HasColumnName("Father_OfficeAddress_State_Name");
					s.Property(st => st.IsoCode).HasMaxLength(10).HasColumnName("Father_OfficeAddress_State_IsoCode");
				});
				
				// Configure Country as owned entity
				oa.OwnsOne(a => a.Country, c =>
				{
					c.Property(ct => ct.Name).HasMaxLength(100).HasColumnName("Father_OfficeAddress_Country_Name");
					c.Property(ct => ct.IsoCode).HasMaxLength(10).HasColumnName("Father_OfficeAddress_Country_IsoCode");
				});
				oa.Navigation(a => a.State).IsRequired();
			});
			fd.Navigation(p => p.OfficeAddress).IsRequired(false);

			// Configure Local Address
			fd.OwnsOne(p => p.LocalAddress, la =>
			{
				la.Property(a => a.Address1).HasMaxLength(100).HasColumnName("Father_LocalAddress_BuildingNo");
				la.Property(a => a.Address2).HasMaxLength(200).HasColumnName("Father_LocalAddress_AreaLocalityRoadName");
				la.Property(a => a.Address3).HasMaxLength(200).HasColumnName("Father_LocalAddress_Landmark");
				la.Property(a => a.City).HasMaxLength(100).HasColumnName("Father_LocalAddress_City").IsRequired();
				la.Property(a => a.Pincode).HasMaxLength(10).HasColumnName("Father_LocalAddress_Pincode");
				
				// Configure State as owned entity
				la.OwnsOne(a => a.State, s =>
				{
					s.Property(st => st.Name).HasMaxLength(100).HasColumnName("Father_LocalAddress_State_Name");
					s.Property(st => st.IsoCode).HasMaxLength(10).HasColumnName("Father_LocalAddress_State_IsoCode");
				});
				
				// Configure Country as owned entity
				la.OwnsOne(a => a.Country, c =>
				{
					c.Property(ct => ct.Name).HasMaxLength(100).HasColumnName("Father_LocalAddress_Country_Name");
					c.Property(ct => ct.IsoCode).HasMaxLength(10).HasColumnName("Father_LocalAddress_Country_IsoCode");
				});
				la.Navigation(a => a.State).IsRequired();
			});
			fd.Navigation(p => p.LocalAddress).IsRequired(false);
		});
		builder.Navigation(x => x.FatherDetails).IsRequired(false);

		// Configure Mother Details as owned entity (complex type)
		builder.OwnsOne(x => x.MotherDetails, md =>
		{
			md.Property(p => p.FullName).HasMaxLength(200).IsRequired();
			md.Property(p => p.Occupation).HasMaxLength(100);
			md.Property(p => p.Email).HasMaxLength(256);
			md.Property(p => p.ContactNo).HasMaxLength(20);
			md.Property(p => p.OfficeNo).HasMaxLength(20);
			md.Property(p => p.Qualification).HasMaxLength(100);
			md.Property(p => p.YearOfPassing).HasMaxLength(10);
			md.Property(p => p.Designation).HasMaxLength(100);
			md.Property(p => p.Organization).HasMaxLength(200);
			md.Property(p => p.AadharNo).HasMaxLength(20);

			// Configure Office Address
			md.OwnsOne(p => p.OfficeAddress, oa =>
			{
				oa.Property(a => a.Address1).HasMaxLength(100).HasColumnName("Mother_OfficeAddress_BuildingNo");
				oa.Property(a => a.Address2).HasMaxLength(200).HasColumnName("Mother_OfficeAddress_AreaLocalityRoadName");
				oa.Property(a => a.Address3).HasMaxLength(200).HasColumnName("Mother_OfficeAddress_Landmark");
				oa.Property(a => a.City).HasMaxLength(100).HasColumnName("Mother_OfficeAddress_City").IsRequired();
				oa.Property(a => a.Pincode).HasMaxLength(10).HasColumnName("Mother_OfficeAddress_Pincode");
				
				// Configure State as owned entity
				oa.OwnsOne(a => a.State, s =>
				{
					s.Property(st => st.Name).HasMaxLength(100).HasColumnName("Mother_OfficeAddress_State_Name");
					s.Property(st => st.IsoCode).HasMaxLength(10).HasColumnName("Mother_OfficeAddress_State_IsoCode");
				});
				
				// Configure Country as owned entity
				oa.OwnsOne(a => a.Country, c =>
				{
					c.Property(ct => ct.Name).HasMaxLength(100).HasColumnName("Mother_OfficeAddress_Country_Name");
					c.Property(ct => ct.IsoCode).HasMaxLength(10).HasColumnName("Mother_OfficeAddress_Country_IsoCode");
				});
				oa.Navigation(a => a.State).IsRequired();
			});
			md.Navigation(p => p.OfficeAddress).IsRequired(false);

			// Configure Local Address
			md.OwnsOne(p => p.LocalAddress, la =>
			{
				la.Property(a => a.Address1).HasMaxLength(100).HasColumnName("Mother_LocalAddress_BuildingNo");
				la.Property(a => a.Address2).HasMaxLength(200).HasColumnName("Mother_LocalAddress_AreaLocalityRoadName");
				la.Property(a => a.Address3).HasMaxLength(200).HasColumnName("Mother_LocalAddress_Landmark");
				la.Property(a => a.City).HasMaxLength(100).HasColumnName("Mother_LocalAddress_City").IsRequired();
				la.Property(a => a.Pincode).HasMaxLength(10).HasColumnName("Mother_LocalAddress_Pincode");
				
				// Configure State as owned entity
				la.OwnsOne(a => a.State, s =>
				{
					s.Property(st => st.Name).HasMaxLength(100).HasColumnName("Mother_LocalAddress_State_Name");
					s.Property(st => st.IsoCode).HasMaxLength(10).HasColumnName("Mother_LocalAddress_State_IsoCode");
				});
				
				// Configure Country as owned entity
				la.OwnsOne(a => a.Country, c =>
				{
					c.Property(ct => ct.Name).HasMaxLength(100).HasColumnName("Mother_LocalAddress_Country_Name");
					c.Property(ct => ct.IsoCode).HasMaxLength(10).HasColumnName("Mother_LocalAddress_Country_IsoCode");
				});
				la.Navigation(a => a.State).IsRequired();
			});
			md.Navigation(p => p.LocalAddress).IsRequired(false);
		});
		builder.Navigation(x => x.MotherDetails).IsRequired(false);

		// Configure Guardian Details as owned entity (complex type)
		builder.OwnsOne(x => x.GuardianDetails, gd =>
		{
			gd.Property(p => p.FullName).HasMaxLength(200).IsRequired();
			gd.Property(p => p.Occupation).HasMaxLength(100);
			gd.Property(p => p.Email).HasMaxLength(256);
			gd.Property(p => p.ContactNo).HasMaxLength(20);
			gd.Property(p => p.OfficeNo).HasMaxLength(20);
			gd.Property(p => p.Qualification).HasMaxLength(100);
			gd.Property(p => p.YearOfPassing).HasMaxLength(10);
			gd.Property(p => p.Designation).HasMaxLength(100);
			gd.Property(p => p.Organization).HasMaxLength(200);
			gd.Property(p => p.AadharNo).HasMaxLength(20);

			// Configure Office Address
			gd.OwnsOne(p => p.OfficeAddress, oa =>
			{
				oa.Property(a => a.Address1).HasMaxLength(100).HasColumnName("Guardian_OfficeAddress_BuildingNo");
				oa.Property(a => a.Address2).HasMaxLength(200).HasColumnName("Guardian_OfficeAddress_AreaLocalityRoadName");
				oa.Property(a => a.Address3).HasMaxLength(200).HasColumnName("Guardian_OfficeAddress_Landmark");
				oa.Property(a => a.City).HasMaxLength(100).HasColumnName("Guardian_OfficeAddress_City").IsRequired();
				oa.Property(a => a.Pincode).HasMaxLength(10).HasColumnName("Guardian_OfficeAddress_Pincode");
				
				// Configure State as owned entity
				oa.OwnsOne(a => a.State, s =>
				{
					s.Property(st => st.Name).HasMaxLength(100).HasColumnName("Guardian_OfficeAddress_State_Name").IsRequired();
					s.Property(st => st.IsoCode).HasMaxLength(10).HasColumnName("Guardian_OfficeAddress_State_IsoCode").IsRequired();
				});
				
				// Configure Country as owned entity
				oa.OwnsOne(a => a.Country, c =>
				{
					c.Property(ct => ct.Name).HasMaxLength(100).HasColumnName("Guardian_OfficeAddress_Country_Name");
					c.Property(ct => ct.IsoCode).HasMaxLength(10).HasColumnName("Guardian_OfficeAddress_Country_IsoCode");
				});
				oa.Navigation(a => a.State).IsRequired();
			});
			gd.Navigation(p => p.OfficeAddress).IsRequired(false);

			// Configure Local Address
			gd.OwnsOne(p => p.LocalAddress, la =>
			{
				la.Property(a => a.Address1).HasMaxLength(100).HasColumnName("Guardian_LocalAddress_BuildingNo");
				la.Property(a => a.Address2).HasMaxLength(200).HasColumnName("Guardian_LocalAddress_AreaLocalityRoadName");
				la.Property(a => a.Address3).HasMaxLength(200).HasColumnName("Guardian_LocalAddress_Landmark");
				la.Property(a => a.City).HasMaxLength(100).HasColumnName("Guardian_LocalAddress_City").IsRequired();
				la.Property(a => a.Pincode).HasMaxLength(10).HasColumnName("Guardian_LocalAddress_Pincode");
				
				// Configure State as owned entity
				la.OwnsOne(a => a.State, s =>
				{
					s.Property(st => st.Name).HasMaxLength(100).HasColumnName("Guardian_LocalAddress_State_Name");
					s.Property(st => st.IsoCode).HasMaxLength(10).HasColumnName("Guardian_LocalAddress_State_IsoCode");
				});
				
				// Configure Country as owned entity
				la.OwnsOne(a => a.Country, c =>
				{
					c.Property(ct => ct.Name).HasMaxLength(100).HasColumnName("Guardian_LocalAddress_Country_Name");
					c.Property(ct => ct.IsoCode).HasMaxLength(10).HasColumnName("Guardian_LocalAddress_Country_IsoCode");
				});
				la.Navigation(a => a.State).IsRequired();
			});
			gd.Navigation(p => p.LocalAddress).IsRequired(false);
		});
		builder.Navigation(x => x.GuardianDetails).IsRequired(false);

		// Configure Sibling Student IDs as a JSON column
		builder.Property(x => x.SiblingStudentIds)
			.HasConversion(
				v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
				v => System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<Guid>())
			.HasColumnType("text");
	}
}
