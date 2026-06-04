using System;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Xunit;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class ProfessionalManagementReviewContributionWorkflow_Tests
	: ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly ProfessionalManagementReviewContributionWorkflow _workflow;
	private readonly IRepository<PerformanceTemplate, Guid> _templateRepository;
	private readonly IRepository<ProfessionalReviewCycle, Guid> _cycleRepository;
	private readonly IRepository<PerformanceReviewAssignment, Guid> _assignmentRepository;
	private readonly IEmployeeRepository _employeeRepository;
	private readonly IIdentityRoleRepository _roleRepository;
	private readonly IGuidGenerator _guidGenerator;
	private readonly ICurrentTenant _currentTenant;
	private readonly IClock _clock;

	public ProfessionalManagementReviewContributionWorkflow_Tests()
	{
		_workflow = GetRequiredService<ProfessionalManagementReviewContributionWorkflow>();
		_templateRepository = GetRequiredService<IRepository<PerformanceTemplate, Guid>>();
		_cycleRepository = GetRequiredService<IRepository<ProfessionalReviewCycle, Guid>>();
		_assignmentRepository = GetRequiredService<IRepository<PerformanceReviewAssignment, Guid>>();
		_employeeRepository = GetRequiredService<IEmployeeRepository>();
		_roleRepository = GetRequiredService<IIdentityRoleRepository>();
		_guidGenerator = GetRequiredService<IGuidGenerator>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
		_clock = GetRequiredService<IClock>();
	}

	[Fact]
	public async Task AddObservationAsync_Should_Default_Missing_ObservationDate_To_Current_Date()
	{
		var scenario = await SeedObservationScenarioAsync();

		using (_currentTenant.Change(scenario.TenantId))
		{
			var result = await WithUnitOfWorkAsync(async () =>
			{
				var expectedDate = _clock.Now.Date;
				var observation = await _workflow.AddObservationAsync(
					scenario.AssignmentId,
					new ClassroomObservationRequest
					{
						ObserverEmployeeId = scenario.ObserverEmployeeId,
						ClassName = "8A",
						SubjectName = "Mathematics"
					});

				observation.ObservationDate.ShouldBe(expectedDate);
				return observation;
			});

			result.ClassName.ShouldBe("8A");
			result.SubjectName.ShouldBe("Mathematics");
		}
	}

	private async Task<ObservationScenario> SeedObservationScenarioAsync()
	{
		var tenantId = _guidGenerator.Create();
		using (_currentTenant.Change(tenantId))
		{
			return await WithUnitOfWorkAsync(async () =>
			{
				var teacher = await CreateEmployeeAsync(tenantId, "Teacher", "Mathematics Teacher");
				var observer = await CreateEmployeeAsync(tenantId, "Observer", "Principal");
				var template = new PerformanceTemplate(
					_guidGenerator.Create(),
					tenantId,
					$"Teacher Observation {_guidGenerator.Create():N}",
					PerformanceStaffCategory.Teacher,
					roleName: null);
				await _templateRepository.InsertAsync(template, autoSave: true);

				var cycle = new ProfessionalReviewCycle(
					_guidGenerator.Create(),
					tenantId,
					$"Observation Cycle {_guidGenerator.Create():N}",
					new DateTime(2026, 1, 1),
					new DateTime(2026, 12, 31),
					ProfessionalReviewCycleStatus.Published);
				await _cycleRepository.InsertAsync(cycle, autoSave: true);

				var assignment = new PerformanceReviewAssignment(
					_guidGenerator.Create(),
					tenantId,
					cycle.Id,
					template.Id,
					teacher.Id,
					reportingManagerEmployeeId: observer.Id);
				await _assignmentRepository.InsertAsync(assignment, autoSave: true);

				return new ObservationScenario(tenantId, assignment.Id, observer.Id);
			});
		}
	}

	private async Task<Employee> CreateEmployeeAsync(Guid tenantId, string firstName, string designation)
	{
		var suffix = _guidGenerator.Create().ToString("N");
		var codePrefix = firstName[..Math.Min(firstName.Length, 3)].ToUpperInvariant();
		var roleId = _guidGenerator.Create();
		await _roleRepository.InsertAsync(new IdentityRole(roleId, $"pm-{codePrefix.ToLowerInvariant()}-{suffix[..8]}", tenantId), autoSave: true);

		var employee = new Employee(
			_guidGenerator.Create(),
			tenantId,
			userId: null,
			employeeCode: $"PM{codePrefix}{suffix[..10]}",
			basicInfo: new EmployeeBasicInfo(
				ProfilePictureBlobName: null,
				LastName: "Professional",
				FirstName: firstName,
				MiddleName: null,
				ShortName: firstName,
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
				Email: $"{firstName.ToLowerInvariant()}{suffix[..8]}@test.local",
				PanNo: UniquePan(suffix),
				AdharNo: UniqueAdhar(suffix),
				Address1: null,
				Address2: null,
				Address3: null,
				Department: "Academics",
				Designation: designation,
				DateOfJoining: new DateOnly(2025, 1, 1),
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
			workExperiences: null);

		return await _employeeRepository.InsertAsync(employee, autoSave: true);
	}

	private static string UniquePan(string suffix) =>
		$"PMTES{Math.Abs(suffix.GetHashCode()) % 10000:0000}A";

	private static string UniqueAdhar(string suffix) =>
		$"{Math.Abs(suffix.GetHashCode()) % 1000000000000:000000000000}";

	private sealed record ObservationScenario(Guid TenantId, Guid AssignmentId, Guid ObserverEmployeeId);
}
