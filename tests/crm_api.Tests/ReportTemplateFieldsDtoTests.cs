using crm_api.DTOs;
using System.Linq;
using Xunit;

namespace crm_api.Tests;

public class ReportTemplateFieldsDtoTests
{
    [Theory]
    [InlineData("Lines.StockCode")]
    [InlineData("Lines.StockUnit")]
    [InlineData("Lines.StockManufacturerCode")]
    [InlineData("Lines.StockGroupName")]
    [InlineData("Lines.StockCode1")]
    [InlineData("Lines.StockCode1Name")]
    [InlineData("Lines.StockCode2")]
    [InlineData("Lines.StockCode2Name")]
    [InlineData("Lines.StockCode3")]
    [InlineData("Lines.StockCode3Name")]
    [InlineData("Lines.StockCode4")]
    [InlineData("Lines.StockCode4Name")]
    [InlineData("Lines.StockCode5")]
    [InlineData("Lines.StockCode5Name")]
    [InlineData("Lines.HtmlDescription")]
    [InlineData("Lines.DefaultImagePath")]
    public void DemandFields_ShouldContain_NewStockFields(string expectedPath)
    {
        var fields = DemandFields.GetFields();
        Assert.Contains(fields.LineFields, x => x.Path == expectedPath);
    }

    [Theory]
    [InlineData("Lines.StockCode")]
    [InlineData("Lines.StockUnit")]
    [InlineData("Lines.StockManufacturerCode")]
    [InlineData("Lines.StockGroupName")]
    [InlineData("Lines.StockCode1")]
    [InlineData("Lines.StockCode1Name")]
    [InlineData("Lines.StockCode2")]
    [InlineData("Lines.StockCode2Name")]
    [InlineData("Lines.StockCode3")]
    [InlineData("Lines.StockCode3Name")]
    [InlineData("Lines.StockCode4")]
    [InlineData("Lines.StockCode4Name")]
    [InlineData("Lines.StockCode5")]
    [InlineData("Lines.StockCode5Name")]
    [InlineData("Lines.HtmlDescription")]
    [InlineData("Lines.DefaultImagePath")]
    public void QuotationFields_ShouldContain_NewStockFields(string expectedPath)
    {
        var fields = QuotationFields.GetFields();
        Assert.Contains(fields.LineFields, x => x.Path == expectedPath);
    }

    [Theory]
    [InlineData("Lines.StockCode")]
    [InlineData("Lines.StockUnit")]
    [InlineData("Lines.StockManufacturerCode")]
    [InlineData("Lines.StockGroupName")]
    [InlineData("Lines.StockCode1")]
    [InlineData("Lines.StockCode1Name")]
    [InlineData("Lines.StockCode2")]
    [InlineData("Lines.StockCode2Name")]
    [InlineData("Lines.StockCode3")]
    [InlineData("Lines.StockCode3Name")]
    [InlineData("Lines.StockCode4")]
    [InlineData("Lines.StockCode4Name")]
    [InlineData("Lines.StockCode5")]
    [InlineData("Lines.StockCode5Name")]
    [InlineData("Lines.HtmlDescription")]
    [InlineData("Lines.DefaultImagePath")]
    public void OrderFields_ShouldContain_NewStockFields(string expectedPath)
    {
        var fields = OrderFields.GetFields();
        Assert.Contains(fields.LineFields, x => x.Path == expectedPath);
    }

    [Theory]
    [InlineData("OfferNo")]
    [InlineData("QuotationNo")]
    [InlineData("Currency")]
    [InlineData("Total")]
    [InlineData("GrandTotal")]
    [InlineData("Lines.ImagePath")]
    [InlineData("Lines.ProductCode")]
    [InlineData("Lines.ProductName")]
    [InlineData("Lines.LineGrandTotal")]
    [InlineData("ExchangeRates.Currency")]
    [InlineData("ExchangeRates.IsManual")]
    public void FastQuotationFields_ShouldContain_ExpectedFields(string expectedPath)
    {
        var fields = FastQuotationFields.GetFields();
        var allPaths = fields.HeaderFields.Select(x => x.Path)
            .Concat(fields.LineFields.Select(x => x.Path))
            .Concat(fields.ExchangeRateFields.Select(x => x.Path));

        Assert.Contains(expectedPath, allPaths);
    }
}
