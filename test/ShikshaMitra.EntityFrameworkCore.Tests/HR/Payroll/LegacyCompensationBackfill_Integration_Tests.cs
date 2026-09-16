using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Compensation;
using ShikshaMitra.HR.Payroll.Templates;
using ShikshaMitra.Seeders;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Tests.HR.Payroll;

public class LegacyCompensationBackfill_Integration_Tests
	: ShikshaMitraEntityFrameworkCoreTestBase
{
	private readonly HRSeederContributor _hrSeeder;
	private readonly IEmployeeRepository _employeeRepository;
	private readonly IEmployeePayrollComponentRepository _payrollComponentRepository;
	private readonly IRepository<EmployeeCompensationRevision, Guid> _revisionRepository;
	private readonly IRepository<EmployeeCompensationRevisionLine, Guid> _lineRepository;
	private readonly IRepository<IdentityRole, Guid> _roleRepository;
	private readonly ICurrentTenant _currentTenant;
	private readonly IGuidGenerator _guidGenerator;

	public LegacyCompensationBackfill_Integration_Tests()
	{
		_hrSeeder = GetRequiredService<HRSeederContributor>();
		_employeeRepository = GetRequiredService<IEmployeeRepository>();
		_payrollComponentRepository = GetRequiredService<IEmployeePayrollComponentRepository>();
		_revisionRepository = GetRequiredService<IRepository<EmployeeCompensationRevision, Guid>>();
		_lineRepository = GetRequiredService<IRepository<EmployeeCompensationRevisionLine, Guid>>();
		_roleRepository = GetRequiredService<IRepository<IdentityRole, Guid>>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
		_guidGenerator = GetRequiredService<IGuidGenerator>();
	}

	[Fact]
	public async Task Should_backfill_once_without_changing_employee_status_or_legacy_workflow_history()
	{
		var tenantId = _guidGenerator.Create();
		var employeeId = _guidGenerator.Create();

		await WithTenantAsync(tenantId, async () =>
		{
			var roleId = _guidGenerator.Create();
			await _roleRepository.InsertAsync(
				new IdentityRole(roleId, $"legacy-payroll-{Guid.NewGuid():N}", tenantId),
				autoSave: true);
			await _employeeRepository.InsertAsync(
				new Employee(
					employeeId,
					tenantId,
					userId: null,
					employeeCode: $"LEG-{Guid.NewGuid():N}"[..16],
					basicInfo: new EmployeeBasicInfo(
						ProfilePictureBlobName: null,
						LastName: "History",
						FirstName: "Legacy",
						MiddleName: null,
						ShortName: "LH",
						EmployeeStatusType: EmployeeStatusType.Active,
						DateOfBirth: new DateOnly(1990, 1, 1),
						FamilyCast: null,
						CastType: null,
						GenderType: null,
						Religion: null,
						MaritalStatus: null,
						BloodGroup: null,
						MobileNo: null,
						SecondaryMobileNo: null,
						Email: $"legacy-{Guid.NewGuid():N}@test.local",
						PanNo: "ABCDE1234F",
						AdharNo: "123456789012",
						Address1: null,
						Address2: null,
						Address3: null,
						Department: null,
						Designation: null,
						DateOfJoining: new DateOnly(2025, 4, 1),
						EmployeeGroup: null,
						Qualification: null,
						RoleId: roleId,
						IdentificationMark1: null,
						City: null,
						State: null,
						Country: null,
						PinCode: null,
						ShiftId: null),
					bankDetails: new BankDetails(null, null, null, null, null),
					workExperiences: null),
				autoSave: true);

			await _payrollComponentRepository.InsertAsync(
				new EmployeePayrollComponent(
					_guidGenerator.Create(), employeeId, "Basic Salary", componentId: null,
					description: null, amount: 30_000m, isFixed: true, isTemp: false,
					applicableMonth: new DateTime(2025, 4, 1), SalaryComponentType.Fixed,
					SalaryComponentDependency.None, isEarning: true, usesMasterValue: false,
					PayrollComponentSemanticType.Basic),
				autoSave: true);

			var legacyDraft = new EmployeeCompensationRevision(
				_guidGenerator.Create(), tenantId, employeeId, 1, new DateTime(2025, 4, 1),
				"legacy-pending-workflow");
			legacyDraft.ReplaceDraft(
				new DateTime(2025, 4, 1), SalaryInputBasis.Gross,
				CompensationProtectionPolicy.ProtectGross, 30_000m, 30_000m,
				30_000m, 30_000m, 30_000m, null, null, null, null, "IN-GJ", "{}");
			await _revisionRepository.InsertAsync(legacyDraft, autoSave: true);
		});

		await WithTenantAsync(tenantId, _hrSeeder.BackfillLegacyCompensationRevisionsAsync);
		await WithTenantAsync(tenantId, _hrSeeder.BackfillLegacyCompensationRevisionsAsync);

		await WithTenantAsync(tenantId, async () =>
		{
			var revisions = (await _revisionRepository.GetListAsync())
				.Where(x => x.EmployeeId == employeeId)
				.OrderBy(x => x.RevisionNumber)
				.ToList();
			revisions.Count.ShouldBe(2);
			revisions[0].Status.ShouldBe(CompensationRevisionStatus.Draft);
			revisions[1].RevisionNumber.ShouldBe(2);
			revisions[1].Status.ShouldBe(CompensationRevisionStatus.Approved);
			revisions[1].ApprovedByUserId.ShouldBeNull();

			var lines = (await _lineRepository.GetListAsync())
				.Where(x => x.RevisionId == revisions[1].Id)
				.ToList();
			lines.Count.ShouldBe(1);
			lines[0].Source.ShouldBe(CompensationLineSource.Legacy);

			var employee = await _employeeRepository.GetAsync(employeeId);
			employee.EmployeeStatusType.ShouldBe(EmployeeStatusType.Active);
		});
	}

	private async Task WithTenantAsync(Guid tenantId, Func<Task> action)
	{
		using (_currentTenant.Change(tenantId))
		{
			await WithUnitOfWorkAsync(action);
		}
	}
}
