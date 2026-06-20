using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common;
using ShikshaMitra.Application.Contracts.Groups;
using ShikshaMitra.Communication;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Groups;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.EducationBoards;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Communication;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Communication)]
public class CommunicationCampaignApi_Tests : ShikshaMitraApiTestBase
{
	private const string CampaignsUrl = "/api/communication/campaigns";
	private const string ProviderSettingsUrl = "/api/communication/provider-settings";
	private const string ApprovalRulesUrl = "/api/communication/approval-rules";
	private const string RepliesUrl = "/api/communication/replies";

	public CommunicationCampaignApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Campaign_Publish_Should_Require_Approval_Then_Create_Delivery_And_Usage_Ledgers()
	{
		await CreateProviderAsync(CommunicationChannelType.WhatsApp);
		await CreateProviderAsync(CommunicationChannelType.Email);

		var recipientId = Guid.NewGuid();
		var audience = new CommunicationAudienceDefinitionDto
		{
			ParentsFirst = true,
			ExplicitRecipients =
			{
				new CommunicationExplicitRecipientDto
				{
					RecipientType = "ParentGuardian",
					RecipientId = recipientId,
					DisplayName = "Aarav Parent",
					Email = "parent@example.test",
					MobileNumber = "+919999999999",
					PreferredLanguageCode = "en",
					IsParentDefault = true
				}
			}
		};

		var preview = await PostAndReadAsync<CommunicationAudiencePreviewDto>(
			$"{CampaignsUrl}/audience/preview",
			audience);
		preview.TotalRecipientCount.ShouldBe(1);
		preview.EligibleRecipientCount.ShouldBe(1);
		preview.ParentsFirst.ShouldBeTrue();

		var campaign = await PostAndReadAsync<CommunicationCampaignDto>(
			CampaignsUrl,
			new CreateUpdateCommunicationCampaignDto
			{
				AcademicMediumId = Factory.SeededMediumId,
				CampaignType = "Notification",
				SourceModule = CommunicationSourceModule.Student,
				Title = $"Fee reminder {Guid.NewGuid():N}",
				Subject = "Fee reminder",
				Body = "Please review the fee reminder link.",
				ImportanceLevel = CommunicationImportanceLevel.Urgent,
				ApprovalRequired = true,
				AudienceDefinition = audience,
				Channels = { CommunicationChannelType.WhatsApp, CommunicationChannelType.Email },
				FallbackOrder = { CommunicationChannelType.WhatsApp, CommunicationChannelType.Email },
				LinkUrl = "https://school.example.test/fees/reminder"
			});
		campaign.Status.ShouldBe(CommunicationCampaignStatus.PendingApproval);
		campaign.RecipientCount.ShouldBe(1);

		var blockedPublish = await Client.PutAsync($"{CampaignsUrl}/{campaign.Id}/publish", content: null);
		blockedPublish.StatusCode.ShouldNotBe(HttpStatusCode.OK);

		var approvedResponse = await Client.PutAsJsonAsync(
			$"{CampaignsUrl}/{campaign.Id}/approve",
			new CommunicationApprovalDto { Reason = "Checked by communication approver" },
			JsonOptions);
		approvedResponse.EnsureSuccessStatusCode();

		var publishResponse = await Client.PutAsync($"{CampaignsUrl}/{campaign.Id}/publish", content: null);
		publishResponse.EnsureSuccessStatusCode();
		var published = await publishResponse.Content.ReadFromJsonAsync<CommunicationCampaignDto>(JsonOptions)
			?? throw new InvalidOperationException("Published campaign response was empty.");

		published.Status.ShouldBe(CommunicationCampaignStatus.Queued);
		published.RecipientCount.ShouldBe(1);
		published.QueuedCount.ShouldBe(2);
		published.FailedCount.ShouldBe(0);

		var attempts = await GetAndReadAsync<PagedResultDto<DeliveryAttemptDto>>(
			$"/api/communication/delivery-attempts?campaignId={campaign.Id}&maxResultCount=20");
		attempts.TotalCount.ShouldBe(2);
		attempts.Items.Select(x => x.Channel).ShouldContain(CommunicationChannelType.WhatsApp);
		attempts.Items.Select(x => x.Channel).ShouldContain(CommunicationChannelType.Email);
		attempts.Items.All(x => x.Status == CommunicationDeliveryStatus.Queued).ShouldBeTrue();

		var usage = await GetAndReadAsync<PagedResultDto<UsageLedgerEntryDto>>(
			$"/api/communication/usage-ledger?campaignId={campaign.Id}&maxResultCount=20");
		usage.TotalCount.ShouldBe(2);
		usage.Items.Sum(x => x.AttemptCount).ShouldBe(2);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Publish_Should_Block_When_Channel_Provider_Is_Missing()
	{
		await DisableProvidersAsync(CommunicationChannelType.Sms);

		var campaign = await CreateCampaignAsync(
			"Notification",
			CommunicationSourceModule.Manual,
			new CommunicationAudienceDefinitionDto
			{
				ExplicitRecipients =
				{
					new CommunicationExplicitRecipientDto
					{
						DisplayName = "Missing Provider Parent",
						MobileNumber = "+919888888888"
					}
				}
			},
			CommunicationChannelType.Sms);

		var publishResponse = await Client.PutAsync($"{CampaignsUrl}/{campaign.Id}/publish", content: null);
		publishResponse.StatusCode.ShouldNotBe(HttpStatusCode.OK);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Consent_Required_Provider_Should_Skip_Unconsented_Recipients_And_Record_Ledgers()
	{
		await CreateProviderAsync(CommunicationChannelType.Sms, consentRequired: true);
		var destination = $"+91{Random.Shared.Next(700000000, 999999999)}";

		var campaign = await CreateCampaignAsync(
			"Notification",
			CommunicationSourceModule.Student,
			new CommunicationAudienceDefinitionDto
			{
				ExplicitRecipients =
				{
					new CommunicationExplicitRecipientDto
					{
						DisplayName = "Consent Pending Parent",
						MobileNumber = destination
					}
				}
			},
			CommunicationChannelType.Sms);

		var published = await PublishAsync(campaign.Id);
		published.Status.ShouldBe(CommunicationCampaignStatus.Failed);
		published.FailedCount.ShouldBe(1);

		var attempts = await GetAttemptsAsync(campaign.Id);
		attempts.TotalCount.ShouldBe(1);
		attempts.Items[0].Status.ShouldBe(CommunicationDeliveryStatus.Skipped);
		attempts.Items[0].ErrorCode.ShouldBe("CONSENT_REQUIRED");

		var usage = await GetUsageAsync(campaign.Id);
		usage.TotalCount.ShouldBe(1);
		usage.Items[0].AttemptCount.ShouldBe(1);
		usage.Items[0].FailedCount.ShouldBe(1);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Approval_Rule_Should_Block_Publish_Until_Authorized_Approval()
	{
		await CreateProviderAsync(CommunicationChannelType.InApp);

		var ruleResponse = await Client.PostAsJsonAsync(
			ApprovalRulesUrl,
			new CreateUpdateCommunicationApprovalRuleDto
			{
				RuleName = $"Urgent audience approval {Guid.NewGuid():N}",
				IsEnabled = true,
				CampaignType = "Notification",
				SourceModule = CommunicationSourceModule.Student,
				Channel = CommunicationChannelType.InApp,
				MinimumAudienceSize = 1,
				ImportanceLevel = CommunicationImportanceLevel.Urgent,
				RequiresApproval = true
			},
			JsonOptions);
		ruleResponse.EnsureSuccessStatusCode();

		var campaign = await CreateCampaignAsync(
			"Notification",
			CommunicationSourceModule.Student,
			new CommunicationAudienceDefinitionDto
			{
				ExplicitRecipients =
				{
					new CommunicationExplicitRecipientDto
					{
						DisplayName = "Rule Recipient",
						UserId = Guid.NewGuid()
					}
				}
			},
			CommunicationChannelType.InApp,
			CommunicationImportanceLevel.Urgent);

		campaign.ApprovalRequired.ShouldBeTrue();
		campaign.Status.ShouldBe(CommunicationCampaignStatus.PendingApproval);

		var blockedPublish = await Client.PutAsync($"{CampaignsUrl}/{campaign.Id}/publish", content: null);
		blockedPublish.StatusCode.ShouldNotBe(HttpStatusCode.OK);

		await ApproveAsync(campaign.Id);
		var published = await PublishAsync(campaign.Id);
		published.Status.ShouldBe(CommunicationCampaignStatus.Queued);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Published_Campaign_Delete_Should_Be_Blocked_And_Cancel_Should_Retain_Ledgers()
	{
		await CreateProviderAsync(CommunicationChannelType.InApp);
		var campaign = await CreateCampaignAsync(
			"Notification",
			CommunicationSourceModule.Manual,
			new CommunicationAudienceDefinitionDto
			{
				ExplicitRecipients =
				{
					new CommunicationExplicitRecipientDto
					{
						DisplayName = "Retained Ledger Recipient",
						UserId = Guid.NewGuid()
					}
				}
			},
			CommunicationChannelType.InApp);

		await PublishAsync(campaign.Id);

		var deleteResponse = await Client.DeleteAsync($"{CampaignsUrl}/{campaign.Id}");
		deleteResponse.StatusCode.ShouldNotBe(HttpStatusCode.OK);

		var cancelResponse = await Client.PutAsync($"{CampaignsUrl}/{campaign.Id}/cancel", content: null);
		cancelResponse.EnsureSuccessStatusCode();
		var cancelled = await cancelResponse.Content.ReadFromJsonAsync<CommunicationCampaignDto>(JsonOptions)
			?? throw new InvalidOperationException("Cancel response was empty.");
		cancelled.Status.ShouldBe(CommunicationCampaignStatus.Cancelled);

		var attempts = await GetAttemptsAsync(campaign.Id);
		attempts.TotalCount.ShouldBe(1);

		var usage = await GetUsageAsync(campaign.Id);
		usage.TotalCount.ShouldBe(1);
		usage.Items[0].AttemptCount.ShouldBe(1);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Reply_Capture_And_Assignment_Should_Update_Inbox_Status()
	{
		await CreateProviderAsync(CommunicationChannelType.InApp);
		var campaign = await CreateCampaignAsync(
			"Notification",
			CommunicationSourceModule.Manual,
			new CommunicationAudienceDefinitionDto
			{
				ExplicitRecipients =
				{
					new CommunicationExplicitRecipientDto
					{
						DisplayName = "Reply Recipient",
						UserId = Guid.NewGuid()
					}
				}
			},
			CommunicationChannelType.InApp);
		await PublishAsync(campaign.Id);
		var attempt = (await GetAttemptsAsync(campaign.Id)).Items.Single();

		var reply = await PostAndReadAsync<CommunicationReplyDto>(
			RepliesUrl,
			new CaptureCommunicationReplyDto
			{
				CampaignId = campaign.Id,
				DeliveryAttemptId = attempt.Id,
				RecipientSnapshotId = attempt.RecipientSnapshotId,
				Channel = CommunicationChannelType.InApp,
				FromAddress = attempt.Destination,
				Body = "Please call me back."
			});

		reply.Status.ShouldBe(CommunicationReplyStatus.Open);

		var assignResponse = await Client.PutAsJsonAsync(
			$"{RepliesUrl}/{reply.Id}/assign",
			new AssignCommunicationReplyDto
			{
				AssignedDepartment = "Admin Office",
				Status = CommunicationReplyStatus.Assigned,
				InternalNotes = "Follow up today"
			},
			JsonOptions);
		assignResponse.EnsureSuccessStatusCode();
		var assigned = await assignResponse.Content.ReadFromJsonAsync<CommunicationReplyDto>(JsonOptions)
			?? throw new InvalidOperationException("Assign response was empty.");
		assigned.Status.ShouldBe(CommunicationReplyStatus.Assigned);
		assigned.AssignedDepartment.ShouldBe("Admin Office");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Unsupported_Source_Filters_Should_Appear_In_Preview_And_Block_Publish()
	{
		await CreateProviderAsync(CommunicationChannelType.InApp);
		var audience = new CommunicationAudienceDefinitionDto
		{
			ExplicitRecipients =
			{
				new CommunicationExplicitRecipientDto
				{
					DisplayName = "Fee Filter Recipient",
					UserId = Guid.NewGuid()
				}
			},
			Filters =
			{
				new CommunicationAudienceFilterDto
				{
					Field = "finance.feeStatus",
					Operator = "Equals",
					Value = "Overdue"
				}
			}
		};

		var preview = await PostAndReadAsync<CommunicationAudiencePreviewDto>(
			$"{CampaignsUrl}/audience/preview",
			audience);
		preview.CanPublish.ShouldBeFalse();
		preview.BlockingIssues.Count.ShouldBeGreaterThan(0);

		var campaign = await CreateCampaignAsync(
			"Notification",
			CommunicationSourceModule.Finance,
			audience,
			CommunicationChannelType.InApp);

		var publishResponse = await Client.PutAsync($"{CampaignsUrl}/{campaign.Id}/publish", content: null);
		publishResponse.StatusCode.ShouldNotBe(HttpStatusCode.OK);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Student_Audience_Should_Resolve_Parent_First_Recipient()
	{
		var studentId = await CreateStudentWithParentContactAsync();

		var preview = await PostAndReadAsync<CommunicationAudiencePreviewDto>(
			$"{CampaignsUrl}/audience/preview",
			new CommunicationAudienceDefinitionDto
			{
				StudentIds = { studentId },
				ParentsFirst = true
			});

		preview.TotalRecipientCount.ShouldBe(1);
		preview.SampleRecipients[0].RecipientType.ShouldBe("ParentGuardian");
		preview.SampleRecipients[0].IsParentDefault.ShouldBeTrue();
		preview.SampleRecipients[0].MobileNumber.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Group_Audience_Should_Resolve_User_Destinations()
	{
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "CommGroup");
		employee.UserId.ShouldNotBeNull();
		var group = await CreateEmployeeGroupAsync(employee.Id);

		var preview = await PostAndReadAsync<CommunicationAudiencePreviewDto>(
			$"{CampaignsUrl}/audience/preview",
			new CommunicationAudienceDefinitionDto
			{
				GroupIds = { group.Id }
			});

		preview.GroupRecipientCount.ShouldBe(1);
		preview.TotalRecipientCount.ShouldBe(1);
		preview.SampleRecipients[0].UserId.ShouldBe(employee.UserId);
	}

	private async Task<ChannelProviderSettingDto> CreateProviderAsync(
		CommunicationChannelType channel,
		bool consentRequired = false,
		bool templateApprovalRequired = false)
	{
		var response = await Client.PostAsJsonAsync(
			ProviderSettingsUrl,
			new CreateUpdateChannelProviderSettingDto
			{
				Channel = channel,
				ProviderName = $"Test {channel} {Guid.NewGuid():N}",
				IsEnabled = true,
				IsDefault = true,
				ConsentRequired = consentRequired,
				TemplateApprovalRequired = templateApprovalRequired,
				Status = CommunicationProviderStatus.Enabled
			},
			JsonOptions);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<ChannelProviderSettingDto>(JsonOptions)
			?? throw new InvalidOperationException("Provider setting response was empty.");
	}

	private async Task DisableProvidersAsync(CommunicationChannelType channel)
	{
		var providers = await GetAndReadAsync<PagedResultDto<ChannelProviderSettingDto>>(
			$"{ProviderSettingsUrl}?channel={(int)channel}&maxResultCount=1000");

		foreach (var provider in providers.Items)
		{
			var response = await Client.PutAsJsonAsync(
				$"{ProviderSettingsUrl}/{provider.Id}",
				new CreateUpdateChannelProviderSettingDto
				{
					Channel = provider.Channel,
					ProviderName = provider.ProviderName,
					SenderId = provider.SenderId,
					IsEnabled = false,
					IsDefault = false,
					ConfigurationJson = provider.ConfigurationJson,
					SecretReference = provider.SecretReference,
					FallbackOrder = provider.FallbackOrder,
					DailyQuota = provider.DailyQuota,
					MonthlyQuota = provider.MonthlyQuota,
					ConsentRequired = provider.ConsentRequired,
					TemplateApprovalRequired = provider.TemplateApprovalRequired,
					Status = CommunicationProviderStatus.Disabled
				},
				JsonOptions);
			response.EnsureSuccessStatusCode();
		}
	}

	private async Task<CommunicationCampaignDto> CreateCampaignAsync(
		string campaignType,
		CommunicationSourceModule sourceModule,
		CommunicationAudienceDefinitionDto audience,
		CommunicationChannelType channel,
		CommunicationImportanceLevel importanceLevel = CommunicationImportanceLevel.Normal)
	{
		return await PostAndReadAsync<CommunicationCampaignDto>(
			CampaignsUrl,
			new CreateUpdateCommunicationCampaignDto
			{
				AcademicMediumId = Factory.SeededMediumId,
				CampaignType = campaignType,
				SourceModule = sourceModule,
				Title = $"Communication test {Guid.NewGuid():N}",
				Subject = "Communication test",
				Body = "Operational communication regression message.",
				ImportanceLevel = importanceLevel,
				AudienceDefinition = audience,
				Channels = { channel },
				FallbackOrder = { channel }
			});
	}

	private async Task<CommunicationCampaignDto> PublishAsync(Guid campaignId)
	{
		var response = await Client.PutAsync($"{CampaignsUrl}/{campaignId}/publish", content: null);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<CommunicationCampaignDto>(JsonOptions)
			?? throw new InvalidOperationException("Publish response was empty.");
	}

	private async Task ApproveAsync(Guid campaignId)
	{
		var response = await Client.PutAsJsonAsync(
			$"{CampaignsUrl}/{campaignId}/approve",
			new CommunicationApprovalDto { Reason = "Approved by regression test" },
			JsonOptions);
		response.EnsureSuccessStatusCode();
	}

	private async Task<PagedResultDto<DeliveryAttemptDto>> GetAttemptsAsync(Guid campaignId)
	{
		return await GetAndReadAsync<PagedResultDto<DeliveryAttemptDto>>(
			$"/api/communication/delivery-attempts?campaignId={campaignId}&maxResultCount=20");
	}

	private async Task<PagedResultDto<UsageLedgerEntryDto>> GetUsageAsync(Guid campaignId)
	{
		return await GetAndReadAsync<PagedResultDto<UsageLedgerEntryDto>>(
			$"/api/communication/usage-ledger?campaignId={campaignId}&maxResultCount=20");
	}

	private async Task<GroupDto> CreateEmployeeGroupAsync(Guid employeeId)
	{
		var input = new CreateUpdateGroupDto
		{
			Name = $"CommGroup{Guid.NewGuid():N}"[..20],
			NameBased = new NameBasedSelectionDto
			{
				SelectedMembers =
				{
					new CreateGroupMemberDto
					{
						MemberType = GroupMemberType.Employee,
						MemberId = employeeId
					}
				}
			}
		};

		var response = await Client.PostAsJsonAsync(
			$"/api/groups?academicYearId={Factory.SeededAcademicYearId}",
			input,
			JsonOptions);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<GroupDto>(JsonOptions)
			?? throw new InvalidOperationException("Group response was empty.");
	}

	private async Task<Guid> CreateStudentWithParentContactAsync()
	{
		using var scope = Factory.Services.CreateScope();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var studentRepository = scope.ServiceProvider.GetRequiredService<IStudentRepository>();
		var dbContext = scope.ServiceProvider.GetRequiredService<ShikshaMitraDbContext>();

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var boardId = await dbContext.Set<EducationBoard>()
				.IgnoreAutoIncludes()
				.AsNoTracking()
				.OrderBy(x => x.Id)
				.Select(x => x.Id)
				.FirstAsync();
			var studentId = Guid.NewGuid();
			var student = Student.CreateDirectRegistration(
				studentId,
				tenantId,
				"Comm",
				null,
				"Parent",
				Factory.SeededAcademicYearId,
				boardId,
				Factory.SeededMediumId,
				Factory.SeededMediumStageId,
				Factory.SeededStandardId,
				Factory.SeededDivisionId,
				DateTimeOffset.UtcNow.AddYears(-10),
				GenderEnum.Female,
				primaryContactNo: $"+91{Random.Shared.Next(700000000, 999999999)}",
				email: $"commparent{Guid.NewGuid():N}@example.test");

			await studentRepository.InsertAsync(student, autoSave: false);
			await uow.CompleteAsync();

			return studentId;
		}
	}
}
