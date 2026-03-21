using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
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
using Volo.Abp.Emailing;
using Volo.Abp.Modularity;
using Volo.Abp.OpenIddict;
using Volo.Abp.Security.Claims;
using Volo.Abp.SettingManagement;
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
			// SECURITY: Show detailed error messages and stack traces only in development
			// In production, this prevents exposing internal implementation details and potential vulnerabilities
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

	private static string GetSwaggerModule(string ns)
	{
		if (ns.Contains(".HR.") || ns.Contains(".Controllers.HR")) return "v1-hr";
		if (ns.Contains(".Controllers.Academic")) return "v1-academic";
		if (ns.Contains(".Controllers.Students") || ns.StartsWith("ShikshaMitra.Students.")) return "v1-students";
		if (ns.Contains(".Controllers.Finance")) return "v1-finance";
		if (ns.Contains(".Controllers.Library")) return "v1-library";
		return "v1-admin";
	}

	private static string[] GetSwaggerTags(string ns, string controllerName)
	{
		// HR Module — handles both ShikshaMitra.Controllers.HR.* and ShikshaMitra.HttpApi.Controllers.HR.*
		if (ns.Contains(".Controllers.HR."))
		{
			var sub = ns.Split(".Controllers.HR.").Last().Split('.').First();
			return
			[
				sub switch
				{
					"Employees" => "HR - Employees",
					"AttendanceAndLeaves" => "HR - Attendance & Leaves",
					"Payroll" => "HR - Payroll",
					"Calendar" => "HR - Calendar",
					"EventAndHolidays" => "HR - Events & Holidays",
					"Reports" => "HR - Reports",
					_ => $"HR - {sub}"
				}
			];
		}

		// Academic Module
		if (ns.Contains(".Controllers.Academic."))
		{
			var sub = ns.Split(".Controllers.Academic.").Last().Split('.').First();
			return
			[
				sub switch
				{
					"HomeworkAssignment" => "Academic - Homework & Assignments",
					"Syllabus" => "Academic - Syllabus",
					"ClassManagement" => "Academic - Class Management",
					"ClassTeacher" => "Academic - Class Teacher",
					"Curriculum" => "Academic - Curriculum",
					"Examination" => "Academic - Examination",
					_ => $"Academic - {sub}"
				}
			];
		}

		// Students Module — handles .Controllers.Students.* and ShikshaMitra.Students.Attendances.*
		if (ns.Contains(".Controllers.Students."))
		{
			var sub = ns.Split(".Controllers.Students.").Last().Split('.').First();
			return
			[
				sub switch
				{
					"Enrollments" or "StudentEnrollments" => "Students - Enrollments",
					"Attendances" => "Students - Attendance",
					"Records" => "Students - Records",
					_ => $"Students - {sub}"
				}
			];
		}
		if (ns.StartsWith("ShikshaMitra.Students.Attendances"))
			return ["Students - Attendance"];

		// Finance Module — handles .Controllers.Finance.Setting and .Controllers.Finance (root-level)
		if (ns.Contains(".Controllers.Finance"))
		{
			if (ns.Contains(".Controllers.Finance."))
			{
				var sub = ns.Split(".Controllers.Finance.").Last().Split('.').First();
				return
				[
					sub switch
					{
						"Setting" => "Finance - Settings",
						"StudentFeeAssignment" => "Finance - Student Fee Assignment",
						_ => $"Finance - {sub}"
					}
				];
			}
			return ["Finance - General"];
		}

		// Library Module — handles .Controllers.Library.Setting and .Controllers.Library (root-level)
		if (ns.Contains(".Controllers.Library"))
		{
			if (ns.Contains(".Controllers.Library."))
			{
				var sub = ns.Split(".Controllers.Library.").Last().Split('.').First();
				return
				[
					sub switch
					{
						"Setting" => "Library - Settings",
						"Members" => "Library - Members",
						_ => $"Library - {sub}"
					}
				];
			}
			return ["Library - General"];
		}

		// Admin / everything else
		if (ns.Contains(".Controllers.TenantSettings")) return ["Admin - Tenant Settings"];
		if (ns.Contains(".Controllers.Security")) return ["Admin - Security"];
		if (ns.Contains(".Controllers.Communication")) return ["Admin - Communication"];
		if (ns.Contains(".Controllers.Groups")) return ["Admin - Groups"];

		return [controllerName];
	}

	private static void ConfigureSwaggerServices(ServiceConfigurationContext context, IConfiguration configuration)
	{
		context.Services.AddAbpSwaggerGenWithOAuth(
			configuration["AuthServer:Authority"]!,
			new Dictionary<string, string> { { "ShikshaMitra", "ShikshaMitra API" } },
			options =>
			{
				// Define separate Swagger documents per module
				options.SwaggerDoc("v1-hr", new OpenApiInfo { Title = "HR Module APIs", Version = "v1" });
				options.SwaggerDoc("v1-academic", new OpenApiInfo { Title = "Academic Module APIs", Version = "v1" });
				options.SwaggerDoc("v1-students", new OpenApiInfo { Title = "Students Module APIs", Version = "v1" });
				options.SwaggerDoc("v1-finance", new OpenApiInfo { Title = "Finance Module APIs", Version = "v1" });
				options.SwaggerDoc("v1-library", new OpenApiInfo { Title = "Library Module APIs", Version = "v1" });
				options.SwaggerDoc("v1-admin", new OpenApiInfo { Title = "Admin & Settings APIs", Version = "v1" });

				// Route each API to the correct document based on controller namespace
				options.DocInclusionPredicate((docName, apiDescription) =>
				{
					if (apiDescription.ActionDescriptor is not ControllerActionDescriptor controllerAction)
						return docName == "v1-admin";

					var ns = controllerAction.ControllerTypeInfo.Namespace ?? string.Empty;
					return GetSwaggerModule(ns) == docName;
				});

				options.CustomSchemaIds(type => type.FullName);

				// Sub-group within each document using tags
				options.TagActionsBy(api =>
				{
					if (api.ActionDescriptor is not ControllerActionDescriptor controllerAction)
						return ["Default"];

					var ns = controllerAction.ControllerTypeInfo.Namespace ?? string.Empty;
					return GetSwaggerTags(ns, controllerAction.ControllerName);
				});
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

		ConfigureSmtpPassword(context.ServiceProvider);

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
			c.SwaggerEndpoint("/swagger/v1-hr/swagger.json", "HR Module APIs");
			c.SwaggerEndpoint("/swagger/v1-academic/swagger.json", "Academic Module APIs");
			c.SwaggerEndpoint("/swagger/v1-students/swagger.json", "Students Module APIs");
			c.SwaggerEndpoint("/swagger/v1-finance/swagger.json", "Finance Module APIs");
			c.SwaggerEndpoint("/swagger/v1-library/swagger.json", "Library Module APIs");
			c.SwaggerEndpoint("/swagger/v1-admin/swagger.json", "Admin & Settings APIs");

			var configuration = context.ServiceProvider.GetRequiredService<IConfiguration>();
			c.OAuthClientId(configuration["AuthServer:SwaggerClientId"]);
			c.OAuthScopes("ShikshaMitra");
		});

		app.UseAuditing();
		app.UseAbpSerilogEnrichers();
		app.UseConfiguredEndpoints();
	}

	private void ConfigureSmtpPassword(IServiceProvider serviceProvider)
	{
		var configuration = serviceProvider.GetRequiredService<IConfiguration>();
		var smtpPassword = configuration["Settings:Abp.Mailing.Smtp.Password"];
		if (!string.IsNullOrEmpty(smtpPassword))
		{
			var settingManager = serviceProvider.GetService<SettingManager>();
			settingManager?.SetGlobalAsync(EmailSettingNames.Smtp.Password, smtpPassword);
		}
	}
}