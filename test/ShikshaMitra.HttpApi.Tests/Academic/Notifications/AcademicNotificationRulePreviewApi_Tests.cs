using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.ValueObjects;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Academic.Notifications;

[CollectionDefinition(Name)]
public class AcademicNotificationPreviewApiCollection : ICollectionFixture<AcademicNotificationPreviewApiFactory>
{
	public const string Name = "Academic rule preview";
}

public class AcademicNotificationPreviewApiFactory : ShikshaMitraApiTestFactory
{
	internal const string Event = AcademicNotificationEventKeys.HomeworkPublished;
	internal static readonly Guid SourceId = Guid.NewGuid();
	internal static readonly Guid TenantId = Guid.Parse(TestAuthHandler.TestTenantId);
	internal static readonly Guid StudentId = Guid.NewGuid();
	internal static readonly Guid SiblingDivision = Guid.NewGuid();

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
		{
			["App:ClientUrl"] = "https://school.example.invalid"
		}));
		builder.ConfigureTestServices(services =>
		{
			var source = new PreviewSource(this);
			services.AddSingleton<INotificationSourceAdapter>(source);
			services.AddSingleton<IAcademicNotificationSourceAuthorizer>(source);
			services.Replace(ServiceDescriptor.Singleton<IAcademicNotificationRecipientProjection>(new PreviewRecipients(this)));
		});
	}

	private sealed class PreviewSource(AcademicNotificationPreviewApiFactory factory)
		: INotificationSourceAdapter, IAcademicNotificationSourceAuthorizer
	{
		public IReadOnlyCollection<string> EventKeys => [Event];

		public Task EnsureCanSendAsync(string eventKey, Guid sourceId, CancellationToken cancellationToken)
		{
			if (sourceId != SourceId) throw new AbpAuthorizationException();
			return Task.CompletedTask;
		}

		public Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken cancellationToken)
		{
			context.TenantId.ShouldBe(TenantId);
			context.SourceId.ShouldBe(SourceId);
			var scoped = new NotificationSourceScopeDto(factory.SeededAcademicYearId, factory.SeededBoardId, factory.SeededMediumId,
				factory.SeededStandardId, factory.SeededDivisionId);
			var child = new NotificationRecipientRefDto("Student", StudentId);
			var sibling = new NotificationRecipientRefDto("Student", Guid.NewGuid());
			var definition = new AcademicNotificationCatalogue([]).Get(Event);
			var variables = definition.Variables.ToDictionary(x => x.Key, x => new NotificationVariableValueDto(x.Type, x.Type switch
			{
				NotificationVariableType.Date => "2026-12-01",
				NotificationVariableType.DateTime => "2026-12-01T12:00:00+00:00",
				NotificationVariableType.Number => "2",
				_ => "Private source text"
			}));
			return Task.FromResult(new NotificationSourceSnapshot(true, null, "homework-v1", "/academic/homework",
				variables, [child, sibling], AudienceSlices: [new(scoped, [child]), new(scoped with { DivisionId = SiblingDivision }, [sibling])]));
		}
	}

	private sealed class PreviewRecipients(AcademicNotificationPreviewApiFactory factory) : IAcademicNotificationRecipientProjection
	{
		public Task<AcademicNotificationRecipientData> LoadAsync(Guid tenantId, IReadOnlyList<NotificationRecipientRefDto> sourceRecipients,
			CancellationToken cancellationToken)
		{
			tenantId.ShouldBe(TenantId);
			sourceRecipients.Select(x => x.StudentId).ShouldBe([StudentId]);
			var student = new Student(StudentId, TenantId, "Private", null, "Child", factory.SeededAcademicYearId,
				factory.SeededBoardId, factory.SeededMediumId, factory.SeededMediumStageId, factory.SeededStandardId,
				factory.SeededDivisionId, status: StudentLifeCycleStatusEnum.Enrolled);
			student.UpdateFamilyInfo(Profile("Father"), Profile("Mother"), null, []);
			StudentFamilyContactSelection.Select(student, "mother");
			return Task.FromResult(new AcademicNotificationRecipientData([new(student, student.FamilyDetail, false,
				[Link("guardian", "mother"), Link("guardian", "father"), Link("student", "self")])], []));
		}

		private static AcademicNotificationAccount Link(string accountType, string relationship)
		{
			var user = new IdentityUser(Guid.NewGuid(), relationship, "private@example.invalid", TenantId);
			return new(new StudentPortalAccountLink(Guid.NewGuid(), TenantId, StudentId, user.Id, accountType, relationship, Guid.NewGuid()), user);
		}

		private static ParentGuardianDetails Profile(string name) =>
			new(name, null, null, "private@example.invalid", null, null, null, null, null, null, null, null, null);
	}
}

[Collection(AcademicNotificationPreviewApiCollection.Name)]
public class AcademicNotificationRulePreviewApi_Tests(AcademicNotificationPreviewApiFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string BasePath = "/api/academic/notifications";
	private const string Event = AcademicNotificationPreviewApiFactory.Event;
	private const string PreviewPath = BasePath + "/rules/" + Event + "/preview";
	private const string Templates = BasePath + "/templates";
	private const string Add = ShikshaMitraSubmodulePermissions.Academics.Notification.Add;
	private const string View = ShikshaMitraSubmodulePermissions.Academics.Notification.View;

	[Fact]
	public async Task Preview_counts_only_the_selected_division_and_renders_a_synthetic_sample_without_queuing()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		Client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, $"{Add},{View}");
		await PostAndReadAsync<int>($"{Templates}/initialize-defaults", new { eventKey = Event });
		var page = await GetAndReadAsync<PagedResultDto<NotificationTemplateDto>>($"{Templates}?eventKey={Event}&maxResultCount=100");
		var draft = page.Items.Single(x => x.RecipientKind == "Guardian" && x.Channel == CommunicationChannelType.InApp);
		await PostAndReadAsync<NotificationTemplateDto>($"{Templates}/{draft.TemplateId}/publish", new
		{
			revisionId = draft.RevisionId, concurrencyStamp = draft.ConcurrencyStamp
		});

		using var response = await Client.PostAsJsonAsync(PreviewPath, Request([draft.TemplateId]));
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		response.Headers.CacheControl?.NoStore.ShouldBe(true);
		var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
		result.GetProperty("eligibleRecipientCount").GetInt32().ShouldBe(1);
		result.GetProperty("channels")[0].GetProperty("readyDeliveryCount").GetInt32().ShouldBe(1);
		result.GetProperty("samples")[0].GetProperty("isSynthetic").GetBoolean().ShouldBeTrue();
		var content = await response.Content.ReadAsStringAsync();
		content.ShouldNotContain("Private source text");
		content.ShouldNotContain("private@example.invalid");
		content.ShouldNotContain(AcademicNotificationPreviewApiFactory.StudentId.ToString());
		await AssertNoQueuedWorkAsync();
	}

	[Fact]
	public async Task Draft_preview_uses_only_the_add_action_and_counts_explicit_family_options_without_templates()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		Client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, Add);
		var result = await PostAndReadAsync<JsonElement>(PreviewPath, Request([], includeAdditionalFamily: true));
		result.GetProperty("eligibleRecipientCount").GetInt32().ShouldBe(3);
		result.GetProperty("channels")[0].GetProperty("readyDeliveryCount").GetInt32().ShouldBe(0);
		result.GetProperty("blockingIssues").EnumerateArray().Select(x => x.GetString()).ShouldContain(NotificationErrorCodes.TemplateInvalid);
		await AssertNoQueuedWorkAsync();
	}

	[Theory]
	[InlineData(Add, true)]
	[InlineData(View, false)]
	public async Task Preview_requires_configuration_permission_and_the_source_guard(string permission, bool deniedSource)
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		Client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, permission);
		using var response = await Client.PostAsJsonAsync(PreviewPath, Request([], sourceId: deniedSource ? Guid.NewGuid() : null));
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	private object Request(Guid[] templateIds, bool includeAdditionalFamily = false, Guid? sourceId = null) => new
	{
		sourceId = sourceId ?? AcademicNotificationPreviewApiFactory.SourceId,
		sourceType = "Academic.Homework",
		rule = new
		{
			eventKey = Event,
			scope = new AcademicNotificationScopeDto(Factory.SeededAcademicYearId, Factory.SeededBoardId, Factory.SeededMediumId,
				Factory.SeededStandardId, Factory.SeededDivisionId),
			templateIds,
			guardianMode = includeAdditionalFamily ? NotificationGuardianMode.PrimaryAndSelected : NotificationGuardianMode.PrimaryOnly,
			additionalGuardianRelationships = includeAdditionalFamily ? new[] { "father" } : Array.Empty<string>(),
			includeStudentAccount = includeAdditionalFamily
		}
	};

	private async Task AssertNoQueuedWorkAsync()
	{
		using var serviceScope = Factory.Services.CreateScope();
		var services = serviceScope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(AcademicNotificationPreviewApiFactory.TenantId);
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin();
		var db = await services.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		(await db.NotificationIntents.AnyAsync(x => x.SourceId == AcademicNotificationPreviewApiFactory.SourceId)).ShouldBeFalse();
		(await db.CommunicationCampaigns.AnyAsync(x => x.SourceEntityId == AcademicNotificationPreviewApiFactory.SourceId)).ShouldBeFalse();
		(await db.NotificationRules.AnyAsync(x => x.EventKey == Event)).ShouldBeFalse();
		await unit.CompleteAsync();
	}
}
