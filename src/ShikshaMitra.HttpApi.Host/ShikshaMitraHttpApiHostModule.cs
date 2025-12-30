using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using OpenIddict.Validation.AspNetCore;
using QuestPDF.Infrastructure;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Extensions;
using ShikshaMitra.Middleware;
using ShikshaMitra.MultiTenancy;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Volo.Abp;
using Volo.Abp.Account;
using Volo.Abp.Account.Web;
using Volo.Abp.AspNetCore.ExceptionHandling;
using Volo.Abp.AspNetCore.MultiTenancy;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.UI.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonXLite;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonXLite.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;
using Volo.Abp.OpenIddict;
using Volo.Abp.Security.Claims;
using Volo.Abp.Swashbuckle;
using Volo.Abp.UI.Navigation.Urls;
using Volo.Abp.VirtualFileSystem;

namespace ShikshaMitra;

[DependsOn(
	typeof(ShikshaMitraHttpApiModule),
	typeof(AbpAutofacModule),
	typeof(AbpAspNetCoreMultiTenancyModule),
	typeof(ShikshaMitraApplicationModule),
	typeof(ShikshaMitraEntityFrameworkCoreModule),
	typeof(AbpAspNetCoreMvcUiLeptonXLiteThemeModule),
	typeof(AbpAccountWebOpenIddictModule),
	typeof(AbpAspNetCoreSerilogModule),
	typeof(AbpSwashbuckleModule),
	typeof(AbpAspNetCoreMvcModule)
)]
public class ShikshaMitraHttpApiHostModule : AbpModule
{
	public override void PreConfigureServices(ServiceConfigurationContext context)
	{
		var hostingEnvironment = context.Services.GetHostingEnvironment();
		var configuration = context.Services.GetConfiguration();

		PreConfigure<OpenIddictBuilder>(builder =>
		{
			builder.AddValidation(options =>
			{
				options.AddAudiences("ShikshaMitra");
				options.UseLocalServer();
				options.UseAspNetCore();
			});
		});

		if (hostingEnvironment.IsDevelopment()) return;

		PreConfigure<AbpOpenIddictAspNetCoreOptions>(options => options.AddDevelopmentEncryptionAndSigningCertificate = false);
		PreConfigure<OpenIddictServerBuilder>(builder =>
		{
			builder.AddEncryptionCertificate(
				GetEncryptionCertificate(hostingEnvironment, context.Services.GetConfiguration()));
			builder.AddSigningCertificate(
				GetSigningCertificate(hostingEnvironment, context.Services.GetConfiguration()));
			builder.SetIssuer(new Uri(configuration["AuthServer:Authority"]!));
		});

	}

	public override void ConfigureServices(ServiceConfigurationContext context)
	{
		var configuration = context.Services.GetConfiguration();
		var hostingEnvironment = context.Services.GetHostingEnvironment();
		var services = context.Services;
		services.AddHttpClient();

		ConfigureAuthentication(context);
		ConfigureBundles();
		ConfigureUrls(configuration);
		ConfigureConventionalControllers();
		ConfigureVirtualFileSystem(context);
		ConfigureCors(context, configuration);

		ConfigureFormOptions(context, configuration);

		ConfigureSwaggerServices(context, configuration);

		ConfigureExceptionHandling(context, hostingEnvironment);
	}

	private void ConfigureFormOptions(ServiceConfigurationContext context, IConfiguration configuration)
	{
		context.Services.Configure<FormOptions>(options =>
		{
			options.MultipartBodyLengthLimit = configuration.GetValue<long>("FormOptions:MultipartBodyLengthLimit");
			options.MemoryBufferThreshold = configuration.GetValue<int>("FormOptions:MemoryBufferThreshold");
		});
	}

	private void ConfigureExceptionHandling(ServiceConfigurationContext context, IHostEnvironment environment)
	{
		Configure<AbpExceptionHttpStatusCodeOptions>(options =>
		{
			options.Map("Volo.Abp.Authorization.AbpAuthorizationException", HttpStatusCode.Forbidden);
			options.Map("Volo.Abp.Validation.AbpValidationException", HttpStatusCode.BadRequest);
		});

		Configure<AbpExceptionHandlingOptions>(options =>
		{
			// Show detailed error messages and stack traces in development
			options.SendExceptionsDetailsToClients = environment.IsDevelopment();
			options.SendStackTraceToClients = environment.IsDevelopment();
		});
	}

	private void ConfigureAuthentication(ServiceConfigurationContext context)
	{

		context.Services.ForwardIdentityAuthenticationForBearer(OpenIddictValidationAspNetCoreDefaults
			.AuthenticationScheme);
		context.Services.Configure<AbpClaimsPrincipalFactoryOptions>(options =>
		{
			options.IsDynamicClaimsEnabled = true;
		});

		context.Services.AddControllers(options =>
		{
			options.Filters.Add(new IgnoreAntiforgeryTokenAttribute());
		});
	}

	private void ConfigureBundles()
	{
		Configure<AbpBundlingOptions>(options =>
		{
			options.StyleBundles.Configure(
				LeptonXLiteThemeBundles.Styles.Global,
				bundle => bundle.AddFiles("/global-styles.css")
			);
		});
	}

	private void ConfigureUrls(IConfiguration configuration)
	{
		Configure<AppUrlOptions>(options =>
		{
			options.Applications["MVC"].RootUrl = configuration["App:SelfUrl"];
			options.RedirectAllowedUrls.AddRange(configuration["App:RedirectAllowedUrls"]?.Split(',') ??
												 Array.Empty<string>());

			options.Applications["Angular"].RootUrl = configuration["App:ClientUrl"];
			options.Applications["Angular"].Urls[AccountUrlNames.PasswordReset] = "account/reset-password";
		});
	}

	private void ConfigureVirtualFileSystem(ServiceConfigurationContext context)
	{
		var hostingEnvironment = context.Services.GetHostingEnvironment();

		if (hostingEnvironment.IsDevelopment())
		{
			Configure<AbpVirtualFileSystemOptions>(options =>
			{
				options.FileSets.ReplaceEmbeddedByPhysical<ShikshaMitraDomainSharedModule>(
					Path.Combine(hostingEnvironment.ContentRootPath,
						$"..{Path.DirectorySeparatorChar}ShikshaMitra.Domain.Shared"));
				options.FileSets.ReplaceEmbeddedByPhysical<ShikshaMitraDomainModule>(
					Path.Combine(hostingEnvironment.ContentRootPath,
						$"..{Path.DirectorySeparatorChar}ShikshaMitra.Domain"));
				options.FileSets.ReplaceEmbeddedByPhysical<ShikshaMitraApplicationContractsModule>(
					Path.Combine(hostingEnvironment.ContentRootPath,
						$"..{Path.DirectorySeparatorChar}ShikshaMitra.Application.Contracts"));
				options.FileSets.ReplaceEmbeddedByPhysical<ShikshaMitraApplicationModule>(
					Path.Combine(hostingEnvironment.ContentRootPath,
						$"..{Path.DirectorySeparatorChar}ShikshaMitra.Application"));
			});
		}
	}

	private void ConfigureConventionalControllers()
	{
		Configure<AbpAspNetCoreMvcOptions>(options =>
		{
			options.ConventionalControllers.Create(typeof(ShikshaMitraApplicationModule).Assembly);
		});
	}

	private static void ConfigureSwaggerServices(ServiceConfigurationContext context, IConfiguration configuration)
	{
		context.Services.AddAbpSwaggerGenWithOAuth(
			configuration["AuthServer:Authority"]!,
			new Dictionary<string, string> { { "ShikshaMitra", "ShikshaMitra API" } },
			options =>
			{
				options.SwaggerDoc("v1", new OpenApiInfo { Title = "ShikshaMitra API", Version = "v1" });
				options.DocInclusionPredicate((docName, description) => true);
				options.CustomSchemaIds(type => type.FullName);
			});
	}

	private void ConfigureCors(ServiceConfigurationContext context, IConfiguration configuration)
	{
		context.Services.AddCors(options =>
		{
			options.AddDefaultPolicy(builder =>
			{
				builder
					.WithOrigins(configuration["App:CorsOrigins"]?
						.Split(",", StringSplitOptions.RemoveEmptyEntries)
						.Select(o => o.RemovePostFix("/"))
						.ToArray() ?? Array.Empty<string>())
					.WithAbpExposedHeaders()
					.SetIsOriginAllowedToAllowWildcardSubdomains()
					.AllowAnyHeader()
					.AllowAnyMethod()
					.AllowCredentials();
			});
		});

		context.Services.ConfigureApplicationCookie(options =>
		{
			options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
			options.Cookie.SameSite = SameSiteMode.None;
		});
	}
	private X509Certificate2 GetEncryptionCertificate(IWebHostEnvironment environment, IConfiguration config)
	{
		var fileName = "encryption-certificate.pfx";
		var password = config["MyAppCertificate:X590:Password"];

		var file = Path.Combine(environment.ContentRootPath, fileName);
		if (File.Exists(file))
		{
			var created = File.GetCreationTime(file);
			var days = (DateTime.Now - created).TotalDays;
			if (days > 180)
			{
				File.Delete(file);
			}
			else
			{
				return new X509Certificate2(file, password, X509KeyStorageFlags.MachineKeySet);
			}
		}


		using var algorithm = RSA.Create(keySizeInBits: 2048);
		var subject = new X500DistinguishedName("CN=Fabrikam Encryption Certificate");
		var request = new CertificateRequest(subject, algorithm,
			HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
		request.CertificateExtensions.Add(new X509KeyUsageExtension(
			X509KeyUsageFlags.KeyEncipherment, critical: true));
		var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow,
			DateTimeOffset.UtcNow.AddYears(2));
		File.WriteAllBytes(file, certificate.Export(X509ContentType.Pfx, password));
		return new X509Certificate2(file, password, X509KeyStorageFlags.MachineKeySet);
	}

	private X509Certificate2 GetSigningCertificate(IWebHostEnvironment environment, IConfiguration config)
	{
		var fileName = "signing-certificate.pfx";
		var password = config["MyAppCertificate:X590:Password"];
		var file = Path.Combine(environment.ContentRootPath, fileName);

		if (File.Exists(file))
		{
			var created = File.GetCreationTime(file);
			var days = (DateTime.Now - created).TotalDays;
			if (days > 180)
			{
				File.Delete(file);
			}
			else
			{
				return new X509Certificate2(file, password, X509KeyStorageFlags.MachineKeySet);
			}
		}

		using var algorithm = RSA.Create(keySizeInBits: 2048);
		var subject = new X500DistinguishedName("CN=Fabrikam Signing Certificate");
		var request = new CertificateRequest(subject, algorithm, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
		request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature,
			critical: true));

		var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(2));

		File.WriteAllBytes(file, certificate.Export(X509ContentType.Pfx, password));
		return new X509Certificate2(file, password, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.MachineKeySet);
	}

	public override void OnApplicationInitialization(ApplicationInitializationContext context)
	{
		var app = context.GetApplicationBuilder();
		var env = context.GetEnvironment();

		QuestPDF.Settings.License = LicenseType.Community;

		// 🔧 DEVELOPMENT TOGGLE: Comment this out to see raw ABP exception details
		// Uncomment to use custom error formatting
		// app.UseGlobalExceptionHandling();

		if (env.IsDevelopment())
		{
			app.UseDeveloperExceptionPage();
		}

		app.UseAbpRequestLocalization();

		if (!env.IsDevelopment())
		{
			app.UseErrorPage();
		}

		app.UseCorrelationId();
		app.UseStaticFiles();
		app.UseCustomHeader();
		app.UseRouting();
		app.UseCors();
		app.UseAuthentication();
		app.UseAbpOpenIddictValidation();

		if (MultiTenancyConsts.IsEnabled)
		{
			app.UseMultiTenancy();
		}

		app.UseUnitOfWork();
		app.UseDynamicClaims();
		app.UseAuthorization();

		app.UseSwagger();
		app.UseAbpSwaggerUI(c =>
		{
			c.SwaggerEndpoint("/swagger/v1/swagger.json", "ShikshaMitra API");

			var configuration = context.ServiceProvider.GetRequiredService<IConfiguration>();
			c.OAuthClientId(configuration["AuthServer:SwaggerClientId"]);
			c.OAuthScopes("ShikshaMitra");
		});

		app.UseAuditing();
		app.UseAbpSerilogEnrichers();
		app.UseConfiguredEndpoints();
	}
}