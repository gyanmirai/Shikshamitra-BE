using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees.PersonalInfo;
using Shouldly;
using Volo.Abp.Emailing;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Employees;

public class EmployeeWelcomePostCommit_Tests
{
	[Fact]
	public async Task Completion_callback_restores_the_creating_tenant_before_rendering_welcome_email()
	{
		var creatingTenant = Guid.NewGuid();
		var laterTenant = Guid.NewGuid();
		Guid? activeTenant = creatingTenant;
		var tenant = Substitute.For<ICurrentTenant>();
		tenant.Id.Returns(_ => activeTenant);
		tenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(call =>
		{
			var previous = activeTenant;
			activeTenant = call.ArgAt<Guid?>(0);
			return new RestoreTenant(() => activeTenant = previous);
		});
		var tenantStore = Substitute.For<ITenantStore>();
		tenantStore.FindAsync(creatingTenant).Returns(new TenantConfiguration(creatingTenant, "DDVS"));
		tenantStore.FindAsync(laterTenant).Returns(new TenantConfiguration(laterTenant, "OTHER-SCHOOL"));
		var sender = Substitute.For<IEmailSender>();
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
		{
			["App:ClientUrl"] = "https://school.example"
		}).Build();
		var email = new EmployeeEmailService(sender, NullLogger<EmployeeEmailService>.Instance,
			configuration, tenant, tenantStore);
		Func<Task>? onCompleted = null;
		var unitOfWork = Substitute.For<IUnitOfWork>();
		unitOfWork.OnCompleted(Arg.Do<Func<Task>>(callback => onCompleted = callback));
		var manager = Substitute.For<IUnitOfWorkManager>();
		manager.Current.Returns(unitOfWork);
		var effects = new EmployeePersonalInfoPostCommitEffects(null!, email,
			Substitute.For<IDistributedEventBus>(), tenant, manager, Substitute.For<IClock>(),
			NullLogger<EmployeePersonalInfoPostCommitEffects>.Instance);
		// Register only reads identity fields; no persistence or real staff is involved.
		var employee = (Employee)Activator.CreateInstance(typeof(Employee), nonPublic: true)!;
		effects.Register(employee, new EmployeePersonalInfoDto(),
			new CreateEmployeePersonalInfoDto { FirstName = "Synthetic", LastName = "Teacher", Email = "teacher@example.invalid" },
			new EmployeePersonalInfoUserAccount(new IdentityUser(Guid.NewGuid(), "synthetic", "teacher@example.invalid", creatingTenant),
				"synthetic", "synthetic-only"), null, null);
		sender.ReceivedCalls().ShouldBeEmpty();
		onCompleted.ShouldNotBeNull();
		activeTenant = laterTenant;
		await onCompleted!();
		await sender.Received(1).SendAsync("teacher@example.invalid", Arg.Any<string>(),
			Arg.Is<string>(html => html.Contains("https://school.example/login?organization=DDVS") && !html.Contains("OTHER-SCHOOL")),
			true, Arg.Any<AdditionalEmailSendingArgs>());
		activeTenant.ShouldBe(laterTenant);
	}

	private sealed class RestoreTenant(Action restore) : IDisposable
	{
		public void Dispose() => restore();
	}
}
