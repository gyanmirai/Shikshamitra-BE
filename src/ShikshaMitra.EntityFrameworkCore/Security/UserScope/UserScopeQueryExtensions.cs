using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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

		// If user has no scope assigned at any level (no IDs and no wildcards), deny access to everything
		if (!userScope.HasAnyAccess)
		{
			// Return empty queryable (no access)
			return queryable.Where(e => false);
		}

		// Apply hierarchical filtering using NULL = ALL logic with parent constraints
		// User has access if entity matches ANY of their assigned scopes at ANY level
		// Wildcards grant access to ALL entities at that level WITHIN parent hierarchy constraints
		//
		// IMPORTANT: Academic Year is enforced as a TOP-LEVEL FILTER across all checks.
		// User must have access to the entity's academic year (or wildcard) to see it at all.
		var baseFilter = queryable.Where(e =>
			// Division level (most specific)
			// Wildcard: ALL divisions WITHIN assigned standards
			// Specific: exact division match
			(e.DivisionId.HasValue && (
				(userScope.HasWildcardDivision &&
				 e.StandardId.HasValue &&
				 userScope.StandardIds.Contains(e.StandardId.Value)) ||
				userScope.DivisionIds.Contains(e.DivisionId.Value)
			)) ||

			// Standard/Class level
			// Wildcard: ALL standards WITHIN assigned medium stages
			// Specific: exact standard match
			(e.StandardId.HasValue && (
				(userScope.HasWildcardStandard &&
				 e.EducationMediumStageId.HasValue &&
				 userScope.MediumStageIds.Contains(e.EducationMediumStageId.Value)) ||
				userScope.StandardIds.Contains(e.StandardId.Value)
			)) ||

			// Medium Stage level
			// Wildcard: ALL stages WITHIN assigned mediums
			// Specific: exact stage match
			(e.EducationMediumStageId.HasValue && (
				(userScope.HasWildcardMediumStage &&
				 e.EducationMediumId.HasValue &&
				 userScope.MediumIds.Contains(e.EducationMediumId.Value)) ||
				userScope.MediumStageIds.Contains(e.EducationMediumStageId.Value)
			)) ||

			// Medium level
			// Wildcard: ALL mediums WITHIN assigned boards
			// Specific: exact medium match
			(e.EducationMediumId.HasValue && (
				(userScope.HasWildcardMedium &&
				 e.EducationBoardId.HasValue &&
				 userScope.BoardIds.Contains(e.EducationBoardId.Value)) ||
				userScope.MediumIds.Contains(e.EducationMediumId.Value)
			)) ||

			// Board level
			// Wildcard: ALL boards (no parent constraint)
			// Specific: exact board match
			(e.EducationBoardId.HasValue && (
				userScope.HasWildcardBoard ||
				userScope.BoardIds.Contains(e.EducationBoardId.Value)
			))
		);

		// Apply Academic Year filter as a cross-cutting constraint
		// This ensures that even if a user has access at board/medium/standard/division level,
		// they can only see entities in their assigned academic year(s)
		if (userScope.HasWildcardAcademicYear)
		{
			// Wildcard academic year: see entities in ALL years (no additional filter needed)
			return baseFilter;
		}
		else if (userScope.AcademicYearIds.Any())
		{
			// Specific academic year(s): only see entities in assigned years
			return baseFilter.Where(e =>
				!e.AcademicYearId.HasValue ||  // Entities without academic year are accessible
				userScope.AcademicYearIds.Contains(e.AcademicYearId.Value)
			);
		}
		else
		{
			// No academic year assigned and no wildcard: filter out all entities with an academic year
			return baseFilter.Where(e => !e.AcademicYearId.HasValue);
		}
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

		// First check academic year (cross-cutting constraint)
		// Academic year restriction applies regardless of hierarchy level
		if (entity.AcademicYearId.HasValue)
		{
			// Entity has an academic year - check if user has access to it
			if (!userScope.HasWildcardAcademicYear && !userScope.AcademicYearIds.Contains(entity.AcademicYearId.Value))
			{
				// User doesn't have access to this academic year
				return false;
			}
		}

		// Check each level of the hierarchy from most specific to least specific
		// Wildcards grant access to ALL entities at that level WITHIN parent constraints

		// 1. Division level (most granular)
		// Wildcard: division within assigned standards, OR specific match
		if (entity.DivisionId.HasValue && (
			(userScope.HasWildcardDivision &&
			 entity.StandardId.HasValue &&
			 userScope.StandardIds.Contains(entity.StandardId.Value)) ||
			userScope.DivisionIds.Contains(entity.DivisionId.Value)))
			return true;

		// 2. Standard/Class level
		// Wildcard: standard within assigned stages, OR specific match
		if (entity.StandardId.HasValue && (
			(userScope.HasWildcardStandard &&
			 entity.EducationMediumStageId.HasValue &&
			 userScope.MediumStageIds.Contains(entity.EducationMediumStageId.Value)) ||
			userScope.StandardIds.Contains(entity.StandardId.Value)))
			return true;

		// 3. Medium Stage level
		// Wildcard: stage within assigned mediums, OR specific match
		if (entity.EducationMediumStageId.HasValue && (
			(userScope.HasWildcardMediumStage &&
			 entity.EducationMediumId.HasValue &&
			 userScope.MediumIds.Contains(entity.EducationMediumId.Value)) ||
			userScope.MediumStageIds.Contains(entity.EducationMediumStageId.Value)))
			return true;

		// 4. Medium level
		// Wildcard: medium within assigned boards, OR specific match
		if (entity.EducationMediumId.HasValue && (
			(userScope.HasWildcardMedium &&
			 entity.EducationBoardId.HasValue &&
			 userScope.BoardIds.Contains(entity.EducationBoardId.Value)) ||
			userScope.MediumIds.Contains(entity.EducationMediumId.Value)))
			return true;

		// 5. Board level (top of hierarchy)
		// Wildcard: all boards (no parent), OR specific match
		if (entity.EducationBoardId.HasValue && (
			userScope.HasWildcardBoard ||
			userScope.BoardIds.Contains(entity.EducationBoardId.Value)))
			return true;

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
	/// Asynchronously gets the count of entities the user can access based on their scope.
	/// More efficient than loading and counting filtered entities.
	/// </summary>
	/// <typeparam name="TEntity">Entity type implementing IUserScopedEntity</typeparam>
	/// <param name="queryable">The queryable to count</param>
	/// <param name="userScope">User's scope context</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Task containing the count of accessible entities</returns>
	/// <example>
	/// <code>
	/// var query = await _studentRepository.GetQueryableAsync();
	/// var userScope = await _userScopeService.GetUserScopeAsync(userId);
	/// var accessibleCount = await query.CountWithUserScopeAsync(userScope);
	/// </code>
	/// </example>
	public static async Task<int> CountWithUserScopeAsync<TEntity>(
		this IQueryable<TEntity> queryable,
		UserScopeContext userScope,
		CancellationToken cancellationToken = default)
		where TEntity : IUserScopedEntity
	{
		return await queryable.ApplyUserScope(userScope).CountAsync(cancellationToken);
	}

	/// <summary>
	/// Asynchronously checks if any entities exist that the user can access based on their scope.
	/// </summary>
	/// <typeparam name="TEntity">Entity type implementing IUserScopedEntity</typeparam>
	/// <param name="queryable">The queryable to check</param>
	/// <param name="userScope">User's scope context</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Task containing true if user can access at least one entity, false otherwise</returns>
	/// <example>
	/// <code>
	/// var query = await _studentRepository.GetQueryableAsync();
	/// var userScope = await _userScopeService.GetUserScopeAsync(userId);
	/// var hasAccess = await query.AnyWithUserScopeAsync(userScope);
	/// </code>
	/// </example>
	public static async Task<bool> AnyWithUserScopeAsync<TEntity>(
		this IQueryable<TEntity> queryable,
		UserScopeContext userScope,
		CancellationToken cancellationToken = default)
		where TEntity : IUserScopedEntity
	{
		return await queryable.ApplyUserScope(userScope).AnyAsync(cancellationToken);
	}
}