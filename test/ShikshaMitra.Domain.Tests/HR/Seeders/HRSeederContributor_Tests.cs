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
	public void Surat_Demo_Profiles_Should_Include_Cbse_Principal_Login_Profile()
	{
		var profiles = HRSeederContributor.BuildSuratSeedEmployeeProfiles();

		profiles.ContainsKey("principal-cbse@ddvs.com").ShouldBeTrue();
		profiles["principal-cbse@ddvs.com"].RoleName.ShouldBe("Principal");
	}

	[Fact]
	public void Seeded_Login_Emails_Should_Have_Employee_Profiles()
	{
		var profiles = HRSeederContributor.BuildSuratSeedEmployeeProfiles();
		var loginEmails = UserSeederContributor.BuildSeededLoginEmails()
			.Concat(TestLoginSeederContributor.BuildSeededLoginEmails())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		loginEmails.ShouldAllBe(email => profiles.ContainsKey(email));
	}

	[Fact]
	public void Test_Login_Employee_Aliases_Should_Prioritize_Login_Email()
	{
		var aliasEmailsByLoginEmail = TestLoginSeederContributor.BuildSeededLoginEmployeeAliasEmailsByEmail();

		foreach (var (loginEmail, aliasEmails) in aliasEmailsByLoginEmail)
		{
			aliasEmails.Count.ShouldBeGreaterThan(0);
			aliasEmails[0].ShouldBe(loginEmail);
			aliasEmails.Distinct(StringComparer.OrdinalIgnoreCase).Count().ShouldBe(aliasEmails.Count);
		}
	}

	[Fact]
	public void Seeded_Login_Employee_Profiles_Should_Be_Protected_From_Inactive_Distribution()
	{
		var protectedEmails = HRSeederContributor.BuildSeededLoginEmployeeEmails();
		var loginEmails = UserSeederContributor.BuildSeededLoginEmails()
			.Concat(TestLoginSeederContributor.BuildSeededLoginEmails())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		loginEmails.ShouldAllBe(email => protectedEmails.Contains(email));
	}

	[Fact]
	public void Seeded_Employee_Financial_Data_Should_Be_Complete_And_Demo_Safe()
	{
		var profiles = HRSeederContributor.BuildSuratSeedEmployeeProfiles();

		foreach (var profile in profiles.Values)
		{
			var financialData = HRSeederContributor.BuildSeedEmployeeFinancialData(profile, "EMP001");
			var bankDetails = HRSeederContributor.BuildSeedEmployeeBankDetails(profile);

			financialData.BankName.ShouldNotBeNullOrWhiteSpace();
			financialData.BranchName.ShouldNotBeNullOrWhiteSpace();
			financialData.AccountHolderName.ShouldBe($"{profile.FirstName} {profile.LastName}");
			financialData.AccountNumber.ShouldNotBe("12345678");
			financialData.AccountNumber.Length.ShouldBeGreaterThanOrEqualTo(10);
			financialData.IfscCode.Length.ShouldBe(11);
			financialData.EpfNumber.ShouldStartWith("GJ/SRT/");
			financialData.UanNumber.Length.ShouldBe(12);
			financialData.PunchId.ShouldBe("DDVS-EMP001");
			financialData.TotalWorkingHourPerMonth.ShouldBeGreaterThan(0m);
			bankDetails.BankName.ShouldBe(financialData.BankName);
			bankDetails.AccountHolderName.ShouldBe(financialData.AccountHolderName);
			bankDetails.AccountNumber.ShouldBe(financialData.AccountNumber);
			bankDetails.IfscCode.ShouldBe(financialData.IfscCode);
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
	public void Past_Attendance_Months_Should_Use_Current_Payroll_Config()
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
