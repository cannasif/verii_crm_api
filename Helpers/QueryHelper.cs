using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Collections.Generic;
using crm_api.DTOs;

namespace crm_api.Helpers
{
    public static class QueryHelper
    {
        public static readonly string[] CommonSearchableColumns =
        {
            "Name",
            "Title",
            "TitleName",
            "Description",
            "Code",
            "ERPCode",
            "ErpCode",
            "Value",
            "Type",
            "Status",
            "Reason",
            "Comment",
            "Comments",
            "Notes",
            "Text",
            "Content",
            "Message",
            "Subject",
            "Username",
            "Email",
            "FirstName",
            "LastName",
            "FullName",
            "Phone",
            "Phone1",
            "Phone2",
            "Address",
            "Website",
            "Url",
            "Path",
            "CustomerCode",
            "CustomerName",
            "TaxOffice",
            "TaxNumber",
            "TcknNumber",
            "SalesRepCode",
            "GroupCode",
            "StockCode",
            "StockName",
            "Barcode",
            "CurrencyCode",
            "CurrencyName",
            "SerialPrefix",
            "PermissionKey",
            "DisplayName",
            "SessionId",
            "IpAddress",
            "Browser",
            "Platform",
            "DeviceName",
            "Country.Name",
            "City.Name",
            "District.Name",
            "CustomerType.Name",
            "Customer.CustomerCode",
            "Customer.CustomerName",
            "Title.TitleName",
            "ApprovalRoleGroup.Name",
            "ApprovalRole.Name",
            "RoleNavigation.Title",
            "PermissionDefinition.Name",
            "PermissionDefinition.Code",
            "Group.Name",
            "User.Username",
            "User.Email",
            "User.FirstName",
            "User.LastName",
            "PowerBIGroup.Name",
            "ReportDefinition.Name",
            "Stock.StockCode",
            "Stock.StockName",
            "Order.OrderNumber",
            "Demand.DemandNumber",
            "Quotation.QuotationNumber"
        };

        public static IQueryable<T> ApplySearch<T>(this IQueryable<T> query, string? search, params string[] searchableColumns)
        {
            if (string.IsNullOrWhiteSpace(search) || searchableColumns.Length == 0)
            {
                return query;
            }

            var terms = search
                .Trim()
                .ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (terms.Length == 0)
            {
                return query;
            }

            var parameter = Expression.Parameter(typeof(T), "x");
            Expression? searchPredicate = null;

            foreach (var term in terms)
            {
                Expression? termPredicate = null;
                var searchValue = Expression.Constant(term);

                foreach (var column in searchableColumns)
                {
                    var resolved = ResolvePropertyPath(parameter, typeof(T), column);
                    if (resolved == null || resolved.Value.property.PropertyType != typeof(string))
                    {
                        continue;
                    }

                    var member = resolved.Value.expression;
                    var notNull = Expression.NotEqual(member, Expression.Constant(null, typeof(string)));
                    var toLower = Expression.Call(member, typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!);
                    var contains = Expression.Call(
                        toLower,
                        typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!,
                        searchValue);
                    var currentPredicate = Expression.AndAlso(notNull, contains);

                    termPredicate = termPredicate == null
                        ? currentPredicate
                        : Expression.OrElse(termPredicate, currentPredicate);
                }

                if (termPredicate == null)
                {
                    continue;
                }

                searchPredicate = searchPredicate == null
                    ? termPredicate
                    : Expression.AndAlso(searchPredicate, termPredicate);
            }

            if (searchPredicate == null)
            {
                return query;
            }

            var lambda = Expression.Lambda<Func<T, bool>>(searchPredicate, parameter);
            return query.Where(lambda);
        }

        /// <summary>
        /// Resolves a property path (possibly dot-notation like "Country.Name")
        /// to a chain of Expression.Property calls.
        /// Returns null if any part of the path does not exist.
        /// </summary>
        private static (Expression expression, PropertyInfo property)? ResolvePropertyPath(Expression param, Type rootType, string path)
        {
            var parts = path.Split('.');
            Expression current = param;
            PropertyInfo? prop = null;
            Type currentType = rootType;

            foreach (var part in parts)
            {
                prop = currentType.GetProperty(part, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                if (prop == null) return null;
                current = Expression.Property(current, prop);
                currentType = prop.PropertyType;
            }

            return prop == null ? null : (current, prop);
        }

        /// <summary>
        /// Resolves the actual entity column name from DTO/frontend column name using the columnMapping dictionary.
        /// </summary>
        private static string ResolveColumnName(string column, IReadOnlyDictionary<string, string>? columnMapping)
        {
            if (columnMapping == null) return column;
            var mappingKey = columnMapping.Keys.FirstOrDefault(k => string.Equals(k, column, StringComparison.OrdinalIgnoreCase));
            return mappingKey != null ? columnMapping[mappingKey] : column;
        }

        /// <summary>
        /// Applies filters to the query based on Filter list.
        /// columnMapping: DTO/frontend column names to entity property names (e.g. "name" -> "CustomerName", "countryName" -> "Country.Name").
        /// Supports dot-notation for navigation properties (left join / Include).
        /// </summary>
        public static IQueryable<T> ApplyFilters<T>(this IQueryable<T> query, List<Filter>? filters, string filterLogic = "and", IReadOnlyDictionary<string, string>? columnMapping = null)
        {
            ParameterExpression param = Expression.Parameter(typeof(T), "x");
            Expression? basePredicate = null;

            // Default filter: IsDeleted = false (if IsDeleted property exists)
            var isDeletedProperty = typeof(T).GetProperty("IsDeleted");
            if (isDeletedProperty != null && (isDeletedProperty.PropertyType == typeof(bool) || isDeletedProperty.PropertyType == typeof(bool?)))
            {
                var isDeletedLeft = Expression.Property(param, isDeletedProperty);
                var isDeletedExp = Expression.Equal(isDeletedLeft, Expression.Constant(false));
                basePredicate = isDeletedExp;
            }

            if (filters == null || filters.Count == 0)
            {
                if (basePredicate == null) return query;
                var defaultLambda = Expression.Lambda<Func<T, bool>>(basePredicate, param);
                return query.Where(defaultLambda);
            }

            bool useOr = string.Equals(filterLogic, "or", StringComparison.OrdinalIgnoreCase);
            Expression? filterPredicate = null;

            foreach (var filter in filters)
            {
                if (string.IsNullOrEmpty(filter.Value)) continue;

                var columnName = ResolveColumnName(filter.Column, columnMapping);

                var resolved = ResolvePropertyPath(param, typeof(T), columnName);
                if (resolved == null) continue;
                var (left, property) = resolved.Value;

                Expression? exp = null;

                var operatorLower = filter.Operator.ToLowerInvariant();

                if (property.PropertyType == typeof(string))
                {
                    var method = operatorLower switch
                    {
                        "contains" => typeof(string).GetMethod("Contains", new[] { typeof(string) }),
                        "startswith" => typeof(string).GetMethod("StartsWith", new[] { typeof(string) }),
                        "endswith" => typeof(string).GetMethod("EndsWith", new[] { typeof(string) }),
                        _ => null
                    };
                    
                    if (method != null)
                    {
                        exp = Expression.Call(left, method, Expression.Constant(filter.Value));
                    }
                    else
                    {
                        exp = Expression.Equal(left, Expression.Constant(filter.Value));
                    }
                }
                else if (property.PropertyType == typeof(int) || property.PropertyType == typeof(int?))
                {
                    if (int.TryParse(filter.Value, out int val))
                    {
                        exp = operatorLower switch
                        {
                            ">" or "gt" => Expression.GreaterThan(left, Expression.Constant(val)),
                            ">=" or "gte" => Expression.GreaterThanOrEqual(left, Expression.Constant(val)),
                            "<" or "lt" => Expression.LessThan(left, Expression.Constant(val)),
                            "<=" or "lte" => Expression.LessThanOrEqual(left, Expression.Constant(val)),
                            _ => Expression.Equal(left, Expression.Constant(val))
                        };
                    }
                }
                else if (property.PropertyType == typeof(long) || property.PropertyType == typeof(long?))
                {
                    if (long.TryParse(filter.Value, out long val))
                    {
                        exp = operatorLower switch
                        {
                            ">" or "gt" => Expression.GreaterThan(left, Expression.Constant(val)),
                            ">=" or "gte" => Expression.GreaterThanOrEqual(left, Expression.Constant(val)),
                            "<" or "lt" => Expression.LessThan(left, Expression.Constant(val)),
                            "<=" or "lte" => Expression.LessThanOrEqual(left, Expression.Constant(val)),
                            _ => Expression.Equal(left, Expression.Constant(val))
                        };
                    }
                }
                else if (property.PropertyType == typeof(decimal) || property.PropertyType == typeof(decimal?))
                {
                    if (decimal.TryParse(filter.Value, out decimal val))
                    {
                        exp = operatorLower switch
                        {
                            ">" or "gt" => Expression.GreaterThan(left, Expression.Constant(val)),
                            ">=" or "gte" => Expression.GreaterThanOrEqual(left, Expression.Constant(val)),
                            "<" or "lt" => Expression.LessThan(left, Expression.Constant(val)),
                            "<=" or "lte" => Expression.LessThanOrEqual(left, Expression.Constant(val)),
                            _ => Expression.Equal(left, Expression.Constant(val))
                        };
                    }
                }
                else if (property.PropertyType == typeof(DateTime) || property.PropertyType == typeof(DateTime?))
                {
                    if (DateTime.TryParse(filter.Value, out DateTime val))
                    {
                        exp = operatorLower switch
                        {
                            ">" or "gt" => Expression.GreaterThan(left, Expression.Constant(val)),
                            ">=" or "gte" => Expression.GreaterThanOrEqual(left, Expression.Constant(val)),
                            "<" or "lt" => Expression.LessThan(left, Expression.Constant(val)),
                            "<=" or "lte" => Expression.LessThanOrEqual(left, Expression.Constant(val)),
                            _ => Expression.Equal(left, Expression.Constant(val))
                        };
                    }
                }
                else if (property.PropertyType == typeof(bool) || property.PropertyType == typeof(bool?))
                {
                    if (bool.TryParse(filter.Value, out bool val))
                    {
                        exp = Expression.Equal(left, Expression.Constant(val));
                    }
                }
                else if (property.PropertyType.IsEnum)
                {
                    if (Enum.TryParse(property.PropertyType, filter.Value, true, out var enumVal))
                    {
                        exp = Expression.Equal(left, Expression.Constant(enumVal));
                    }
                }

                if (exp != null)
                {
                    filterPredicate = filterPredicate == null
                        ? exp
                        : useOr
                            ? Expression.OrElse(filterPredicate, exp)
                            : Expression.AndAlso(filterPredicate, exp);
                }
            }

            // Combine: basePredicate (IsDeleted) is always AND, filterPredicate is AND/OR based on filterLogic
            Expression? finalPredicate;
            if (basePredicate != null && filterPredicate != null)
                finalPredicate = Expression.AndAlso(basePredicate, filterPredicate);
            else
                finalPredicate = basePredicate ?? filterPredicate;

            if (finalPredicate == null) return query;
            
            var lambda = Expression.Lambda<Func<T, bool>>(finalPredicate, param);
            return query.Where(lambda);
        }

        /// <summary>
        /// Applies sorting to the query.
        /// Supports columnMapping and dot-notation for navigation properties (e.g. "countryName" -> "Country.Name").
        /// </summary>
        public static IQueryable<T> ApplySorting<T>(this IQueryable<T> query, string? sortBy, string? sortDirection, IReadOnlyDictionary<string, string>? columnMapping = null)
        {
            if (string.IsNullOrWhiteSpace(sortBy))
            {
                sortBy = "Id";
            }

            var resolvedSortBy = ResolveColumnName(sortBy, columnMapping);

            var parameter = Expression.Parameter(typeof(T), "x");
            var resolved = ResolvePropertyPath(parameter, typeof(T), resolvedSortBy);
            if (resolved == null)
            {
                // Fallback to Id if resolved property doesn't exist
                resolved = ResolvePropertyPath(parameter, typeof(T), "Id");
                if (resolved == null) return query;
            }

            var (member, _) = resolved.Value;
            var keySelector = Expression.Lambda(
                typeof(Func<,>).MakeGenericType(typeof(T), member.Type),
                member,
                parameter
            );

            bool isDescending = string.IsNullOrWhiteSpace(sortDirection) 
                ? false 
                : sortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase) 
                   || sortDirection.Equals("descending", StringComparison.OrdinalIgnoreCase);

            var methodName = isDescending ? "OrderByDescending" : "OrderBy";
            var call = Expression.Call(
                typeof(Queryable),
                methodName,
                new[] { typeof(T), member.Type },
                query.Expression,
                keySelector
            );
            
            return query.Provider.CreateQuery<T>(call);
        }

        /// <summary>
        /// Applies pagination to the query (1-based page number)
        /// </summary>
        public static IQueryable<T> ApplyPagination<T>(this IQueryable<T> query, int pageNumber, int pageSize)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 20;
            
            int skip = (pageNumber - 1) * pageSize;
            return query.Skip(skip).Take(pageSize);
        }

        /// <summary>
        /// Applies all PagedRequest operations (filters, sorting, pagination) to the query.
        /// Supports columnMapping for DTO-to-entity property name resolution.
        /// </summary>
        public static IQueryable<T> ApplyPagedRequest<T>(this IQueryable<T> query, PagedRequest request, IReadOnlyDictionary<string, string>? columnMapping = null)
        {
            if (request == null) return query;

            query = query.ApplySearch(request.Search);
            query = query.ApplyFilters(request.Filters, request.FilterLogic, columnMapping);
            query = query.ApplySorting(request.SortBy, request.SortDirection, columnMapping);
            query = query.ApplyPagination(request.PageNumber, request.PageSize);

            return query;
        }
    }
}
