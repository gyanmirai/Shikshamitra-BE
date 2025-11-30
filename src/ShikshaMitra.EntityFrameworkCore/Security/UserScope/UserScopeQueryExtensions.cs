using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Security.UserScope;

namespace ShikshaMitra.EntityFrameworkCore.Security.UserScope;

/// <summary>
/// Extension methods for applying user scope filters to queryable entities.
/// Implements the "NULL = ALL" principle for hierarchical scope filtering.
/// </summary>
public static class UserScopeQueryExtensions
{
	/// <summary>
	/// Applies user scope filtering to a queryable of scoped entities.
	/// Uses the "NULL = ALL" principle - if a scope level is NULL, user has access to ALL at that level.
	/// </summary>
	/// <typeparam name="TEntity">Entity type implementing IUserScopedEntity</typeparam>
	/// <param name="queryable">The queryable to filter</param>
	/// <param name="userScope">User's scope context containing accessible IDs at each hierarchy level</param>
	/// <returns>Filtered queryable containing only entities the user can access</returns>
	/// <example>
	/// <code>
	/// var userScope = await _userScopeService.GetUserScopeAsync(userId);
	/// var query = await _studentRepository.GetQueryableAsync();
	/// var filteredStudents = query.ApplyUserScope(userScope);
	/// </code>
	/// </example>
	public static IQueryable<TEntity> ApplyUserScope<TEntity>(
		this IQueryable<TEntity> queryable,
		UserScopeContext userScope)
		where TEntity : IUserScopedEntity
	{
		// Admin users bypass all scope checks and see everything
		if (userScope.IsAdmin)
			return queryable;

		// If user has no scope assigned at any level, deny access to everything
		if (!userScope.DivisionIds.Any() &&
			!userScope.StandardIds.Any() &&
			!userScope.MediumStageIds.Any() &&
			!userScope.MediumIds.Any() &&
			!userScope.BoardIds.Any())
		{
			// Return empty queryable (no access)
			return queryable.Where(e => false);
		}

		// Apply hierarchical filtering using NULL = ALL logic
		// User has access if entity matches ANY of their assigned scopes at ANY level
		return queryable.Where(e =>
			// Division level (most specific) - user has access to specific divisions
			(e.DivisionId.HasValue && userScope.DivisionIds.Contains(e.DivisionId.Value)) ||

			// Standard/Class level - user has access to all divisions in these classes
			(e.StandardId.HasValue && userScope.StandardIds.Contains(e.StandardId.Value)) ||

			// Medium Stage level - user has access to all classes in these stages (Primary, Secondary, etc.)
			(e.EducationMediumStageId.HasValue && userScope.MediumStageIds.Contains(e.EducationMediumStageId.Value)) ||

			// Medium level - user has access to all stages in these mediums (English, Hindi, etc.)
			(e.EducationMediumId.HasValue && userScope.MediumIds.Contains(e.EducationMediumId.Value)) ||

			// Board level (least specific) - user has access to all mediums in these boards (CBSE, State, etc.)
			(e.EducationBoardId.HasValue && userScope.BoardIds.Contains(e.EducationBoardId.Value))
		);
	}

	/// <summary>
	/// Checks if a user has access to a specific entity based on their scope.
	/// Useful for authorization checks on individual entities.
	/// </summary>
	/// <typeparam name="TEntity">Entity type implementing IUserScopedEntity</typeparam>
	/// <param name="entity">The entity to check access for</param>
	/// <param name="userScope">User's scope context</param>
	/// <returns>True if user has access to the entity, false otherwise</returns>
	/// <example>
	/// <code>
	/// var student = await _studentRepository.GetAsync(studentId);
	/// var userScope = await _userScopeService.GetUserScopeAsync(userId);
	/// 
	/// if (!UserScopeQueryExtensions.HasAccessToEntity(student, userScope))
	/// {
	///     throw new UnauthorizedAccessException("You don't have access to this student");
	/// }
	/// </code>
	/// </example>
	public static bool HasAccessToEntity<TEntity>(
		TEntity entity,
		UserScopeContext userScope)
		where TEntity : IUserScopedEntity
	{
		// Admin users have access to everything
		if (userScope.IsAdmin)
			return true;

		// Check each level of the hierarchy from most specific to least specific

		// 1. Division level (most granular)
		if (entity.DivisionId.HasValue && userScope.DivisionIds.Contains(entity.DivisionId.Value))
			return true;

		// 2. Standard/Class level
		if (entity.StandardId.HasValue && userScope.StandardIds.Contains(entity.StandardId.Value))
			return true;

		// 3. Medium Stage level
		if (entity.EducationMediumStageId.HasValue && userScope.MediumStageIds.Contains(entity.EducationMediumStageId.Value))
			return true;

		// 4. Medium level
		if (entity.EducationMediumId.HasValue && userScope.MediumIds.Contains(entity.EducationMediumId.Value))
			return true;

		// 5. Board level (least granular)
		if (entity.EducationBoardId.HasValue && userScope.BoardIds.Contains(entity.EducationBoardId.Value))
			return true;

		// No matching scope found - deny access
		return false;
	}

	/// <summary>
	/// Filters a collection of entities based on user scope.
	/// Useful for in-memory filtering after entities have been loaded.
	/// </summary>
	/// <typeparam name="TEntity">Entity type implementing IUserScopedEntity</typeparam>
	/// <param name="entities">Collection of entities to filter</param>
	/// <param name="userScope">User's scope context</param>
	/// <returns>Filtered collection containing only entities the user can access</returns>
	/// <example>
	/// <code>
	/// var allStudents = await _studentRepository.GetListAsync();
	/// var userScope = await _userScopeService.GetUserScopeAsync(userId);
	/// var accessibleStudents = allStudents.FilterByUserScope(userScope);
	/// </code>
	/// </example>
	public static IEnumerable<TEntity> FilterByUserScope<TEntity>(
		this IEnumerable<TEntity> entities,
		UserScopeContext userScope)
		where TEntity : IUserScopedEntity
	{
		// Admin users see everything
		if (userScope.IsAdmin)
			return entities;

		// Filter entities based on scope
		return entities.Where(entity => HasAccessToEntity(entity, userScope));
	}

	/// <summary>
	/// Gets the count of entities the user can access based on their scope.
	/// More efficient than loading and counting filtered entities.
	/// </summary>
	/// <typeparam name="TEntity">Entity type implementing IUserScopedEntity</typeparam>
	/// <param name="queryable">The queryable to count</param>
	/// <param name="userScope">User's scope context</param>
	/// <returns>Count of accessible entities</returns>
	/// <example>
	/// <code>
	/// var query = await _studentRepository.GetQueryableAsync();
	/// var userScope = await _userScopeService.GetUserScopeAsync(userId);
	/// var accessibleCount = await query.CountWithUserScopeAsync(userScope);
	/// </code>
	/// </example>
	public static int CountWithUserScope<TEntity>(
		this IQueryable<TEntity> queryable,
		UserScopeContext userScope)
		where TEntity : IUserScopedEntity
	{
		return queryable.ApplyUserScope(userScope).Count();
	}

	/// <summary>
	/// Checks if any entities exist that the user can access based on their scope.
	/// </summary>
	/// <typeparam name="TEntity">Entity type implementing IUserScopedEntity</typeparam>
	/// <param name="queryable">The queryable to check</param>
	/// <param name="userScope">User's scope context</param>
	/// <returns>True if user can access at least one entity, false otherwise</returns>
	public static bool AnyWithUserScope<TEntity>(
		this IQueryable<TEntity> queryable,
		UserScopeContext userScope)
		where TEntity : IUserScopedEntity
	{
		return queryable.ApplyUserScope(userScope).Any();
	}
}