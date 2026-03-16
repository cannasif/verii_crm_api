using System.Collections.Generic;
using System.Linq;
using crm_api.DTOs;
using crm_api.Helpers;
using crm_api.Models;
using Xunit;

namespace crm_api.Tests;

public class QueryHelperFilterLogicTests
{
    [Fact]
    public void ApplyFilters_ShouldReturnUnion_WhenFilterLogicIsOr()
    {
        var stocks = new List<Stock>
        {
            new() { Id = 1, ErpStockCode = "IP17", StockName = "iPhone 17", IsDeleted = false },
            new() { Id = 2, ErpStockCode = "IP17P", StockName = "iPhone 17 Pro", IsDeleted = false },
            new() { Id = 3, ErpStockCode = "IP16", StockName = "iPhone 16", IsDeleted = false },
            new() { Id = 4, ErpStockCode = "CASE17", StockName = "17 Kılıf", IsDeleted = false },
        }.AsQueryable();

        var filters = new List<Filter>
        {
            new() { Column = "StockName", Operator = "contains", Value = "iPhone" },
            new() { Column = "StockName", Operator = "contains", Value = "17" },
        };

        var result = stocks.ApplyFilters(filters, "or").ToList();

        Assert.Equal(4, result.Count);
        Assert.Contains(result, x => x.StockName == "iPhone 17");
        Assert.Contains(result, x => x.StockName == "iPhone 17 Pro");
        Assert.Contains(result, x => x.StockName == "iPhone 16");
        Assert.Contains(result, x => x.StockName == "17 Kılıf");
    }

    [Fact]
    public void ApplyFilters_ShouldReturnIntersection_WhenFilterLogicIsAnd()
    {
        var stocks = new List<Stock>
        {
            new() { Id = 1, ErpStockCode = "IP17", StockName = "iPhone 17", IsDeleted = false },
            new() { Id = 2, ErpStockCode = "IP17P", StockName = "iPhone 17 Pro", IsDeleted = false },
            new() { Id = 3, ErpStockCode = "IP16", StockName = "iPhone 16", IsDeleted = false },
            new() { Id = 4, ErpStockCode = "CASE17", StockName = "17 Kılıf", IsDeleted = false },
        }.AsQueryable();

        var filters = new List<Filter>
        {
            new() { Column = "StockName", Operator = "contains", Value = "iPhone" },
            new() { Column = "StockName", Operator = "contains", Value = "17" },
        };

        var result = stocks.ApplyFilters(filters, "and").ToList();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, x => x.StockName == "iPhone 17");
        Assert.Contains(result, x => x.StockName == "iPhone 17 Pro");
    }
}
