using System;
using System.IO;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class WebPushSubscriptionProtection_Tests
{
	[Fact]
	public void A_browser_registration_survives_process_restart_and_key_rotation()
	{
		var directory = Directory.CreateTempSubdirectory("academic-push-test-");
		try
		{
			var owner = new NotificationSelfContext(Guid.NewGuid(), Guid.NewGuid());
			var registration = new RegisterWebPushSubscriptionDto(Guid.NewGuid(), "https://push.example.test/receipt", new("p256dh", "auth"), "v1");
			string protectedValue;
			using (var writer = BuildProvider(directory))
				protectedValue = Protector(writer).Protect(owner, registration);
			var row = new WebPushSubscription(Guid.NewGuid(), owner.TenantId, owner.UserId, registration.BrowserInstallationId,
				WebPushSubscriptionProtector.HashEndpoint(registration.Endpoint), protectedValue, registration.VapidKeyVersion, DateTimeOffset.UtcNow);

			using (var rotation = BuildProvider(directory))
			{
				var manager = rotation.GetRequiredService<IKeyManager>();
				var activation = DateTimeOffset.UtcNow;
				manager.CreateNewKey(activation, activation.AddDays(90));
				manager.GetAllKeys().Count.ShouldBeGreaterThanOrEqualTo(2);
			}

			using var reader = BuildProvider(directory);
			var reloaded = Protector(reader).Unprotect(row);
			reloaded.Endpoint.ShouldBe(registration.Endpoint);
			reloaded.Keys.ShouldBe(registration.Keys);
		}
		finally
		{
			directory.Delete(recursive: true);
		}
	}

	private static ServiceProvider BuildProvider(DirectoryInfo directory)
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddDataProtection().SetApplicationName("Academic.WebPush.KeyRing.Test").PersistKeysToFileSystem(directory);
		return services.BuildServiceProvider();
	}

	private static WebPushSubscriptionProtector Protector(ServiceProvider services) =>
		new(services.GetRequiredService<IDataProtectionProvider>());
}
