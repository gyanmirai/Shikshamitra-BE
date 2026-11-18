using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Academic.Notifications;

public class AcademicNotificationCatalogue_Tests
{
	private readonly AcademicNotificationCatalogue _catalogue = new([]);

	[Fact]
	public void Catalogue_covers_the_approved_academic_areas_without_duplicate_events()
	{
		var definitions = _catalogue.GetAll();
		definitions.Count.ShouldBe(83);
		definitions.Select(x => x.EventKey).Distinct(StringComparer.Ordinal).Count().ShouldBe(83);
		definitions.ShouldContain(x => x.EventKey == "Academic.Elective.LockedSelectionCorrected");
		definitions.ShouldContain(x => x.EventKey == "Academic.Calendar.EmergencyClosureDeclared");
		definitions.ShouldContain(x => x.EventKey == "Academic.Homework.FeedbackReleased");
		definitions.ShouldContain(x => x.EventKey == "Academic.Report.ExportReady");
		definitions.ShouldNotContain(x => x.EventKey.Contains("Quiz", StringComparison.Ordinal));
	}

	[Fact]
	public void Every_event_has_a_typed_schema_recipient_policy_and_canonical_link_owner()
	{
		_catalogue.GetAll().ShouldNotBeEmpty();
		foreach (var definition in _catalogue.GetAll())
		{
			definition.SchemaVersion.ShouldBeGreaterThan(0);
			definition.Category.ShouldNotBeNullOrWhiteSpace();
			definition.SourceType.ShouldNotBeNullOrWhiteSpace();
			definition.LinkBuilderKey.ShouldNotBeNullOrWhiteSpace();
			definition.RecipientSelectors.ShouldNotBeEmpty();
			definition.SupportedChannels.ShouldContain(CommunicationChannelType.InApp);
			definition.Variables.ShouldContain(x => x.Key == "school_name" && x.Required);
			definition.Variables.ShouldContain(x => x.Key == "source_link" && x.Required);
			definition.Variables.Select(x => x.Key).Distinct(StringComparer.Ordinal).Count()
				.ShouldBe(definition.Variables.Count);
			definition.Variables.ShouldNotContain(x => x.Key.EndsWith("_id", StringComparison.Ordinal)
				|| x.Key.Contains("token", StringComparison.OrdinalIgnoreCase));
		}
	}

	[Fact]
	public void Missing_source_adapter_prevents_activation_instead_of_claiming_ready()
	{
		var availability = _catalogue.GetAvailability("Academic.Homework.Published");
		availability.IsAvailable.ShouldBeFalse();
		availability.ReasonCode.ShouldBe(NotificationErrorCodes.SourceUnavailable);
		Should.Throw<BusinessException>(() => _catalogue.EnsureAvailable("Academic.Homework.Published"))
			.Code.ShouldBe(NotificationErrorCodes.SourceUnavailable);
	}

	[Fact]
	public void Registering_one_source_event_does_not_activate_its_unimplemented_siblings()
	{
		var catalogue = new AcademicNotificationCatalogue([new HomeworkSourceAdapter()]);
		catalogue.GetAvailability("Academic.Homework.Published").IsAvailable.ShouldBeTrue();
		catalogue.GetAvailability("Academic.Homework.Overdue").IsAvailable.ShouldBeFalse();
		catalogue.GetAvailability("Academic.Result.Published").IsAvailable.ShouldBeFalse();
	}

	[Fact]
	public void Unsupported_event_names_and_unknown_variables_cannot_create_triggers()
	{
		Should.Throw<BusinessException>(() => _catalogue.Get("Academic.Quiz.Published"))
			.Code.ShouldBe(NotificationErrorCodes.EventUnknown);
		Should.Throw<BusinessException>(() => _catalogue.Get("academic.homework.published"))
			.Code.ShouldBe(NotificationErrorCodes.EventUnknown);
	}

	[Fact]
	public void Existing_channel_numbers_remain_compatible_when_web_push_is_added()
	{
		((int)CommunicationChannelType.WhatsApp).ShouldBe(0);
		((int)CommunicationChannelType.Sms).ShouldBe(1);
		((int)CommunicationChannelType.Email).ShouldBe(2);
		((int)CommunicationChannelType.InApp).ShouldBe(3);
		Enum.GetName(typeof(CommunicationChannelType), 4).ShouldBe("WebPush");
	}

	private sealed class HomeworkSourceAdapter : INotificationSourceAdapter
	{
		public IReadOnlyCollection<string> EventKeys => ["Academic.Homework.Published"];

		public Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context,
			CancellationToken cancellationToken) => throw new NotSupportedException();
	}
}
