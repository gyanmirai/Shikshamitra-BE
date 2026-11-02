using Microsoft.Extensions.Logging;
using System;
using ShikshaMitra.Localization;
using Volo.Abp;
using Volo.Abp.ExceptionHandling;
using Volo.Abp.Localization;

namespace ShikshaMitra.Security.Permissions.Exceptions;

public class VisibilityPrerequisiteMissingException : BusinessException, ILocalizeErrorMessage
{
	public VisibilityPrerequisiteMissingException(
		string? code,
		string? message,
		string? details,
		Exception? innerException,
		LogLevel logLevel)
		: base(code, message, details, innerException, logLevel)
	{
	}

	public VisibilityPrerequisiteMissingException(string permissionName)
		: base(SecurityPermissionErrorCodes.VisibilityPrerequisiteMissing,
			$"Cannot grant permission for '{permissionName}' because Navigation Visibility is revoked for this user/role.")
	{
		WithData("FeatureKey", permissionName);
	}

	public string LocalizeMessage(LocalizationContext context) =>
		context.LocalizerFactory.Create(typeof(ShikshaMitraResource))[SecurityPermissionErrorCodes.VisibilityPrerequisiteMissing]
			.Value.Replace("{FeatureKey}", Data["FeatureKey"]?.ToString());
}
