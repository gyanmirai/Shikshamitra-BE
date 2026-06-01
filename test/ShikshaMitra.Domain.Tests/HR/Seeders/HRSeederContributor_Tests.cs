using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using ShikshaMitra.Common;
using ShikshaMitra.Seeders;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Seeders;

public class HRSeederContributor_Tests
{
	[Fact]
	public void Surat_Demo_Profiles_Should_Be_Complete_And_Local()
	{
		var profiles = HRSeederContributor.BuildSuratSeedEmployeeProfiles();

		profiles.Count.ShouldBeGreaterThanOrEqualTo(16);
		profiles.Keys.ShouldAllBe(email => email.EndsWith("@ddvs.com", StringComparison.OrdinalIgnoreCase));

		foreach (var profile in profiles.Values)
		{
			profile.FirstName.ShouldNotBeNullOrWhiteSpace();
			profile.LastName.ShouldNotBeNullOrWhiteSpace();
			profile.Mobile.ShouldStartWith("98250");
			profile.RoleName.ShouldNotBeNullOrWhiteSpace();
			profile.Designation.ShouldNotBeNullOrWhiteSpace();
			profile.Department.ShouldNotBeNullOrWhiteSpace();
			profile.Qualification.ShouldNotBeNullOrWhiteSpace();
			profile.Address1.ShouldNotBeNullOrWhiteSpace();
			profile.City.ShouldBe("Surat");
			profile.State.Name.ShouldBe("Gujarat");
			profile.State.IsoCode.ShouldBe("GJ");
			profile.Country.Name.ShouldBe("India");
			profile.PinCode.ShouldStartWith("39");
		}
	}

	[Fact]
	public void Surat_Demo_Profiles_Should_Have_Work_Experience()
	{
		var profiles = HRSeederContributor.BuildSuratSeedEmployeeProfiles();

		foreach (var profile in profiles.Values)
		{
			var experiences = HRSeederContributor.BuildWorkExperiences(profile);

			experiences.Count.ShouldBeGreaterThanOrEqualTo(1);
			experiences.ShouldAllBe(x =>
				!string.IsNullOrWhiteSpace(x.OrganizationName) &&
				!string.IsNullOrWhiteSpace(x.Designation) &&
				x.FromDate.HasValue &&
				x.ToDate.HasValue);
		}
	}

	[Fact]
	public void Seed_Document_Content_Should_Be_A_Valid_Pdf_Upload()
	{
		var profile = HRSeederContributor.BuildSuratSeedEmployeeProfiles().Values.First();
		var bytes = HRSeederContributor.BuildSeedPdfBytes("Joining Letter", profile);

		bytes.Length.ShouldBeGreaterThan(4);
		bytes[0].ShouldBe((byte)'%');
		bytes[1].ShouldBe((byte)'P');
		bytes[2].ShouldBe((byte)'D');
		bytes[3].ShouldBe((byte)'F');

		FileValidationService.ValidateDocumentUpload(
			"joining-letter.pdf",
			bytes,
			bytes.LongLength);
	}

	[Fact]
	public void Past_Attendance_Months_Should_Prefer_New_Config_And_Clamp()
	{
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ShikshaMitra:Seeders:HR:PastPayrollMonthsToSeed"] = "2",
				["ShikshaMitra:Seeders:HR:PastAttendanceMonthsToSeed"] = "18"
			})
			.Build();

		HRSeederContributor.ResolvePastAttendanceMonthsToSeed(config).ShouldBe(12);
	}

	[Fact]
	public void Past_Attendance_Months_Should_Fallback_To_Legacy_Payroll_Config()
	{
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ShikshaMitra:Seeders:HR:PastPayrollMonthsToSeed"] = "4"
			})
			.Build();

		HRSeederContributor.ResolvePastAttendanceMonthsToSeed(config).ShouldBe(4);
	}

	[Fact]
	public void Performance_Management_Demo_Seed_Should_Default_On_For_Demo_Profiles()
	{
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				[ShikshaMitraSeederProfile.ContextPropertyName] = ShikshaMitraSeederProfile.FastDemo
			})
			.Build();

		HRSeederContributor.ResolveShouldSeedPerformanceManagementDemoData(config).ShouldBeTrue();
	}

	[Fact]
	public void Performance_Management_Demo_Seed_Should_Default_Off_For_Minimal_Profile()
	{
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				[ShikshaMitraSeederProfile.ContextPropertyName] = ShikshaMitraSeederProfile.Minimal
			})
			.Build();

		HRSeederContributor.ResolveShouldSeedPerformanceManagementDemoData(config).ShouldBeFalse();
	}

	[Fact]
	public void Performance_Management_Demo_Seed_Should_Respect_Explicit_Config()
	{
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				[ShikshaMitraSeederProfile.ContextPropertyName] = ShikshaMitraSeederProfile.Minimal,
				["ShikshaMitra:Seeders:HR:SeedPerformanceManagementDemoData"] = "true"
			})
			.Build();

		HRSeederContributor.ResolveShouldSeedPerformanceManagementDemoData(config).ShouldBeTrue();
	}

	[Fact]
	public void Attendance_Seed_Should_Skip_Weekends_And_Holidays()
	{
		var holidays = HRSeederContributor.BuildSeedHolidayDateSet(
			[
				new HRSeederContributor.SeedHolidayDateRange(
					new DateOnly(2026, 5, 1),
					new DateOnly(2026, 5, 31))
			],
			new DateOnly(2026, 5, 1),
			new DateOnly(2026, 5, 31));

		HRSeederContributor.ShouldSeedAttendanceForDate(new DateOnly(2026, 5, 4), holidays).ShouldBeFalse();
		HRSeederContributor.ShouldSeedAttendanceForDate(new DateOnly(2026, 5, 9), holidays).ShouldBeFalse();
		HRSeederContributor.ShouldSeedAttendanceForDate(new DateOnly(2026, 4, 30), holidays).ShouldBeTrue();
	}

	[Fact]
	public void Holiday_Date_Set_Should_Clamp_Seed_Ranges_To_Attendance_Window()
	{
		var holidays = HRSeederContributor.BuildSeedHolidayDateSet(
			[
				new HRSeederContributor.SeedHolidayDateRange(
					new DateOnly(2026, 4, 28),
					new DateOnly(2026, 5, 3)),
				new HRSeederContributor.SeedHolidayDateRange(
					new DateOnly(2026, 6, 1),
					new DateOnly(2026, 6, 5))
			],
			new DateOnly(2026, 5, 1),
			new DateOnly(2026, 5, 31));

		holidays.Count.ShouldBe(3);
		holidays.ShouldContain(new DateOnly(2026, 5, 1));
		holidays.ShouldContain(new DateOnly(2026, 5, 2));
		holidays.ShouldContain(new DateOnly(2026, 5, 3));
		holidays.ShouldNotContain(new DateOnly(2026, 4, 30));
		holidays.ShouldNotContain(new DateOnly(2026, 6, 1));
	}
}
