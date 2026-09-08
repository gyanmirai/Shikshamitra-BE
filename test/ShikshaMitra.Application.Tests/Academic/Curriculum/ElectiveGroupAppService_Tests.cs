using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Academic.Curriculum;

public class ElectiveGroupAppService_Tests
	: ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly Guid TestTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

	private readonly IElectiveGroupAppService _electiveGroupAppService;
	private readonly IRepository<EducationMediumStage, Guid> _stageRepository;
	private readonly IRepository<Standard, Guid> _standardRepository;
	private readonly IRepository<Subject, Guid> _subjectRepository;
	private readonly IRepository<AssignedSubject, Guid> _assignedSubjectRepository;
	private readonly IRepository<ElectiveGroup, Guid> _electiveGroupRepository;
	private readonly StandardManager _standardManager;
	private readonly ICurrentTenant _currentTenant;

	public ElectiveGroupAppService_Tests()
	{
		_electiveGroupAppService = GetRequiredService<IElectiveGroupAppService>();
		_stageRepository = GetRequiredService<IRepository<EducationMediumStage, Guid>>();
		_standardRepository = GetRequiredService<IRepository<Standard, Guid>>();
		_subjectRepository = GetRequiredService<IRepository<Subject, Guid>>();
		_assignedSubjectRepository = GetRequiredService<IRepository<AssignedSubject, Guid>>();
		_electiveGroupRepository = GetRequiredService<IRepository<ElectiveGroup, Guid>>();
		_standardManager = GetRequiredService<StandardManager>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
	}

	[Fact]
	public async Task Usage_Should_Count_Distinct_Classes_Instead_Of_Mapped_Subject_Rows()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
				var stage = await _stageRepository.GetAsync(stageId);
				var unique = Guid.NewGuid().ToString("N");
				var standard = await _standardManager.CreateAsync(
					stageId,
					$"Elective Std {unique}"[..30],
					12,
					shiftId: null);
				await _standardRepository.InsertAsync(standard, autoSave: true);

				var firstSubject = await CreateSubjectAsync(stage.EducationMediumId, $"Core Maths {unique}"[..30]);
				var secondSubject = await CreateSubjectAsync(stage.EducationMediumId, $"Applied Maths {unique}"[..30]);
				var electiveGroup = new ElectiveGroup(
					Guid.NewGuid(),
					TestTenantId,
					$"Maths Elective {unique}"[..30],
					"#8E33FF",
					stage.EducationMediumId);
				electiveGroup.AddSubject(firstSubject.Id);
				electiveGroup.AddSubject(secondSubject.Id);
				await _electiveGroupRepository.InsertAsync(electiveGroup, autoSave: true);

				await _assignedSubjectRepository.InsertManyAsync(
					[
						CreateAssignment(standard.Id, firstSubject.Id, electiveGroup.Id, "MATH01"),
						CreateAssignment(standard.Id, secondSubject.Id, electiveGroup.Id, "MATH02")
					],
					autoSave: true);

				var usage = await _electiveGroupAppService.GetUsageDetailsAsync(
					stage.EducationMediumId,
					electiveGroup.Id);
				var listItem = (await _electiveGroupAppService.GetListAsync(stage.EducationMediumId))
					.Single(item => item.Id == electiveGroup.Id);

				usage.Subjects.Count.ShouldBe(2);
				usage.Usage.ClassAllocationsCount.ShouldBe(1);
				listItem.UsedInClassesCount.ShouldBe(1);
			}
		});
	}

	private async Task<Subject> CreateSubjectAsync(Guid mediumId, string name)
	{
		var subject = new Subject(Guid.NewGuid(), TestTenantId, name, "#00A76F", mediumId);
		await _subjectRepository.InsertAsync(subject, autoSave: true);
		return subject;
	}

	private static AssignedSubject CreateAssignment(
		Guid standardId,
		Guid subjectId,
		Guid electiveGroupId,
		string code) =>
		new(
			Guid.NewGuid(),
			TestTenantId,
			standardId,
			subjectId,
			code,
			SubjectStatus.Published,
			SubjectType.Elective,
			electiveGroupId);
}
