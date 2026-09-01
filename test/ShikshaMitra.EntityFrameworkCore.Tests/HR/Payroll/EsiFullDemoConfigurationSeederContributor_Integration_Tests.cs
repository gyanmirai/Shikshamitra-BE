using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.Compensation;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.HR.Payroll.Templates;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Seeders;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Tests.HR.Payroll;

public class EsiFullDemoConfigurationSeederContributor_Integration_Tests
	: ShikshaMitraEntityFrameworkCoreTestBase
{
	private const string AdmissionsReceptionEmail = "admissions-reception@ddvs.com";
	private const string AdmissionsCounselorEmail = "admissions-counselor@ddvs.com";
	private const string ClerkOfficeEmail = "clerk-office@ddvs.com";
	private const string AboveCeilingEmail = "teacher-demo-1@ddvs.com";
	private const string InactiveEmail = "teacher-demo-2@ddvs.com";

	private static readonly IReadOnlyDictionary<string, StatutoryWageTreatment> ExpectedTreatments =
		new Dictionary<string, StatutoryWageTreatment>(StringComparer.OrdinalIgnoreCase)
		{
			["Basic Salary"] = StatutoryWageTreatment.Included,
			["House Rent Allowance (HRA)"] = StatutoryWageTreatment.SpecifiedExclusion,
			["Dearness Allowance (DA)"] = StatutoryWageTreatment.Included,
			["Transport Allowance"] = StatutoryWageTreatment.SpecifiedExclusion,
			["Medical Allowance"] = StatutoryWageTreatment.Included,
			["Special Allowance"] = StatutoryWageTreatment.Included,
			["Performance Bonus"] = StatutoryWageTreatment.Included,
			["Incentive"] = StatutoryWageTreatment.Included
		};
	private static readonly IReadOnlyDictionary<string, string> ExpectedContractDescriptions =
		new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["Medical Allowance"] = "Fixed cash medical allowance paid as monthly contractual remuneration",
			["Performance Bonus"] = "Contractual monthly performance pay",
			["Incentive"] = "Contractual monthly incentive pay"
		};

	private readonly IEmployeeRepository _employeeRepository;
	private readonly EmployeeManager _employeeManager;
	private readonly IdentityUserManager _userManager;
	private readonly IIdentityRoleRepository _roleRepository;
	private readonly IRepository<EarningComponent, Guid> _earningComponentRepository;
	private readonly IRepository<StatutoryRuleVersion, Guid> _ruleRepository;
	private readonly IRepository<StatutoryEstablishment, Guid> _establishmentRepository;
	private readonly IRepository<PayrollComponentStatutoryTreatment, Guid> _treatmentRepository;
	private readonly IRepository<EmployeeStatutoryEnrollment, Guid> _enrollmentRepository;
	private readonly IRepository<EmployeeCompensationRevision, Guid> _revisionRepository;
	private readonly IRepository<EmployeeCompensationRevisionLine, Guid> _revisionLineRepository;
	private readonly IRepository<UserScopeAssignment, Guid> _userScopeRepository;
	private readonly ICurrentTenant _currentTenant;
	private readonly IGuidGenerator _guidGenerator;

	public EsiFullDemoConfigurationSeederContributor_Integration_Tests()
	{
		_employeeRepository = GetRequiredService<IEmployeeRepository>();
		_employeeManager = GetRequiredService<EmployeeManager>();
		_userManager = GetRequiredService<IdentityUserManager>();
		_roleRepository = GetRequiredService<IIdentityRoleRepository>();
		_earningComponentRepository = GetRequiredService<IRepository<EarningComponent, Guid>>();
		_ruleRepository = GetRequiredService<IRepository<StatutoryRuleVersion, Guid>>();
		_establishmentRepository = GetRequiredService<IRepository<StatutoryEstablishment, Guid>>();
		_treatmentRepository = GetRequiredService<IRepository<PayrollComponentStatutoryTreatment, Guid>>();
		_enrollmentRepository = GetRequiredService<IRepository<EmployeeStatutoryEnrollment, Guid>>();
		_revisionRepository = GetRequiredService<IRepository<EmployeeCompensationRevision, Guid>>();
		_revisionLineRepository = GetRequiredService<IRepository<EmployeeCompensationRevisionLine, Guid>>();
		_userScopeRepository = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
		_guidGenerator = GetRequiredService<IGuidGenerator>();
	}

	[Fact]
	public async Task Should_seed_preview_ready_FullDemo_ESI_data_idempotently()
	{
		var tenantId = _guidGenerator.Create();
		var employees = await WithTenantAsync(tenantId, async () =>
		{
			var components = await CreateEarningComponentsAsync(tenantId);
			await CreateRuleAsync(tenantId);

			var reception = await CreateEmployeeAsync(tenantId, AdmissionsReceptionEmail, EmployeeStatusType.Active, 1);
			var counselor = await CreateEmployeeAsync(tenantId, AdmissionsCounselorEmail, EmployeeStatusType.Active, 2);
			var clerk = await CreateEmployeeAsync(tenantId, ClerkOfficeEmail, EmployeeStatusType.Active, 3);
			var aboveCeiling = await CreateEmployeeAsync(tenantId, AboveCeilingEmail, EmployeeStatusType.Active, 4);
			var inactive = await CreateEmployeeAsync(tenantId, InactiveEmail, EmployeeStatusType.Inactive, 5);

			await CreateApprovedRevisionAsync(tenantId, reception.Id, components, BuildSalary(18_000m));
			await CreateApprovedRevisionAsync(tenantId, counselor.Id, components, BuildSalary(20_000m));
			await CreateApprovedRevisionAsync(tenantId, clerk.Id, components, BuildSalary(30_000m));
			await CreateApprovedRevisionAsync(tenantId, aboveCeiling.Id, components, BuildSalary(40_000m));
			return new SeededEmployees(reception.Id, counselor.Id, clerk.Id, aboveCeiling.Id, inactive.Id);
		});

		await SeedTenantAsync(tenantId);
		await SeedTenantAsync(tenantId);

		await WithTenantAsync(tenantId, async () =>
		{
			var establishments = await _establishmentRepository.GetListAsync(x =>
				x.SchemeCode == StatutorySchemeCodes.EmployeeStateInsurance);
			var establishment = establishments.ShouldHaveSingleItem();
			establishment.Status.ShouldBe(StatutoryEstablishmentStatus.Ready);
			establishment.CanActivate().ShouldBeTrue();
			establishment.JurisdictionCode.ShouldBe("IN-GJ");
			establishment.EmployeeCount.ShouldBe(4);
			establishment.CoveredSince.ShouldBe(new DateTime(2025, 11, 21));
			establishment.BenefitsActiveFrom.ShouldBe(new DateTime(2025, 11, 21));
			establishment.RegistrationNumber.ShouldNotBeNull();
			establishment.RegistrationNumber.ShouldStartWith("DEMO-");
			establishment.EvidenceReference.ShouldNotBeNull();
			establishment.EvidenceReference.ShouldContain("NOT-FOR-FILING");
			establishment.ConfirmedByUserId.ShouldBeNull();
			establishment.ConfirmedAt.ShouldBeNull();
			var hrManagerScope = (await _userScopeRepository.GetListAsync(x => x.RoleName == "HR Manager"))
				.ShouldHaveSingleItem();
			hrManagerScope.IsActive.ShouldBeTrue();
			hrManagerScope.AcademicYearId.ShouldBeNull();
			hrManagerScope.EducationBoardId.ShouldBeNull();
			hrManagerScope.EducationMediumId.ShouldBeNull();
			hrManagerScope.EducationMediumStageId.ShouldBeNull();
			hrManagerScope.StandardId.ShouldBeNull();
			hrManagerScope.DivisionId.ShouldBeNull();

			var components = (await _earningComponentRepository.GetListAsync())
				.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
			var treatments = await _treatmentRepository.GetListAsync(x =>
				x.SchemeCode == StatutorySchemeCodes.EmployeeStateInsurance);
			treatments.Count.ShouldBe(ExpectedTreatments.Count);
			foreach (var component in components.Values)
			{
				var effectiveTreatments = treatments.Where(x => x.ComponentId == component.Id).ToList();
				var treatment = effectiveTreatments.ShouldHaveSingleItem();
				treatment.Treatment.ShouldBe(ExpectedTreatments[component.Name]);
			}
			foreach (var expectedDescription in ExpectedContractDescriptions)
			{
				components[expectedDescription.Key].Description.ShouldBe(expectedDescription.Value);
			}

			var enrollments = await _enrollmentRepository.GetListAsync(x =>
				x.SchemeCode == StatutorySchemeCodes.EmployeeStateInsurance);
			enrollments.Count.ShouldBe(5);
			enrollments.Select(x => x.EmployeeId).Distinct().Count().ShouldBe(5);
			AssertEnrollment(enrollments, employees.ReceptionId, EmployeeStatutoryEnrollmentStatus.Covered, true);
			AssertEnrollment(
				enrollments,
				employees.CounselorId,
				EmployeeStatutoryEnrollmentStatus.RegistrationPending,
				false);
			var clerkEnrollment = AssertEnrollment(
				enrollments,
				employees.ClerkId,
				EmployeeStatutoryEnrollmentStatus.Covered,
				true);
			clerkEnrollment.ContributionPeriodEnd.ShouldNotBeNull();
			clerkEnrollment.ContributionPeriodEnd.Value.Day.ShouldBeInRange(30, 31);
			AssertEnrollment(
				enrollments,
				employees.AboveCeilingId,
				EmployeeStatutoryEnrollmentStatus.NotCovered,
				false);
			AssertEnrollment(enrollments, employees.InactiveId, EmployeeStatutoryEnrollmentStatus.Exited, false);
			var inactiveEmployee = await _employeeRepository.GetAsync(employees.InactiveId);
			inactiveEmployee.StatusEffectiveDate.ShouldBe(new DateOnly(2025, 11, 20));
			var currentMonthStart = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
			var currentMonthEnd = currentMonthStart.AddMonths(1).AddDays(-1);
			EmployeeLifecycleEligibility.IsEligibleForPayrollMonth(
				inactiveEmployee,
				currentMonthStart,
				currentMonthEnd).ShouldBeFalse();

			var activeEmployeeIds = new HashSet<Guid>
			{
				employees.ReceptionId,
				employees.CounselorId,
				employees.ClerkId,
				employees.AboveCeilingId
			};
			var revisions = await _revisionRepository.GetListAsync(x => activeEmployeeIds.Contains(x.EmployeeId));
			revisions.Count.ShouldBe(activeEmployeeIds.Count);
			revisions.ShouldAllBe(x => x.Status == CompensationRevisionStatus.Approved);

			await AssertPreviewReadyAsync(establishment, treatments, enrollments, revisions);
		});
	}

	[Fact]
	public async Task Should_preserve_existing_non_synthetic_ESI_establishment()
	{
		var tenantId = _guidGenerator.Create();
		var establishmentId = _guidGenerator.Create();
		await WithTenantAsync(tenantId, async () =>
		{
			var establishment = new StatutoryEstablishment(
				establishmentId,
				tenantId,
				StatutorySchemeCodes.EmployeeStateInsurance,
				"IN-GJ");
			establishment.Configure(
				"IN-GJ",
				"CUSTOM-ESIC-REGISTRATION",
				25,
				new DateTime(2024, 4, 1),
				new DateTime(2024, 4, 1),
				"Uploaded employer registration certificate");
			await _establishmentRepository.InsertAsync(establishment, autoSave: true);
		});

		await SeedTenantAsync(tenantId);

		await WithTenantAsync(tenantId, async () =>
		{
			var preserved = (await _establishmentRepository.GetListAsync()).ShouldHaveSingleItem();
			preserved.Id.ShouldBe(establishmentId);
			preserved.RegistrationNumber.ShouldBe("CUSTOM-ESIC-REGISTRATION");
			preserved.EvidenceReference.ShouldBe("Uploaded employer registration certificate");
			(await _treatmentRepository.GetListAsync()).ShouldBeEmpty();
			(await _enrollmentRepository.GetListAsync()).ShouldBeEmpty();
		});
	}

	private async Task<Dictionary<string, EarningComponent>> CreateEarningComponentsAsync(Guid tenantId)
	{
		var result = new Dictionary<string, EarningComponent>(StringComparer.OrdinalIgnoreCase);
		var displayOrder = 0;
		foreach (var name in ExpectedTreatments.Keys)
		{
			var component = new EarningComponent(
				_guidGenerator.Create(),
				tenantId,
				name,
				$"FullDemo test earning {name}",
				EarningComponentType.Fixed,
				CalculationType.Amount,
				0m,
				null,
				null,
				ComponentDependency.None,
				visibleInPayslip: true,
				displayOrder++);
			await _earningComponentRepository.InsertAsync(component, autoSave: true);
			result[name] = component;
		}

		return result;
	}

	private async Task CreateRuleAsync(Guid tenantId)
	{
		await _ruleRepository.InsertAsync(
			new StatutoryRuleVersion(
				_guidGenerator.Create(),
				tenantId,
				StatutorySchemeCodes.EmployeeStateInsurance,
				"IN-GJ",
				1,
				new DateTime(2025, 11, 21),
				null,
				10,
				21_000m,
				25_000m,
				0.75m,
				3.25m,
				50m,
				15m,
				StatutoryRoundingPolicy.NextHigherRupee,
				"https://www.esic.gov.in/",
				new DateTime(2026, 8, 31)),
			autoSave: true);
	}

	private async Task<Employee> CreateEmployeeAsync(
		Guid tenantId,
		string email,
		EmployeeStatusType status,
		int seed)
	{
		var role = new IdentityRole(_guidGenerator.Create(), $"EsiSeedRole{seed}", tenantId);
		role.SetProperty(EmployeeRoleAudience.ExtraPropertyName, EmployeeRoleAudience.Employee);
		role.SetProperty(EmployeeGroupAudience.ExtraPropertyName, EmployeeGroupAudience.Both);
		role.SetProperty(EmployeeOnboardingRoleAssignment.ExtraPropertyName, true);
		await _roleRepository.InsertAsync(role, autoSave: true);
		var user = new IdentityUser(
			_guidGenerator.Create(),
			$"esiseed{seed}",
			email,
			tenantId);
		(await _userManager.CreateAsync(user, "Test@1234")).Succeeded.ShouldBeTrue();

		var employee = await _employeeManager.CreateEmployeeAsync(
			user.Id,
			role.Id,
			$"Esi{seed}",
			"FullDemo",
			null,
			$"E{seed}",
			status,
			new DateOnly(1990, 1, Math.Min(seed, 28)),
			email: email,
			panNo: $"ABCDE{seed:0000}F",
			adharNo: $"90000000{seed:0000}",
			department: "FullDemo",
			designation: "FullDemo Employee",
			dateOfJoining: new DateOnly(2020, 6, 1),
			employeeGroup: EmployeeGroupType.NonTeaching);
		return await _employeeRepository.InsertAsync(employee, autoSave: true);
	}

	private async Task CreateApprovedRevisionAsync(
		Guid tenantId,
		Guid employeeId,
		IReadOnlyDictionary<string, EarningComponent> components,
		IReadOnlyDictionary<string, decimal> earnings)
	{
		var grossSalary = earnings.Values.Sum();
		var revision = new EmployeeCompensationRevision(
			_guidGenerator.Create(),
			tenantId,
			employeeId,
			1,
			new DateTime(2025, 11, 1, 0, 0, 0, DateTimeKind.Utc));
		revision.ReplaceDraft(
			revision.EffectiveMonth,
			SalaryInputBasis.Gross,
			CompensationProtectionPolicy.ProtectGross,
			grossSalary,
			grossSalary,
			grossSalary,
			grossSalary,
			grossSalary,
			null,
			null,
			null,
			null,
			"IN-GJ",
			"{}");
		revision.Submit(_guidGenerator.Create(), DateTime.UtcNow);
		revision.Approve(_guidGenerator.Create(), DateTime.UtcNow);
		await _revisionRepository.InsertAsync(revision, autoSave: false);

		var displayOrder = 0;
		var lines = earnings.Select(item => new EmployeeCompensationRevisionLine(
			_guidGenerator.Create(),
			tenantId,
			revision.Id,
			employeeId,
			components[item.Key].Id,
			item.Key,
			null,
			item.Value,
			isEarning: true,
			item.Key == "Basic Salary" ? PayrollComponentSemanticType.Basic : PayrollComponentSemanticType.Ordinary,
			CompensationLineSource.Template,
			displayOrder++)).ToList();
		await _revisionLineRepository.InsertManyAsync(lines, autoSave: true);
	}

	private async Task AssertPreviewReadyAsync(
		StatutoryEstablishment establishment,
		IReadOnlyCollection<PayrollComponentStatutoryTreatment> treatments,
		IReadOnlyCollection<EmployeeStatutoryEnrollment> enrollments,
		IReadOnlyCollection<EmployeeCompensationRevision> revisions)
	{
		var rule = (await _ruleRepository.GetListAsync()).ShouldHaveSingleItem();
		var revisionIds = revisions.Select(x => x.Id).ToHashSet();
		var lines = await _revisionLineRepository.GetListAsync(x => revisionIds.Contains(x.RevisionId));
		var treatmentMap = treatments.ToDictionary(x => x.ComponentId, x => x.Treatment);

		foreach (var revision in revisions)
		{
			var enrollment = enrollments.Single(x => x.EmployeeId == revision.EmployeeId);
			var wageLines = lines
				.Where(x => x.RevisionId == revision.Id && x.IsEarning)
				.Select(x => new StatutoryWageLine(
					x.ComponentId!.Value,
					x.Name,
					x.Amount,
					treatmentMap[x.ComponentId.Value]))
				.ToList();
			var result = EsiStatutoryCalculator.Calculate(
				DateTime.UtcNow,
				rule,
				establishment,
				enrollment,
				wageLines,
				assumeEstablishmentActive: true);
			result.IssueCodes.ShouldBeEmpty();
			result.CanCalculate.ShouldBeTrue();
		}
	}

	private static EmployeeStatutoryEnrollment AssertEnrollment(
		IEnumerable<EmployeeStatutoryEnrollment> enrollments,
		Guid employeeId,
		EmployeeStatutoryEnrollmentStatus status,
		bool hasSyntheticInsuranceNumber)
	{
		var enrollment = enrollments.Single(x => x.EmployeeId == employeeId);
		enrollment.Status.ShouldBe(status);
		if (hasSyntheticInsuranceNumber)
		{
			enrollment.InsuranceNumber.ShouldStartWith("DEMO-");
		}
		else
		{
			enrollment.InsuranceNumber.ShouldBeNull();
		}

		return enrollment;
	}

	private static IReadOnlyDictionary<string, decimal> BuildSalary(decimal grossSalary)
	{
		var basic = grossSalary * 0.50m;
		var hra = grossSalary * 0.20m;
		var da = grossSalary * 0.10m;
		const decimal transport = 1_600m;
		const decimal medical = 1_250m;
		var special = grossSalary - basic - hra - da - transport - medical;
		return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
		{
			["Basic Salary"] = basic,
			["House Rent Allowance (HRA)"] = hra,
			["Dearness Allowance (DA)"] = da,
			["Transport Allowance"] = transport,
			["Medical Allowance"] = medical,
			["Special Allowance"] = special,
			["Performance Bonus"] = 0m,
			["Incentive"] = 0m
		};
	}

	private Task SeedTenantAsync(Guid tenantId) =>
		WithTenantAsync(
			tenantId,
			() => GetRequiredService<EsiFullDemoConfigurationSeederContributor>()
				.SeedAsync(new DataSeedContext(tenantId)));

	private async Task WithTenantAsync(Guid tenantId, Func<Task> action)
	{
		using (_currentTenant.Change(tenantId))
		{
			await WithUnitOfWorkAsync(action);
		}
	}

	private async Task<TResult> WithTenantAsync<TResult>(Guid tenantId, Func<Task<TResult>> action)
	{
		using (_currentTenant.Change(tenantId))
		{
			return await WithUnitOfWorkAsync(action);
		}
	}

	private sealed record SeededEmployees(
		Guid ReceptionId,
		Guid CounselorId,
		Guid ClerkId,
		Guid AboveCeilingId,
		Guid InactiveId);
}
