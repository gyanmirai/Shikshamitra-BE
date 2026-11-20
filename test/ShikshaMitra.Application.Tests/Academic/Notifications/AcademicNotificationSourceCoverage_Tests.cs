using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Communication.Notifications;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using static ShikshaMitra.Communication.Notifications.AcademicNotificationEventKeys;

namespace ShikshaMitra.Academic.Notifications;

public sealed class AcademicNotificationSourceCoverage_Tests(ITestOutputHelper output)
	: ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly (string Reason, string[] Events)[] PendingSources =
	[
		("Elective preference windows require saved opening and due dates.", [ElectivePreferenceOpened, ElectivePreferenceDue]),
		("Lesson plan deadlines require a persisted submission policy.", [LessonPlanSubmissionDue]),
		("Published learning task changes need an audience revision and withdrawal source.",
			[HomeworkChanged, HomeworkWithdrawn, AssignmentChanged, AssignmentWithdrawn]),
		("The grading workflow has no saved reminder deadline.", [EvaluationGradingDue]),
		("Exam approval decisions need a review lifecycle.", [ExamApprovalRequested, ExamApproved, ExamChangesRequested]),
		("Exam schedule changes need saved reschedule and cancellation audiences.", [ExamRescheduled, ExamCancelled]),
		("A seating change needs an audience snapshot of the earlier release.", [SeatingChanged]),
		("Exam duties need an assignment and reminder lifecycle.", [ExamDutyAssigned, ExamDutyChanged, ExamDutyReminder]),
		("Marks entry opening and deadline need a saved release and timing policy.", [MarksEntryOpened, MarksEntryDue]),
		("Result review needs a separate review transition.", [ResultReadyForReview]),
		("A released correction already uses the result publication notice with correction content.", [ResultCorrected]),
		("Student follow-ups need a persisted, authorized follow-up workflow.", [Student360FollowUpDue])
	];

	[Fact]
	public void Registered_academic_events_have_a_source_and_history_policy_and_unavailable_events_have_a_reason()
	{
		var catalogue = GetRequiredService<AcademicNotificationCatalogue>();
		var events = catalogue.GetAll();
		var sources = ServiceProvider.GetServices<INotificationSourceAdapter>()
			.SelectMany(source => source.EventKeys.Select(key => (Key: key, Adapter: source.GetType().Name)))
			.ToLookup(row => row.Key, row => row.Adapter, StringComparer.Ordinal);
		var historyPolicies = ServiceProvider.GetServices<IAcademicNotificationSourceAuthorizer>()
			.SelectMany(policy => policy.EventKeys).ToLookup(key => key, StringComparer.Ordinal);
		var schedulers = ServiceProvider.GetServices<INotificationDueSource>().ToArray();
		var gaps = PendingSources.SelectMany(group => group.Events.Select(key => (Key: key, group.Reason)))
			.ToDictionary(x => x.Key, x => x.Reason, StringComparer.Ordinal);

		var unavailable = events.Where(x => !catalogue.GetAvailability(x.EventKey).IsAvailable).Select(x => x.EventKey);
		unavailable.Order(StringComparer.Ordinal).ShouldBe(gaps.Keys.Order(StringComparer.Ordinal));
		using var manifest = typeof(AcademicNotificationSourceCoverage_Tests).Assembly.GetManifestResourceStream("AcademicNotificationCoverage.md");
		using var reader = new StreamReader(manifest.ShouldNotBeNull());
		var documented = reader.ReadToEnd().Split('\n')
			.Where(line => line.StartsWith("| `Academic.", StringComparison.Ordinal))
			.SelectMany(line => line.Split('|')[1].Split('`').Where(value => value.StartsWith("Academic.", StringComparison.Ordinal)));
		documented.Order(StringComparer.Ordinal).ShouldBe(events
			.Where(row => catalogue.GetAvailability(row.EventKey).IsAvailable).Select(row => row.EventKey).Order(StringComparer.Ordinal));
		foreach (var definition in events)
		{
			var key = definition.EventKey;
			if (gaps.TryGetValue(key, out var reason))
			{
				reason.ShouldNotBeNullOrWhiteSpace();
				catalogue.GetAvailability(key).ReasonCode.ShouldBe(NotificationErrorCodes.SourceUnavailable, key);
				catalogue.GetManualAvailability(key).IsAvailable.ShouldBeFalse(key);
				continue;
			}

			sources[key].Count().ShouldBe(1, key);
			historyPolicies[key].Count().ShouldBe(1, key);
			if (definition.TriggerType == NotificationTriggerType.Due)
				schedulers.Any(x => x.SourceType == definition.SourceType && x.ScheduledEventKeys.Contains(key)).ShouldBeTrue(key);
			output.WriteLine($"{key} | {definition.TriggerType} | {sources[key].Single()} | schema {definition.SchemaVersion}");
		}
	}
}
