using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.Seeders;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
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
	public void Seeded_Principals_Should_Be_Restricted_To_Their_Named_Board()
	{
		var gsebStageId = Guid.NewGuid();
		var cbseStageId = Guid.NewGuid();
		var stages = new[]
		{
			new SeededEmployeeAcademicScopePolicy.Stage(gsebStageId, "Secondary", EducationBoardType.GSEB),
			new SeededEmployeeAcademicScopePolicy.Stage(cbseStageId, "Secondary", EducationBoardType.CBSE)
		};

		SeededEmployeeAcademicScopePolicy.ResolveStageIds("Principal", "principal-gseb@ddvs.com", stages)
			.ShouldBe([gsebStageId]);
		SeededEmployeeAcademicScopePolicy.ResolveStageIds("Principal", "principal-cbse@ddvs.com", stages)
			.ShouldBe([cbseStageId]);
	}

	[Fact]
	public void Generic_Administrative_Seed_Profile_Should_Retain_All_Board_Access()
	{
		var stageIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
		var stages = new[]
		{
			new SeededEmployeeAcademicScopePolicy.Stage(stageIds[0], "Secondary", EducationBoardType.GSEB),
			new SeededEmployeeAcademicScopePolicy.Stage(stageIds[1], "Secondary", EducationBoardType.CBSE)
		};

		SeededEmployeeAcademicScopePolicy.ResolveStageIds("Admin", "admin@ddvs.com", stages)
			.ShouldBe(stageIds);
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
	public void Board_Scoped_Test_Logins_Should_Not_Receive_Academic_Year_Wide_Access()
	{
		var academicYearWideLogins = TestLoginSeederContributor.BuildAcademicYearWideScopeLoginEmails();

		academicYearWideLogins.ShouldBeEmpty();
		academicYearWideLogins.ShouldNotContain("principal-gseb@ddvs.com");
		academicYearWideLogins.ShouldNotContain("principal-cbse@ddvs.com");
		academicYearWideLogins.ShouldNotContain("admissions-reception@ddvs.com");
		academicYearWideLogins.ShouldNotContain("student-crm-denied@ddvs.com");
	}

	[Fact]
	public void Existing_Academic_Year_Permissions_Should_Be_Removed_Only_For_Board_Scoped_Test_Logins()
	{
		TestLoginSeederContributor.ShouldRemoveAcademicYearWidePermissions("principal-gseb@ddvs.com")
			.ShouldBeTrue();
		TestLoginSeederContributor.ShouldRemoveAcademicYearWidePermissions("principal-cbse@ddvs.com")
			.ShouldBeTrue();
		TestLoginSeederContributor.ShouldRemoveAcademicYearWidePermissions("admin-ddvs@ddvs.com")
			.ShouldBeFalse();
		TestLoginSeederContributor.ShouldRemoveAcademicYearWidePermissions(
			"principal-gseb@ddvs.com",
			requiresAcademicYearWideScope: true)
			.ShouldBeFalse();
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
	public void Seeded_Demo_Employee_Match_Should_Not_Treat_All_Ddvs_Emails_As_Seeded()
	{
		HRSeederContributor.IsSeededDemoEmployeeEmail("akshay.patel@ddvs.com").ShouldBeTrue();
		HRSeederContributor.IsSeededDemoEmployeeEmail("new.staff@ddvs.com").ShouldBeFalse();
		HRSeederContributor.IsSeededDemoEmployeeEmail("akshay.patel@example.com").ShouldBeFalse();
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
	public void Seed_Payroll_Component_Catalog_Should_Cover_Seeded_Salary_Profile()
	{
		var earningComponents = HRSeederContributor.BuildSeedEarningComponentSpecs();
		var deductionComponents = HRSeederContributor.BuildSeedDeductionComponentSpecs();

		earningComponents.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count().ShouldBe(earningComponents.Count);
		deductionComponents.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count().ShouldBe(deductionComponents.Count);
		earningComponents.Where(x => x.Name != "Basic Salary").ShouldAllBe(x => x.CalculationType == CalculationType.Amount && x.Amount > 0m);
		earningComponents.Single(x => x.Name == "Basic Salary").Percentage.ShouldBe(60m);
		earningComponents.Single(x => x.Name == "Basic Salary").DependOn.ShouldBe(ComponentDependency.Gross);
		deductionComponents.ShouldAllBe(x => x.CalculationType == CalculationType.Amount && x.Amount.HasValue && x.Amount.Value > 0m);

		foreach (var name in new[]
		         {
			         "Basic Salary",
			         "House Rent Allowance (HRA)",
			         "Dearness Allowance (DA)",
			         "Transport Allowance",
			         "Medical Allowance",
			         "Special Allowance",
			         "Performance Bonus",
			         "Incentive"
		         })
		{
			earningComponents.ShouldContain(x => x.Name == name);
		}

		foreach (var name in new[]
		         {
			         "Provident Fund (PF)",
			         "Professional Tax",
			         "Income Tax (TDS)",
			         "Loan Repayment",
			         "Advance Salary Deduction",
			         "Loss of Pay (LOP)"
		         })
		{
			deductionComponents.ShouldContain(x => x.Name == name);
		}

		deductionComponents.ShouldNotContain(x => x.Name.Contains("State Insurance"));
	}

	[Fact]
	public void Seed_Payroll_Component_Match_Should_ReUse_Legacy_Name_Matched_Rows()
	{
		var masterId = Guid.NewGuid();
		var legacyRow = new EmployeePayrollComponent(
			Guid.NewGuid(),
			Guid.NewGuid(),
			"basic salary",
			null,
			"Legacy seeded row without master id",
			30000m,
			isFixed: true,
			isTemp: false,
			applicableMonth: null,
			SalaryComponentType.Fixed,
			dependOn: null,
			isEarning: true,
			usesMasterValue: false);
		var existingById = new Dictionary<(Guid ComponentId, bool IsEarning), EmployeePayrollComponent>();
		var existingByName = new Dictionary<(string Name, bool IsEarning), EmployeePayrollComponent>
		{
			[("BASIC SALARY", true)] = legacyRow
		};

		var match = HRSeederContributor.FindExistingSeedPayrollComponent(
			existingById,
			existingByName,
			masterId,
			"Basic Salary",
			isEarning: true);

		match.ShouldBe(legacyRow);
	}

	[Fact]
	public void Seed_Shift_Catalog_Should_Include_Default_Full_Day_Shift()
	{
		var shifts = HRSeederContributor.BuildSeedShiftSpecs();

		shifts.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count().ShouldBe(shifts.Count);
		var fullDayShift = shifts.Single(x => x.Name == "Full Day Shift");
		fullDayShift.StartTime.ShouldBe(new TimeOnly(7, 0));
		fullDayShift.EndTime.ShouldBe(new TimeOnly(17, 30));
	}

	[Fact]
	public void Seed_Default_Shift_Should_Prefer_Full_Day_And_Fall_Back_For_Legacy_Catalogs()
	{
		var morningShiftId = Guid.NewGuid();
		var fullDayShiftId = Guid.NewGuid();

		HRSeederContributor.ResolveSeedDefaultShiftId(
			[
				BuildShift(morningShiftId, "Morning Shift"),
				BuildShift(fullDayShiftId, "Full Day Shift")
			])
			.ShouldBe(fullDayShiftId);

		HRSeederContributor.ResolveSeedDefaultShiftId(
			[
				BuildShift(morningShiftId, "Legacy Demo Shift")
			])
			.ShouldBe(morningShiftId);
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

	private static ShiftManagement BuildShift(Guid id, string name)
	{
		var shift = new ShiftManagement
		{
			ShiftName = name,
			StartTime = new TimeOnly(7, 0),
			EndTime = new TimeOnly(17, 30)
		};
		var idProperty = typeof(ShiftManagement).GetProperty(nameof(ShiftManagement.Id))
			?? throw new InvalidOperationException("ShiftManagement.Id property was not found.");
		idProperty.SetValue(shift, id);
		return shift;
	}
}
