using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using ShikshaMitra.Academic.Notifications;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationTemplatePreview_Tests
{
	private readonly Guid _tenantId = Guid.NewGuid();
	private readonly Guid _templateId = Guid.NewGuid();
	private readonly List<CommunicationTemplateRevision> _rows = [];
	private readonly INotificationTemplateReadService _reader = Substitute.For<INotificationTemplateReadService>();
	private readonly NotificationTemplatePreviewService _preview;
	public NotificationTemplatePreview_Tests()
	{
		var tenant = Substitute.For<ICurrentTenant>(); tenant.Id.Returns(_tenantId);
		var revisions = Substitute.For<IRepository<CommunicationTemplateRevision, Guid>>(); revisions.GetQueryableAsync().Returns(_rows.AsQueryable());
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["App:ClientUrl"] = "https://school.example" }).Build();
		var validator = new NotificationTemplateVariableValidator([new AcademicNotificationCatalogue([])], configuration);
		var executer = new AsyncQueryableExecuter([]);
		_preview = new(_reader, tenant, revisions, executer, validator, new(tenant, revisions, executer, validator), configuration);
	}
	[Fact]
	public async Task Draft_preview_uses_labelled_synthetic_values_without_publishing_the_draft()
	{
		var draft = Add();
		var preview = await _preview.PreviewAsync(_templateId, draft.Id);
		preview.IsSynthetic.ShouldBeTrue(); preview.Rendered.IsSuccess.ShouldBeTrue();
		preview.Rendered.Body.ShouldNotBeNull(); preview.Rendered.Body!.ShouldContain("Example recipient");
		preview.Rendered.Body!.ShouldContain("Example student");
		draft.IsPublished.ShouldBeFalse();
		await _reader.Received(1).GetAsync(_templateId, Arg.Any<CancellationToken>());
	}
	[Fact]
	public async Task Preview_enforces_the_read_boundary_before_loading_content()
	{
		var draft = Add();
		_reader.GetAsync(_templateId, Arg.Any<CancellationToken>()).Returns(Task.FromException<NotificationTemplateDto>(new AbpAuthorizationException()));
		await Should.ThrowAsync<AbpAuthorizationException>(() => _preview.PreviewAsync(_templateId, draft.Id));
	}
	[Theory]
	[InlineData("tenant")]
	[InlineData("template")]
	[InlineData("deleted")]
	public async Task Another_tenant_or_templates_revision_cannot_be_previewed(string mismatch)
	{
		var draft = Add(mismatch == "tenant" ? Guid.NewGuid() : _tenantId, mismatch == "template" ? Guid.NewGuid() : _templateId);
		if (mismatch == "deleted") draft.IsDeleted = true;
		await Should.ThrowAsync<EntityNotFoundException>(() => _preview.PreviewAsync(_templateId, draft.Id));
	}
	private CommunicationTemplateRevision Add(Guid? tenantId = null, Guid? templateId = null)
	{
		var row = new CommunicationTemplateRevision(Guid.NewGuid(), tenantId ?? _tenantId, templateId ?? _templateId, 1,
			"Academic.Homework.Published", "Guardian", CommunicationChannelType.Email, "en", "Homework",
			"Hello {{recipient_name}}, {{student_name}}: {{source_link}}", 1);
		_rows.Add(row);
		_reader.GetAsync(_templateId, Arg.Any<CancellationToken>()).Returns(new NotificationTemplateDto(_templateId, row.Id, 1,
			"Homework", row.EventKey, row.RecipientKind, row.Channel, row.LanguageCode, row.Subject, row.Body, 1, row.ContentHash,
			false, false, true, row.ConcurrencyStamp, "header-stamp", null, null, null));
		return row;
	}
}
