using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class LeaveTypeAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly ILeaveTypeAppService _leaveTypeAppService;
	private readonly IAcademicYearRepository _academicYearRepository;
	private readonly AcademicYearManager _academicYearManager;
	private readonly IUnitOfWorkManager _unitOfWorkManager;

	public LeaveTypeAppService_Tests()
	{
		_leaveTypeAppService = GetRequiredService<ILeaveTypeAppService>();
		_academicYearRepository = GetRequiredService<IAcademicYearRepository>();
		_academicYearManager = GetRequiredService<AcademicYearManager>();
		_unitOfWorkManager = GetRequiredService<IUnitOfWorkManager>();
	}

	[Fact]
	public async Task Should_Create_Leave_Type_With_Academic_Year()
	{
		var input = new LeaveTypeCreateRequest
		{
			Name = $"Sick-{Guid.NewGuid():N}",
			Color = "#28A745",
			Description = "Sick leave type"
		};

		var academicYearId = await GetAcademicYearIdAsync();
		var result = await _leaveTypeAppService.CreateLeaveTypeAsync(academicYearId, input);

		result.Id.ShouldNotBe(Guid.Empty);
		result.Name.ShouldBe(input.Name);
		result.Color.ShouldBe(input.Color);
	}

	[Fact]
	public async Task Should_Search_Leave_Types_By_Name()
	{
		var marker = $"Search-{Guid.NewGuid():N}";
		var academicYearId = await GetAcademicYearIdAsync();
		await _leaveTypeAppService.CreateLeaveTypeAsync(academicYearId, new LeaveTypeCreateRequest
		{
			Name = marker,
			Color = "#112233"
		});

		var list = await _leaveTypeAppService.GetListByAcademicYearIdAsync(
			academicYearId,
			page: 1,
			pageSize: 10,
			search: marker);

		list.Items.ShouldContain(x => x.Name == marker);
	}

	[Fact]
	public async Task Should_Not_Create_Duplicate_Name_In_Same_Academic_Year()
	{
		var duplicateName = $"Dup-{Guid.NewGuid():N}";
		var academicYearId = await GetAcademicYearIdAsync();
		await _leaveTypeAppService.CreateLeaveTypeAsync(academicYearId, new LeaveTypeCreateRequest
		{
			Name = duplicateName,
			Color = "#abcdef"
		});

		var ex = await Should.ThrowAsync<BusinessException>(() =>
			_leaveTypeAppService.CreateLeaveTypeAsync(academicYearId, new LeaveTypeCreateRequest
			{
				Name = duplicateName,
				Color = "#fedcba"
			}));

		ex.Code.ShouldBe("ShikshaMitra:LeaveTypeAlreadyExists");
	}

	private async Task<Guid> GetAcademicYearIdAsync()
	{
		using var uow = _unitOfWorkManager.Begin(requiresNew: true);
		var query = await _academicYearRepository.GetQueryableAsync();
		var academicYearId = query.Select(x => x.Id).FirstOrDefault();
		if (academicYearId == Guid.Empty)
		{
			var now = DateTime.UtcNow;
			var created = await _academicYearManager.CreateAsync(
				$"AY-{Guid.NewGuid():N}",
				new DateTime(now.Year, 4, 1),
				new DateTime(now.Year + 1, 3, 31));
			await _academicYearRepository.InsertAsync(created, autoSave: true);
			academicYearId = created.Id;
		}
		await uow.CompleteAsync();
		academicYearId.ShouldNotBe(Guid.Empty);
		return academicYearId;
	}
}
