using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.SeatAllocation.Dtos;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Academic.Examination.SeatAllocation;

public class ExamSeatAllocationAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly Guid TestTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

	private readonly IExamSeatAllocationAppService _seatAllocationAppService;
	private readonly IRepository<EducationMediumStage, Guid> _stageRepository;
	private readonly IRepository<Standard, Guid> _standardRepository;
	private readonly IRepository<Division, Guid> _divisionRepository;
	private readonly IRepository<Subject, Guid> _subjectRepository;
	private readonly IRepository<Exam, Guid> _examRepository;
	private readonly StandardManager _standardManager;
	private readonly DivisionManager _divisionManager;
	private readonly ExamTypeManager _examTypeManager;
	private readonly ExamManager _examManager;
	private readonly ICurrentTenant _currentTenant;

	public ExamSeatAllocationAppService_Tests()
	{
		_seatAllocationAppService = GetRequiredService<IExamSeatAllocationAppService>();
		_stageRepository = GetRequiredService<IRepository<EducationMediumStage, Guid>>();
		_standardRepository = GetRequiredService<IRepository<Standard, Guid>>();
		_divisionRepository = GetRequiredService<IRepository<Division, Guid>>();
		_subjectRepository = GetRequiredService<IRepository<Subject, Guid>>();
		_examRepository = GetRequiredService<IRepository<Exam, Guid>>();
		_standardManager = GetRequiredService<StandardManager>();
		_divisionManager = GetRequiredService<DivisionManager>();
		_examTypeManager = GetRequiredService<ExamTypeManager>();
		_examManager = GetRequiredService<ExamManager>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
	}

	[Fact]
	public async Task GetDivisionsForExamAsync_Should_Default_To_Exam_Standard_When_StandardId_Is_Not_Provided()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var (exam, division) = await CreateScheduledExamAsync();

				var result = await _seatAllocationAppService.GetDivisionsForExamAsync(
					exam.Id,
					new GetExamDivisionsInput
					{
						StandardId = Guid.Empty
					});

				result.TotalCount.ShouldBe(1);
				result.Items.Select(item => item.Id).ShouldContain(division.Id);
				result.Items.Single().Name.ShouldBe(division.Name);
			}
		});
	}

	private async Task<(Exam exam, Division division)> CreateScheduledExamAsync()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
			var stage = await _stageRepository.GetAsync(stageId);
			var unique = Guid.NewGuid().ToString("N");

			var standard = await _standardManager.CreateAsync(
				stageId,
				$"Seat Std {unique}"[..30],
				6,
				shiftId: null);
			await _standardRepository.InsertAsync(standard, autoSave: true);

			var division = await _divisionManager.CreateAsync(
				standard.Id,
				$"A-{unique}"[..30],
				30);
			await _divisionRepository.InsertAsync(division, autoSave: true);

			var subject = new Subject(
				Guid.NewGuid(),
				TestTenantId,
				$"Math {unique}"[..30],
				"#00A76F",
				stage.EducationMediumId);
			await _subjectRepository.InsertAsync(subject, autoSave: true);

			var examType = await _examTypeManager.CreateAsync(
				$"Seat Exam {unique}"[..30],
				description: null,
				stage.EducationMediumId);

			var exam = _examManager.Create(
				examType.Id,
				$"Seating Plan Exam {unique}"[..40],
				standard.Id,
				new List<Guid> { division.Id },
				new List<Guid> { subject.Id });
			exam.ChangeStatus(ExamStatus.Published);
			await _examRepository.InsertAsync(exam, autoSave: true);

			return (exam, division);
		}
	}
}
