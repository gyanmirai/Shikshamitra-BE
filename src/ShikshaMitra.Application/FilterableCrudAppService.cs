// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Linq.Dynamic.Core;
// using System.Linq.Expressions;
// using System.Reflection;
// using System.Threading.Tasks;
// using Volo.Abp.Application.Dtos;
// using Volo.Abp.Application.Services;
// using Volo.Abp.Domain.Entities;
// using Volo.Abp.Domain.Repositories;
//
// namespace ShikshaMitra;
//
// public class FilterableCrudAppService<TEntity, TEntityDto, TKey, TGetListInput, TCreateUpdateInput>
// 	: CrudAppService<TEntity, TEntityDto, TKey, TGetListInput, TCreateUpdateInput>
// 	where TEntity : class, IEntity<TKey>
// 	where TEntityDto : IEntityDto<TKey>
// 	where TGetListInput : PagedAndSortedResultRequestDto
// {
// 	private readonly IHttpContextAccessor _httpContextAccessor;
//
// 	public FilterableCrudAppService(
// 		IRepository<TEntity, TKey> repository,
// 		IHttpContextAccessor httpContextAccessor)
// 		: base(repository)
// 	{
// 		_httpContextAccessor = httpContextAccessor;
// 	}
//
// 	public override async Task<PagedResultDto<TEntityDto>> GetListAsync(TGetListInput input)
// 	{
// 		// Step 1: Collect Query Parameters
// 		var queryParams = HttpContext.Request.Query
// 			.ToDictionary(q => q.Key, q => q.Value.ToString());
//
// 		// Step 2: Get the IQueryable from the repository
// 		var query = await Repository.GetQueryableAsync();
//
// 		// Step 3: Apply Dynamic Filtering
// 		query = ApplyFiltering(query, queryParams);
//
// 		// Step 4: Apply Sorting
// 		query = ApplySorting(query, input.Sorting);
//
// 		// Step 5: Apply Paging
// 		query = ApplyPaging(query, input);
//
// 		// Step 6: Execute the Query
// 		var totalCount = await AsyncExecuter.CountAsync(query);
// 		var items = await AsyncExecuter.ToListAsync(query);
//
// 		// Step 7: Map to DTOs and Return
// 		return new PagedResultDto<TEntityDto>(
// 			totalCount,
// 			ObjectMapper.Map<List<TEntity>, List<TEntityDto>>(items)
// 		);
// 	}
//
// 	protected virtual IQueryable<TEntity> ApplyFiltering(IQueryable<TEntity> query,
// 		IDictionary<string, string> queryParams)
// 	{
// 		var parameter = Expression.Parameter(typeof(TEntity), "x");
// 		Expression predicate = null;
//
// 		foreach (var param in queryParams)
// 		{
// 			var propertyName = param.Key;
// 			var propertyValue = param.Value;
//
// 			// Get the property info
// 			PropertyInfo property = typeof(TEntity).GetProperty(propertyName,
// 				BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
// 			if (property == null)
// 				continue; // Skip if the property doesn't exist
//
// 			// Handle nullable types
// 			Type propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
//
// 			// Convert the property value to the correct type
// 			object typedValue;
// 			try
// 			{
// 				typedValue = Convert.ChangeType(propertyValue, propertyType);
// 			}
// 			catch
// 			{
// 				continue; // Skip if the conversion fails
// 			}
//
// 			// Build the expression x.Property == typedValue
// 			var member = Expression.Property(parameter, property);
// 			var constant = Expression.Constant(typedValue, property.PropertyType);
//
// 			Expression equality = Expression.Equal(member, constant);
//
// 			// Combine with existing predicate
// 			predicate = predicate == null ? equality : Expression.AndAlso(predicate, equality);
// 		}
//
// 		if (predicate == null)
// 			return query; // No filtering applied
//
// 		var lambda = Expression.Lambda<Func<TEntity, bool>>(predicate, parameter);
// 		return query.Where(lambda);
// 	}
//
// 	protected virtual IQueryable<TEntity> ApplySorting(IQueryable<TEntity> query, string sorting)
// 	{
// 		if (string.IsNullOrWhiteSpace(sorting))
// 		{
// 			sorting = nameof(IEntity<TKey>.Id); // Default sorting by Id
// 		}
//
// 		return query.OrderBy(sorting);
// 	}
//
// 	protected virtual IQueryable<TEntity> ApplyPaging(IQueryable<TEntity> query, TGetListInput input)
// 	{
// 		return query.Skip(input.SkipCount).Take(input.MaxResultCount);
// 	}
// }