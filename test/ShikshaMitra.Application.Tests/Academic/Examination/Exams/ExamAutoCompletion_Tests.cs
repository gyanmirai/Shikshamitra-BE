using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Academic.Examination.Exams;

public class ExamAutoCompletion_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task Completion_Should_Keep_Institution_Time_End_Boundaries_And_Date_Fallbacks()
	{
		var nowUtc = new DateTime(2026, 9, 16, 10, 0, 0, DateTimeKind.Utc); // 15:30 IST
		var today = new DateTime(2026, 9, 16);
		var tenantId = Guid.NewGuid();
		var repository = GetRequiredService<IExamRepository>();
		Exam Make(ExamStatus status, DateTime? date, DateTime? end)
		{
			var exam = new Exam(Guid.NewGuid(), Guid.NewGuid(), "Scheduled exam", Guid.NewGuid(), tenantId);
			exam.ChangeStatus(status);
			var subject = new ExamSubject(Guid.NewGuid(), exam.Id, Guid.NewGuid());
			subject.UpdateSchedule(date, date?.AddHours(9), end, 100, 33, null);
			exam.AddSubject(subject);
			return exam;
		}
		var before = Make(ExamStatus.Published, today, today.AddHours(15).AddMinutes(29));
		var exact = Make(ExamStatus.Pending, today, today.AddHours(15).AddMinutes(30));
		var after = Make(ExamStatus.Published, today, today.AddHours(15).AddMinutes(31));
		var dateOnlyPast = Make(ExamStatus.Pending, today.AddDays(-1), null);
		var dateOnlyToday = Make(ExamStatus.Published, today, null);
		var undated = Make(ExamStatus.Published, null, null);
		var ongoing = Make(ExamStatus.Ongoing, today.AddDays(-1), today.AddHours(9));
		var mixed = Make(ExamStatus.Published, today.AddDays(-1), today.AddHours(9));
		var undatedEnd = new ExamSubject(Guid.NewGuid(), mixed.Id, Guid.NewGuid());
		undatedEnd.UpdateSchedule(today.AddDays(2), null, null, 100, 33, null);
		mixed.AddSubject(undatedEnd); // Legacy worker uses stored end times when any are present.
		var exams = new List<Exam> { before, exact, after, dateOnlyPast, dateOnlyToday, undated, ongoing, mixed };
		using (GetRequiredService<ICurrentTenant>().Change(tenantId))
			await WithUnitOfWorkAsync(() => repository.InsertManyAsync(exams));

		var service = GetRequiredService<ExamAutoCompletionService>();
		(await service.CompleteDueExamsAsync(nowUtc)).ShouldBe(4);
		(await service.CompleteDueExamsAsync(nowUtc)).ShouldBe(0);
		using (GetRequiredService<ICurrentTenant>().Change(tenantId))
		{
			await WithUnitOfWorkAsync(async () =>
			{
				var saved = (await repository.GetListAsync()).ToDictionary(exam => exam.Id);
				foreach (var completed in new[] { before, exact, dateOnlyPast, mixed })
					saved[completed.Id].Status.ShouldBe(ExamStatus.Completed);
				saved[after.Id].Status.ShouldBe(ExamStatus.Published);
				saved[dateOnlyToday.Id].Status.ShouldBe(ExamStatus.Published);
				saved[undated.Id].Status.ShouldBe(ExamStatus.Published);
				saved[ongoing.Id].Status.ShouldBe(ExamStatus.Ongoing);
			});
		}
	}
}
