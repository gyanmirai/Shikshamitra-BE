using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Localization;
using System.Collections.Generic;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace ShikshaMitra.Security.Permissions.PermissionDefinitionProviders.Base;

/// <summary>
/// Abstract base class for module permission definition providers.
/// Implements the Template Method pattern to eliminate code duplication and ensure consistency.
/// Each module now gets its own permission group for better organization and security isolation.
/// </summary>
public abstract class BaseModulePermissionDefinitionProvider : PermissionDefinitionProvider
{
	#region Abstract Properties - Module Configuration

	/// <summary>
	/// Gets the permission group name for the module.
	/// Each module should have its own group (e.g., "ShikshaMitra.Students").
	/// </summary>
	protected abstract string ModuleGroupName { get; }

	/// <summary>
	/// Gets the display name for the module's permission group.
	/// Used for localization (e.g., "Permission:StudentsModule").
	/// </summary>
	protected abstract string ModuleGroupDisplayName { get; }

	/// <summary>
	/// Gets the root permission name for the module.
	/// Example: ShikshaMitraSubmodulePermissions.Students.Default
	/// </summary>
	protected abstract string ModulePermissionName { get; }

	/// <summary>
	/// Gets the display names dictionary for the module.
	/// Maps permission names to their localization keys.
	/// </summary>
	protected abstract Dictionary<string, string> ModuleDisplayNames { get; }

	/// <summary>
	/// Gets the submodules dictionary for the module.
	/// Contains all submodules with their permissions and display names.
	/// </summary>
	protected abstract Dictionary<string, ShikshaMitraSubmodulePermissions.SubmoduleInfo> ModuleSubmodules { get; }

	#endregion

	#region Template Method Implementation

	/// <summary>
	/// Defines the permissions for the module and its submodules.
	/// This is a sealed template method that orchestrates the permission definition workflow.
	/// Each module creates its own permission group for better isolation.
	/// </summary>
	/// <param name="context">The permission definition context provided by ABP Framework.</param>
	public sealed override void Define(IPermissionDefinitionContext context)
	{
		// Create or get the module-specific permission group
		var group = context.GetGroupOrNull(ModuleGroupName);
		if (group == null)
		{
			group = context.AddGroup(ModuleGroupName, L(ModuleGroupDisplayName));
		}

		// Hook: Allow derived classes to execute custom logic before defining permissions
		BeforeDefinePermissions(context, group);

		// Define the module's root permission
		var modulePermission = group.AddPermission(
			ModulePermissionName,
			L(ModuleDisplayNames[ModulePermissionName]));

		// Define all submodules and their permissions
		DefineSubmodulePermissions(modulePermission, ModuleSubmodules);

		// Hook: Allow derived classes to execute custom logic after defining permissions
		AfterDefinePermissions(context, group, modulePermission);
	}

	#endregion

	#region Protected Methods - Shared Logic

	/// <summary>
	/// Defines all submodule permissions for the given parent permission.
	/// This method handles the hierarchical structure of permissions.
	/// </summary>
	/// <param name="parent">The parent module permission definition.</param>
	/// <param name="submodules">Dictionary of submodule information containing permissions and display names.</param>
	protected void DefineSubmodulePermissions(
		PermissionDefinition parent,
		Dictionary<string, ShikshaMitraSubmodulePermissions.SubmoduleInfo> submodules)
	{
		foreach (var (submoduleName, submoduleInfo) in submodules)
		{
			// Create the submodule permission (e.g., Students.StudentRecords)
			var submodule = parent.AddChild(
				submoduleName,
				L(submoduleInfo.DisplayNames[submoduleName]));

			// Add all action permissions for this submodule (e.g., View, Create, Edit, Delete)
			foreach (var permission in submoduleInfo.Permissions)
			{
				// Skip the default/parent permission as it's already defined
				if (permission == submoduleName) continue;

				submodule.AddChild(
					permission,
					L(submoduleInfo.DisplayNames[permission]));
			}
		}
	}

	/// <summary>
	/// Creates a localizable string for the given localization key.
	/// </summary>
	/// <param name="name">The localization key (e.g., "Permission:Students.StudentRecords").</param>
	/// <returns>A LocalizableString that ABP can resolve at runtime.</returns>
	protected static LocalizableString L(string name)
	{
		return LocalizableString.Create<ShikshaMitraResource>(name);
	}

	#endregion

	#region Virtual Hooks - Extension Points

	/// <summary>
	/// Virtual method called before defining module permissions.
	/// Override this to add custom logic before the standard permission definition process.
	/// </summary>
	/// <param name="context">The permission definition context.</param>
	/// <param name="group">The permission group.</param>
	/// <remarks>
	/// Use cases:
	/// - Logging or diagnostics
	/// - Conditional permission registration
	/// - Custom validation
	/// </remarks>
	protected virtual void BeforeDefinePermissions(
		IPermissionDefinitionContext context,
		PermissionGroupDefinition group)
	{
		// Default: No operation
		// Derived classes can override to add custom behavior
	}

	/// <summary>
	/// Virtual method called after defining module permissions.
	/// Override this to add custom logic after the standard permission definition process.
	/// </summary>
	/// <param name="context">The permission definition context.</param>
	/// <param name="group">The permission group.</param>
	/// <param name="modulePermission">The module permission that was just defined.</param>
	/// <remarks>
	/// Use cases:
	/// - Adding custom child permissions
	/// - Setting additional permission properties
	/// - Logging completion
	/// </remarks>
	protected virtual void AfterDefinePermissions(
		IPermissionDefinitionContext context,
		PermissionGroupDefinition group,
		PermissionDefinition modulePermission)
	{
		// Default: No operation
		// Derived classes can override to add custom behavior
	}

	#endregion
}