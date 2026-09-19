using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.EducationBoards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Academic.ClassManagement.StudentElectiveAssignment;

public class StudentElectiveAssignmentAppService_Tests
	: ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly Guid TestTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

	private readonly IStudentElectiveAssignmentAppService _appService;
	private readonly IRepository<ElectiveGroup, Guid> _electiveGroupRepository;
	private readonly IRepository<Subject, Guid> _subjectRepository;
	private readonly IRepository<StudentElectiveSelection, Guid> _selectionRepository;
	private readonly IRepository<EducationMediumStage, Guid> _stageRepository;
	private readonly ICurrentTenant _currentTenant;

	public StudentElectiveAssignmentAppService_Tests()
	{
		_appService = GetRequiredService<IStudentElectiveAssignmentAppService>();
		_electiveGroupRepository = GetRequiredService<IRepository<ElectiveGroup, Guid>>();
		_subjectRepository = GetRequiredService<IRepository<Subject, Guid>>();
		_selectionRepository = GetRequiredService<IRepository<StudentElectiveSelection, Guid>>();
		_stageRepository = GetRequiredService<IRepository<EducationMediumStage, Guid>>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
	}

	[Fact]
	public async Task Should_Delete_An_Existing_Selection_When_The_Subject_Is_Null()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
				var stage = await _stageRepository.GetAsync(stageId);
				var academicMediumId = stage.EducationMediumId;
				var studentId = Guid.NewGuid();
				var subject = new Subject(
					Guid.NewGuid(),
					TestTenantId,
					"Computer Science",
					"#2563EB",
					academicMediumId);
				var electiveGroup = new ElectiveGroup(
					Guid.NewGuid(),
					TestTenantId,
					"Technology",
					"#7C3AED",
					academicMediumId);
				electiveGroup.AddSubject(subject.Id);
				var selection = new StudentElectiveSelection(
					Guid.NewGuid(),
					TestTenantId,
					studentId,
					electiveGroup.Id,
					subject.Id,
					academicMediumId);

				await _subjectRepository.InsertAsync(subject, autoSave: true);
				await _electiveGroupRepository.InsertAsync(electiveGroup, autoSave: true);
				await _selectionRepository.InsertAsync(selection, autoSave: true);

				await _appService.SaveStudentElectiveAssignmentsAsync(
					new SaveStudentElectiveAssignmentsDto
					{
						AcademicMediumId = academicMediumId,
						ElectiveGroupId = electiveGroup.Id,
						Assignments = new List<StudentElectiveUpdateDto>
						{
							new() { StudentId = studentId, SelectedSubjectId = null }
						}
					});

				(await _selectionRepository.AnyAsync(item => item.Id == selection.Id)).ShouldBeFalse();
			}
		});
	}
}
