using System;
using System.Threading;
using System.Threading.Tasks;
using OpenIddict.Abstractions;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.OpenIddict.Authorizations;
using Volo.Abp.OpenIddict.Tokens;
using Volo.Abp.Uow;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;

namespace ShikshaMitra.Students.Portal;

/// <summary>Use the existing controlled account-email token/cache revocation pattern without changing passwords.</summary>
public class StudentPortalEmailSessionRevoker(IOpenIddictTokenManager tokens, IOpenIddictAuthorizationManager authorizations,
	IOpenIddictTokenCache<OpenIddictTokenModel> tokenCache, IOpenIddictAuthorizationCache<OpenIddictAuthorizationModel> authorizationCache,
	IUnitOfWorkManager units) : ITransientDependency
{
	public async Task RevokeAsync(Guid userId)
	{
		await foreach (var token in tokens.FindBySubjectAsync(userId.ToString()))
		{
			if (await tokens.GetStatusAsync(token) != OpenIddictConstants.Statuses.Valid) continue;
			var model = (OpenIddictTokenModel)token;
			units.Current!.OnCompleted(async () => await tokenCache.RemoveAsync(model, CancellationToken.None));
			try { if (!await tokens.TryRevokeAsync(token)) throw Rejected(); }
			finally { await tokenCache.RemoveAsync(model, CancellationToken.None); }
		}
		await foreach (var authorization in authorizations.FindBySubjectAsync(userId.ToString()))
		{
			if (await authorizations.GetStatusAsync(authorization) != OpenIddictConstants.Statuses.Valid) continue;
			var model = (OpenIddictAuthorizationModel)authorization;
			units.Current!.OnCompleted(async () => await authorizationCache.RemoveAsync(model, CancellationToken.None));
			try { if (!await authorizations.TryRevokeAsync(authorization)) throw Rejected(); }
			finally { await authorizationCache.RemoveAsync(model, CancellationToken.None); }
		}
	}
	private static BusinessException Rejected() => new BusinessException(StudentOnboardingErrorCodes.PortalAccountInvalid)
		.WithData("Reason", "The login email could not be updated safely. Ask your school to review the account.");
}
