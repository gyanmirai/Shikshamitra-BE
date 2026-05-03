using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using OpenIddict.Abstractions;
using Volo.Abp;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.OpenIddict.Applications;
using Volo.Abp.OpenIddict.Scopes;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Uow;

namespace ShikshaMitra.OpenIddict;

/* Creates initial data that is needed to property run the application
 * and make client-to-server communication possible.
 */
public class OpenIddictDataSeedContributor(
	IConfiguration configuration,
	IOpenIddictApplicationRepository openIddictApplicationRepository,
	IAbpApplicationManager applicationManager,
	IOpenIddictScopeRepository openIddictScopeRepository,
	IOpenIddictScopeManager scopeManager,
	IPermissionDataSeeder permissionDataSeeder,
	IStringLocalizer<OpenIddictResponse> l)
	: IDataSeedContributor, ITransientDependency
{
	[UnitOfWork]
	public virtual async Task SeedAsync(DataSeedContext context)
	{
		await CreateScopesAsync();
		await CreateApplicationsAsync();
	}

	private async Task CreateScopesAsync()
	{
		if (await openIddictScopeRepository.FindByNameAsync("ShikshaMitra") == null)
		{
			await scopeManager.CreateAsync(new OpenIddictScopeDescriptor
			{
				Name = "ShikshaMitra",
				DisplayName = "ShikshaMitra API",
				Resources = { "ShikshaMitra" }
			});
		}
	}

	private async Task CreateApplicationsAsync()
	{
		var commonScopes = new List<string>
		{
			OpenIddictConstants.Permissions.Scopes.Address,
			OpenIddictConstants.Permissions.Scopes.Email,
			OpenIddictConstants.Permissions.Scopes.Phone,
			OpenIddictConstants.Permissions.Scopes.Profile,
			OpenIddictConstants.Permissions.Scopes.Roles,
			"ShikshaMitra"
		};

		var configurationSection = configuration.GetSection("OpenIddict:Applications");


		//Console Test / Angular Client
		var consoleAndAngularClientId = configurationSection["ShikshaMitra_App:ClientId"];
		if (!consoleAndAngularClientId.IsNullOrWhiteSpace())
		{
			string? consoleAndAngularClientRootUrl = configurationSection["ShikshaMitra_App:RootUrl"]?.TrimEnd('/');
			await CreateApplicationAsync(
				name: consoleAndAngularClientId!,
				type: OpenIddictConstants.ClientTypes.Public,
				consentType: OpenIddictConstants.ConsentTypes.Implicit,
				displayName: "Console Test / Angular Application",
				secret: null,
				grantTypes: new List<string>
				{
					OpenIddictConstants.GrantTypes.AuthorizationCode,
					OpenIddictConstants.GrantTypes.Password,
					OpenIddictConstants.GrantTypes.ClientCredentials,
					OpenIddictConstants.GrantTypes.RefreshToken
				},
				scopes: commonScopes,
				redirectUri: consoleAndAngularClientRootUrl,
				clientUri: consoleAndAngularClientRootUrl,
				postLogoutRedirectUri: consoleAndAngularClientRootUrl
			);
		}

		// Next.js Password Grant Client (For Postman / Server-side login)
		var nextJsClientId = configurationSection["ShikshaMitra_NextJs:ClientId"];
		var nextJsClientSecret = configurationSection["ShikshaMitra_NextJs:ClientSecret"];
		if (!nextJsClientId.IsNullOrWhiteSpace())
		{
			await CreateApplicationAsync(
				name: nextJsClientId!,
				type: OpenIddictConstants.ClientTypes.Confidential,
				consentType: OpenIddictConstants.ConsentTypes.Explicit,
				displayName: "ShikshaMitra Next.js Server Client",
				secret: nextJsClientSecret,
				grantTypes: new List<string>
				{
			OpenIddictConstants.GrantTypes.Password,
			OpenIddictConstants.GrantTypes.RefreshToken
				},
				scopes: new List<string>
				{
			"profile",
			"email",
			"roles",
			"offline_access",
			"ShikshaMitra"
				}
			);
		}

		// React Client
		var reactClinetId = configurationSection["ShikshaMitra_React_App:ClientId"];
		if (!reactClinetId.IsNullOrWhiteSpace())
		{
			string? reactClientRootUrl = configurationSection["ShikshaMitra_React_App:RootUrl"]?.TrimEnd('/');
			await CreateApplicationAsync(
				name: reactClinetId!,
				type: OpenIddictConstants.ClientTypes.Public,
				consentType: OpenIddictConstants.ConsentTypes.Implicit,
				displayName: "React Application",
				secret: null,
				grantTypes: new List<string>
				{
					OpenIddictConstants.GrantTypes.AuthorizationCode, OpenIddictConstants.GrantTypes.RefreshToken
				},
				scopes: commonScopes,
				redirectUri: $"{reactClientRootUrl}/auth/openiddict",
				clientUri: reactClientRootUrl,
				postLogoutRedirectUri: $"{reactClientRootUrl}/login"
			);
		}

		var reactAzureClinetId = configurationSection["ShikshaMitra_Azure_React_App:ClientId"];
		if (!reactAzureClinetId.IsNullOrWhiteSpace())
		{
			string? reactAzureClientRootUrl = configurationSection["ShikshaMitra_Azure_React_App:RootUrl"]?.TrimEnd('/');
			await CreateApplicationAsync(
				name: reactAzureClinetId!,
				type: OpenIddictConstants.ClientTypes.Public,
				consentType: OpenIddictConstants.ConsentTypes.Implicit,
				displayName: "React Azure Application",
				secret: null,
				grantTypes: new List<string>
				{
					OpenIddictConstants.GrantTypes.AuthorizationCode, OpenIddictConstants.GrantTypes.RefreshToken
				},
				scopes: commonScopes,
				redirectUri: $"{reactAzureClientRootUrl}/auth/openiddict",
				clientUri: reactAzureClientRootUrl,
				postLogoutRedirectUri: $"{reactAzureClientRootUrl}/login"
			);
		}


		// Flutter Client
		var flutterClientId = configurationSection["ShikshaMitra_Flutter_App:ClientId"];
		if (!flutterClientId.IsNullOrWhiteSpace())
		{
			string? flutterClientRootUrl = configurationSection["ShikshaMitra_Flutter_App:RootUrl"]?.TrimEnd('/');
			await CreateApplicationAsync(
				name: flutterClientId!,
				type: OpenIddictConstants.ClientTypes.Public,
				consentType: OpenIddictConstants.ConsentTypes.Implicit,
				displayName: "Flutter Application",
				secret: null,
				grantTypes: new List<string>
				{
			OpenIddictConstants.GrantTypes.AuthorizationCode,
			OpenIddictConstants.GrantTypes.RefreshToken
				},
				scopes: commonScopes,
				redirectUri: $"{flutterClientRootUrl}/callback",
				clientUri: flutterClientRootUrl,
				postLogoutRedirectUri: $"{flutterClientRootUrl}/logout"
			);
		}


		// Swagger Client
		var swaggerClientId = configurationSection["ShikshaMitra_Swagger:ClientId"];
		if (!swaggerClientId.IsNullOrWhiteSpace())
		{
			var swaggerRootUrl = configurationSection["ShikshaMitra_Swagger:RootUrl"]?.TrimEnd('/');

			await CreateApplicationAsync(
				name: swaggerClientId!,
				type: OpenIddictConstants.ClientTypes.Public,
				consentType: OpenIddictConstants.ConsentTypes.Implicit,
				displayName: "Swagger Application",
				secret: null,
				grantTypes: new List<string> { OpenIddictConstants.GrantTypes.AuthorizationCode, },
				scopes: commonScopes,
				redirectUri: $"{swaggerRootUrl}/swagger/oauth2-redirect.html",
				clientUri: swaggerRootUrl
			);
		}
	}

	private async Task CreateApplicationAsync(
		[NotNull] string name,
		[NotNull] string type,
		[NotNull] string consentType,
		string displayName,
		string? secret,
		List<string> grantTypes,
		List<string> scopes,
		string? clientUri = null,
		string? redirectUri = null,
		string? postLogoutRedirectUri = null,
		List<string>? permissions = null)
	{
		if (!string.IsNullOrEmpty(secret) && string.Equals(type, OpenIddictConstants.ClientTypes.Public,
				StringComparison.OrdinalIgnoreCase))
		{
			throw new BusinessException(l["NoClientSecretCanBeSetForPublicApplications"]);
		}

		if (string.IsNullOrEmpty(secret) && string.Equals(type, OpenIddictConstants.ClientTypes.Confidential,
				StringComparison.OrdinalIgnoreCase))
		{
			throw new BusinessException(l["TheClientSecretIsRequiredForConfidentialApplications"]);
		}

		var client = await openIddictApplicationRepository.FindByClientIdAsync(name);

		var application = new AbpApplicationDescriptor
		{
			ClientId = name,
			ClientType = type,
			ClientSecret = secret,
			ConsentType = consentType,
			DisplayName = displayName,
			ClientUri = clientUri,
		};

		Check.NotNullOrEmpty(grantTypes, nameof(grantTypes));
		Check.NotNullOrEmpty(scopes, nameof(scopes));

		if (new[] { OpenIddictConstants.GrantTypes.AuthorizationCode, OpenIddictConstants.GrantTypes.Implicit }.All(
				grantTypes.Contains))
		{
			application.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.CodeIdToken);

			if (string.Equals(type, OpenIddictConstants.ClientTypes.Public, StringComparison.OrdinalIgnoreCase))
			{
				application.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.CodeIdTokenToken);
				application.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.CodeToken);
			}
		}

		if (!redirectUri.IsNullOrWhiteSpace() || !postLogoutRedirectUri.IsNullOrWhiteSpace())
		{
			application.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Logout);
		}

		var buildInGrantTypes = new[]
		{
			OpenIddictConstants.GrantTypes.Implicit, OpenIddictConstants.GrantTypes.Password,
			OpenIddictConstants.GrantTypes.AuthorizationCode, OpenIddictConstants.GrantTypes.ClientCredentials,
			OpenIddictConstants.GrantTypes.DeviceCode, OpenIddictConstants.GrantTypes.RefreshToken
		};

		foreach (var grantType in grantTypes)
		{
			if (grantType == OpenIddictConstants.GrantTypes.AuthorizationCode)
			{
				application.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
				application.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
			}

			if (grantType == OpenIddictConstants.GrantTypes.AuthorizationCode ||
				grantType == OpenIddictConstants.GrantTypes.Implicit)
			{
				application.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
			}

			if (grantType == OpenIddictConstants.GrantTypes.AuthorizationCode ||
				grantType == OpenIddictConstants.GrantTypes.ClientCredentials ||
				grantType == OpenIddictConstants.GrantTypes.Password ||
				grantType == OpenIddictConstants.GrantTypes.RefreshToken ||
				grantType == OpenIddictConstants.GrantTypes.DeviceCode)
			{
				application.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
				application.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Revocation);
				application.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Introspection);
			}

			if (grantType == OpenIddictConstants.GrantTypes.ClientCredentials)
			{
				application.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);
			}

			if (grantType == OpenIddictConstants.GrantTypes.Implicit)
			{
				application.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.Implicit);
			}

			if (grantType == OpenIddictConstants.GrantTypes.Password)
			{
				application.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.Password);
			}

			if (grantType == OpenIddictConstants.GrantTypes.RefreshToken)
			{
				application.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
			}

			if (grantType == OpenIddictConstants.GrantTypes.DeviceCode)
			{
				application.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.DeviceCode);
				application.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Device);
			}

			if (grantType == OpenIddictConstants.GrantTypes.Implicit)
			{
				application.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.IdToken);
				if (string.Equals(type, OpenIddictConstants.ClientTypes.Public, StringComparison.OrdinalIgnoreCase))
				{
					application.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.IdTokenToken);
					application.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Token);
				}
			}

			if (!buildInGrantTypes.Contains(grantType))
			{
				application.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.GrantType + grantType);
			}
		}

		var buildInScopes = new[]
		{
			OpenIddictConstants.Permissions.Scopes.Address, OpenIddictConstants.Permissions.Scopes.Email,
			OpenIddictConstants.Permissions.Scopes.Phone, OpenIddictConstants.Permissions.Scopes.Profile,
			OpenIddictConstants.Permissions.Scopes.Roles
		};

		foreach (var scope in scopes)
		{
			if (buildInScopes.Contains(scope))
			{
				application.Permissions.Add(scope);
			}
			else
			{
				application.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + scope);
			}
		}

		if (redirectUri != null)
		{
			if (!redirectUri.IsNullOrEmpty())
			{
				if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri) || !uri.IsWellFormedOriginalString())
				{
					throw new BusinessException(l["InvalidRedirectUri", redirectUri]);
				}

				if (application.RedirectUris.All(x => x != uri))
				{
					application.RedirectUris.Add(uri);
					if (uri.ToString().Contains($"https://shikshamitra-dev.netlify.app/auth/openiddict"))
					{
						application.RedirectUris.Add(new Uri("http://localhost:3000/auth/openiddict"));
					}
					if (uri.ToString().Contains("stg-gyanmirai-be.azurewebsites.net/swagger/oauth2-redirect.html"))
					{
						application.RedirectUris.Add(new Uri("https://localhost:44332/swagger/oauth2-redirect.html"));
					}
				}
			}
		}

		if (postLogoutRedirectUri != null)
		{
			if (!postLogoutRedirectUri.IsNullOrEmpty())
			{
				if (!Uri.TryCreate(postLogoutRedirectUri, UriKind.Absolute, out var uri) ||
					!uri.IsWellFormedOriginalString())
				{
					throw new BusinessException(l["InvalidPostLogoutRedirectUri", postLogoutRedirectUri]);
				}

				if (application.PostLogoutRedirectUris.All(x => x != uri))
				{
					application.PostLogoutRedirectUris.Add(uri);

					if (uri.ToString().Contains($"https://shikshamitra-dev.netlify.app/login"))
					{
						application.PostLogoutRedirectUris.Add(new Uri("http://localhost:3000/login"));
					}
				}
			}
		}

		if (permissions != null)
		{
			await permissionDataSeeder.SeedAsync(
				ClientPermissionValueProvider.ProviderName,
				name,
				permissions,
				null
			);
		}

		if (client == null)
		{
			await applicationManager.CreateAsync(application);
			return;
		}

		if (!HasSameRedirectUris(client, application))
		{
			client.RedirectUris =
				JsonSerializer.Serialize(application.RedirectUris.Select(q => q.ToString().TrimEnd('/')));
			client.PostLogoutRedirectUris =
				JsonSerializer.Serialize(application.PostLogoutRedirectUris.Select(q => q.ToString().TrimEnd('/')));

			await applicationManager.UpdateAsync(client.ToModel());
		}

		if (!HasSameScopes(client, application))
		{
			client.Permissions = JsonSerializer.Serialize(application.Permissions.Select(q => q));
			await applicationManager.UpdateAsync(client.ToModel());
		}
	}

	private bool HasSameRedirectUris(OpenIddictApplication existingClient, AbpApplicationDescriptor application) => existingClient.RedirectUris ==
			   JsonSerializer.Serialize(application.RedirectUris.Select(q => q.ToString().TrimEnd('/')));

	private bool HasSameScopes(OpenIddictApplication existingClient, AbpApplicationDescriptor application)
	{
		return existingClient.Permissions ==
			   JsonSerializer.Serialize(application.Permissions.Select(q => q.ToString().TrimEnd('/')));
	}
}