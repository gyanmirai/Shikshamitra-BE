using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Curriculum.Settings;
using Shouldly;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Xunit;

namespace ShikshaMitra.Academic.Mobile;

public partial class MobileAcademicAppService_Tests
{
	[Theory]
	[InlineData(false, "08:39:59", "upcoming")]
	[InlineData(true, "08:40:00", "ongoing")]
	[InlineData(false, "09:30:00", "ongoing")]
	[InlineData(true, "09:30:00.001", "completed")]
	public Task Exam_status_uses_school_clock_and_matches_list_detail_and_filters(bool parent, string utcTime, string expected) => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, parent); using var identity = SignIn(f.User.Id);
		var today = MobileAcademicContextAppService.SchoolToday;
		f.Exam.ExamSubjects.Single().UpdateSchedule(today, today.AddHours(14).AddMinutes(10), today.AddHours(15), 100, 35, "");
		f.Exam.ChangeStatus(ExamStatus.Published);
		await GetRequiredService<IRepository<Exam, Guid>>().UpdateAsync(f.Exam, true);
		var service = GetRequiredService<MobileExamAppService>();
		FreezeExamClock(service, DateTime.Parse($"{today:yyyy-MM-dd}T{utcTime}Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal));
		var input = new MobileAcademicInput { StudentId = f.Student.Id, AcademicYearId = f.Year.Id };
		var detail = await service.GetAsync(f.Exam.Id, input);
		detail.Status.ShouldBe(expected);
		detail.ResultStatus.ShouldBe("pending");
		detail.Schedule.Single().StartTime!.Value.Hour.ShouldBe(14);
		var list = await service.GetListAsync(new() { StudentId = f.Student.Id, AcademicYearId = f.Year.Id, Status = expected });
		list.Items.Single().Id.ShouldBe(f.Exam.Id);
		list.Items.Single().Status.ShouldBe(detail.Status);
		(await service.GetListAsync(new() { StudentId = f.Student.Id, AcademicYearId = f.Year.Id,
			Status = expected == "upcoming" ? "ongoing" : "upcoming" })).Items.ShouldBeEmpty();
	});

	[Fact]
	public Task Undated_published_exam_is_not_given_a_synthetic_year_end_schedule() => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, false); using var identity = SignIn(f.User.Id);
		f.Exam.ExamSubjects.Single().UpdateSchedule(null, null, null, 100, 35, "");
		f.Exam.ChangeStatus(ExamStatus.Published);
		await GetRequiredService<IRepository<Exam, Guid>>().UpdateAsync(f.Exam, true);
		(await GetRequiredService<IMobileExamAppService>().GetListAsync(new()
			{ StudentId = f.Student.Id, AcademicYearId = f.Year.Id })).Items.ShouldBeEmpty();
	});

	private void FreezeExamClock(MobileExamAppService service, DateTime utcNow)
	{
		var clock = Substitute.For<IClock>();
		clock.Now.Returns(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));
		service.LazyServiceProvider = new AbpLazyServiceProvider(new ExamClockProvider(ServiceProvider, clock));
	}

	[Fact]
	public Task Exam_status_uses_full_window_without_exposing_unassigned_papers() => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, false); using var identity = SignIn(f.User.Id);
		var today = MobileAcademicContextAppService.SchoolToday;
		var other = await Insert(new Subject(Guid.NewGuid(), tenantId, "Unassigned paper", "#008967", f.Student.AcademicMediumId));
		f.Exam.AddSubject(new ExamSubject(Guid.NewGuid(), f.Exam.Id, other.Id, today.AddDays(1), today.AddHours(9), today.AddHours(10), 100, 35));
		f.Exam.ChangeStatus(ExamStatus.Published);
		await GetRequiredService<IRepository<Exam, Guid>>().UpdateAsync(f.Exam, true);
		var service = GetRequiredService<MobileExamAppService>();
		FreezeExamClock(service, DateTime.SpecifyKind(today.AddHours(18).AddMinutes(31), DateTimeKind.Utc));
		var result = await service.GetAsync(f.Exam.Id, new() { StudentId = f.Student.Id, AcademicYearId = f.Year.Id });
		result.Status.ShouldBe("ongoing");
		result.Schedule.Single().SubjectId.ShouldBe(f.SubjectId);
	});

	private sealed class ExamClockProvider(IServiceProvider provider, IClock clock) : IServiceProvider
	{
		public object? GetService(Type serviceType) => serviceType == typeof(IClock) ? clock : provider.GetService(serviceType);
	}
}
