using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.BackgroundWork;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Academic.Examination.Exams;

public class ExamAutoCompletionService(
	IExamRepository examRepository,
	AcademicBackgroundBatchRunner batchRunner) : DomainService
{
	public Task<int> CompleteDueExamsAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
	{
		var timeZone = GetInstitutionTimeZone();
		var today = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timeZone).Date;
		return batchRunner.RunAsync(examRepository,
			exam => (exam.Status == ExamStatus.Pending || exam.Status == ExamStatus.Published) &&
				exam.ExamSubjects.Any(subject => subject.ExamDate.HasValue && subject.ExamDate.Value.Date <= today),
			exam =>
			{
				if (exam.Status is not (ExamStatus.Pending or ExamStatus.Published) || !HasEnded(exam, nowUtc, timeZone))
					return 0;
				exam.ChangeStatus(ExamStatus.Completed);
				return 1;
			}, cancellationToken);
	}

	private static bool HasEnded(Exam exam, DateTime nowUtc, TimeZoneInfo timeZone)
	{
		var subjectsWithEndTime = exam.ExamSubjects.Where(subject => subject.ExamDate.HasValue && subject.EndTime.HasValue).ToList();
		if (subjectsWithEndTime.Count > 0)
		{
			var finalEndAtUtc = subjectsWithEndTime.Max(subject => TimeZoneInfo.ConvertTimeToUtc(
				DateTime.SpecifyKind(subject.ExamDate!.Value.Date.Add(subject.EndTime!.Value.TimeOfDay), DateTimeKind.Unspecified), timeZone));
			return finalEndAtUtc <= nowUtc;
		}
		var dates = exam.ExamSubjects.Where(subject => subject.ExamDate.HasValue).Select(subject => subject.ExamDate!.Value.Date).ToList();
		return dates.Count > 0 && dates.Max() < TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timeZone).Date;
	}

	private static TimeZoneInfo GetInstitutionTimeZone()
	{
		try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }
		catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"); }
	}
}
