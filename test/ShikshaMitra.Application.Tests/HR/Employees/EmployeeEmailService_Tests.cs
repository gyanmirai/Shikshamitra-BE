using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ShikshaMitra.Common;
using Shouldly;
using Volo.Abp.Emailing;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.HR.Employees;

public class EmployeeEmailService_Tests
{
	[Fact]
	public async Task Does_not_send_a_guessed_tenant_link_when_the_school_cannot_be_resolved()
	{
		var sender = Substitute.For<IEmailSender>();
		var tenant = Substitute.For<ICurrentTenant>();
		tenant.Id.Returns(Guid.NewGuid());
		var service = new EmployeeEmailService(sender, NullLogger<EmployeeEmailService>.Instance,
			new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["App:ClientUrl"] = "https://school.example" }).Build(),
			tenant, Substitute.For<ITenantStore>());
		await service.SendWelcomeEmailAsync("teacher@example.invalid", "Synthetic teacher", "synthetic", "synthetic-only");
		sender.ReceivedCalls().ShouldBeEmpty();
	}

	[Fact]
	public async Task Empty_recipient_does_not_resolve_a_tenant_or_send_email()
	{
		var sender = Substitute.For<IEmailSender>();
		var tenants = Substitute.For<ITenantStore>();
		var service = new EmployeeEmailService(sender, NullLogger<EmployeeEmailService>.Instance,
			new ConfigurationBuilder().Build(), Substitute.For<ICurrentTenant>(), tenants);
		await service.SendWelcomeEmailAsync("", "Synthetic teacher", "synthetic", "synthetic-only");
		sender.ReceivedCalls().ShouldBeEmpty();
		tenants.ReceivedCalls().ShouldBeEmpty();
	}

	[Theory]
	[InlineData("https://school.example/", "DDVS", "https://school.example/login?organization=DDVS")]
	[InlineData("https://school.example/app", "School & One", "https://school.example/app/login?organization=School%20%26%20One")]
	[InlineData("http://localhost:3000", null, "http://localhost:3000/login")]
	public void Builds_configured_school_login_link(string root, string? school, string expected)
		=> EmployeeWelcomeEmailLink.Build(root, school).ShouldBe(expected);

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("/relative")]
	[InlineData("javascript:alert(1)")]
	[InlineData("https://user:password@school.example")]
	[InlineData("https://school.example?redirect=other")]
	public void Rejects_missing_or_unsafe_application_roots(string? root)
		=> Should.Throw<InvalidOperationException>(() => EmployeeWelcomeEmailLink.Build(root, "DDVS"));

	[Fact]
	public async Task Renders_school_button_and_copyable_url_without_sending_real_email()
	{
		var sender = Substitute.For<IEmailSender>();
		var tenant = Substitute.For<ICurrentTenant>();
		var tenantStore = Substitute.For<ITenantStore>();
		var tenantId = Guid.NewGuid();
		tenant.Id.Returns(tenantId);
		tenantStore.FindAsync(tenantId).Returns(new TenantConfiguration(tenantId, "School & One"));
		var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
		{
			["App:ClientUrl"] = "https://school.example/"
		}).Build();
		var service = new EmployeeEmailService(sender, NullLogger<EmployeeEmailService>.Instance, config, tenant, tenantStore);
		string? body = null;
		sender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Do<string>(value => body = value), true, Arg.Any<AdditionalEmailSendingArgs>())
			.Returns(Task.CompletedTask);

		await service.SendWelcomeEmailAsync("teacher@example.invalid", "Teacher <One>", "teacher&one", "synthetic-only");

		body.ShouldNotBeNull();
		body.ShouldContain("Login to ShikshaMitra");
		body.ShouldContain("href='https://school.example/login?organization=School%20%26%20One'");
		body.ShouldContain("School &amp; One");
		body.ShouldContain("Teacher &lt;One&gt;");
		body.ShouldContain("teacher&amp;one");
		body.ShouldContain("overflow-wrap:anywhere");
		body.ShouldNotContain("tenantId=");
		File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "employee-welcome-synthetic.html"), body);
		await sender.Received(1).SendAsync("teacher@example.invalid", Arg.Any<string>(), Arg.Any<string>(), true, Arg.Any<AdditionalEmailSendingArgs>());
	}
}
