using System.Security.Claims;
using AutoMapper;
using crm_api.Data;
using crm_api.DTOs;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.Repositories;
using crm_api.Services;
using crm_api.UnitOfWork;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace crm_api.Tests;

public class CustomerMobileOcrRestoreTests
{
    private sealed class FakeLocalizationService : ILocalizationService
    {
        public string GetLocalizedString(string key) => key;
        public string GetLocalizedString(string key, params object[] arguments) => $"{key}: {string.Join(", ", arguments)}";
    }

    private static CustomerCreateFromMobileDto BuildRequest(
        string customerName,
        string email,
        string phone,
        string contactName = "Ali Veli")
    {
        return new CustomerCreateFromMobileDto
        {
            Name = customerName,
            ContactName = contactName,
            Email = email,
            Phone = phone,
            Title = "Yonetici",
            Notes = "test"
        };
    }

    private static async Task<(CustomerService Service, CmsDbContext Context)> BuildServiceAsync(
        Action<CmsDbContext>? seed = null)
    {
        var dbName = $"crm-ocr-tests-{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<CmsDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var context = new CmsDbContext(options);
        seed?.Invoke(context);
        await context.SaveChangesAsync();

        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(
                    new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "1")
                    }, "test"))
            }
        };

        var customerRepo = new GenericRepository<Customer>(context, httpContextAccessor);
        var contactRepo = new GenericRepository<Contact>(context, httpContextAccessor);
        var titleRepo = new GenericRepository<Title>(context, httpContextAccessor);
        var imageRepo = new GenericRepository<CustomerImage>(context, httpContextAccessor);
        var countryRepo = new GenericRepository<Country>(context, httpContextAccessor);
        var cityRepo = new GenericRepository<City>(context, httpContextAccessor);
        var districtRepo = new GenericRepository<District>(context, httpContextAccessor);

        var uow = new Mock<IUnitOfWork>();
        uow.SetupGet(x => x.Customers).Returns(customerRepo);
        uow.SetupGet(x => x.Contacts).Returns(contactRepo);
        uow.SetupGet(x => x.Titles).Returns(titleRepo);
        uow.SetupGet(x => x.CustomerImages).Returns(imageRepo);
        uow.SetupGet(x => x.Countries).Returns(countryRepo);
        uow.SetupGet(x => x.Cities).Returns(cityRepo);
        uow.SetupGet(x => x.Districts).Returns(districtRepo);
        uow.Setup(x => x.SaveChangesAsync()).Returns(() => context.SaveChangesAsync());
        uow.Setup(x => x.BeginTransactionAsync()).Returns(Task.CompletedTask);
        uow.Setup(x => x.CommitTransactionAsync()).Returns(Task.CompletedTask);
        uow.Setup(x => x.RollbackTransactionAsync()).Returns(Task.CompletedTask);

        var mapper = new Mock<IMapper>();
        var localization = new FakeLocalizationService();
        var erp = new Mock<IErpService>();
        var geocoding = new Mock<IGeocodingService>();
        geocoding.Setup(x => x.GeocodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((decimal Latitude, decimal Longitude)?)null);

        var fileUpload = new Mock<IFileUploadService>();
        fileUpload.Setup(x => x.UploadCustomerImageAsync(It.IsAny<IFormFile>(), It.IsAny<long>()))
            .ReturnsAsync(ApiResponse<string>.ErrorResult("Test.UploadDisabled"));

        var service = new CustomerService(
            uow.Object,
            mapper.Object,
            localization,
            erp.Object,
            NullLogger<CustomerService>.Instance,
            httpContextAccessor,
            geocoding.Object,
            fileUpload.Object);

        return (service, context);
    }

    [Fact]
    public async Task CreateFromMobile_ShouldCreateNewCustomerAndContact_WhenNoMatchExists()
    {
        var (service, context) = await BuildServiceAsync();
        var request = BuildRequest("Yeni Musteri", "new.customer@example.com", "5551000101");

        var result = await service.CreateCustomerFromMobileAsync(request);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data!.CustomerCreated);
        Assert.True(result.Data.ContactCreated);
        Assert.True(result.Data.CustomerId > 0);
        Assert.True(result.Data.ContactId > 0);

        var createdCustomer = await context.Customers.FirstOrDefaultAsync(x => x.Id == result.Data.CustomerId);
        var createdContact = await context.Contacts.FirstOrDefaultAsync(x => x.Id == result.Data.ContactId);
        Assert.NotNull(createdCustomer);
        Assert.NotNull(createdContact);
        Assert.False(createdCustomer!.IsDeleted);
        Assert.False(createdContact!.IsDeleted);
    }

    [Fact]
    public async Task CreateFromMobile_ShouldRestoreDeletedCustomer_WhenEmailOrPhoneMatches()
    {
        var deletedCustomer = new Customer
        {
            CustomerName = "Silinmis Musteri",
            Email = "deleted.customer@example.com",
            Phone1 = "5551000202",
            IsDeleted = true,
            DeletedDate = DateTime.UtcNow
        };

        var (service, context) = await BuildServiceAsync(db =>
        {
            db.Customers.Add(deletedCustomer);
        });

        var request = BuildRequest("Silinmis Musteri", "deleted.customer@example.com", "5551000202");

        var result = await service.CreateCustomerFromMobileAsync(request);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(deletedCustomer.Id, result.Data!.CustomerId);
        Assert.False(result.Data.CustomerCreated);

        var restored = await context.Customers.FirstAsync(x => x.Id == deletedCustomer.Id);
        Assert.False(restored.IsDeleted);
        Assert.Null(restored.DeletedDate);
        Assert.Null(restored.DeletedBy);
    }

    [Fact]
    public async Task CreateFromMobile_ShouldRestoreDeletedContact_AndRelinkToMatchedCustomer()
    {
        var activeCustomer = new Customer
        {
            CustomerName = "Aktif Musteri",
            Email = "active.customer@example.com",
            Phone1 = "5551000303",
            IsDeleted = false
        };

        var deletedContact = new Contact
        {
            FirstName = "Ali",
            LastName = "Veli",
            FullName = "Ali Veli",
            Email = "active.customer@example.com",
            Mobile = "5551000303",
            CustomerId = 999, // intentionally wrong old link
            IsDeleted = true,
            DeletedDate = DateTime.UtcNow
        };

        var (service, context) = await BuildServiceAsync(db =>
        {
            db.Customers.Add(activeCustomer);
            db.Contacts.Add(deletedContact);
        });

        var request = BuildRequest("Aktif Musteri", "active.customer@example.com", "5551000303", "Ali Veli");

        var result = await service.CreateCustomerFromMobileAsync(request);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(deletedContact.Id, result.Data!.ContactId);
        Assert.False(result.Data.ContactCreated);

        var restoredContact = await context.Contacts.FirstAsync(x => x.Id == deletedContact.Id);
        Assert.False(restoredContact.IsDeleted);
        Assert.Equal(activeCustomer.Id, restoredContact.CustomerId);
        Assert.Null(restoredContact.DeletedDate);
        Assert.Null(restoredContact.DeletedBy);
    }

    [Fact]
    public async Task CreateFromMobile_ShouldReturnConflict_WhenMatchingActiveContactExists()
    {
        var matchedCustomer = new Customer
        {
            CustomerName = "Musteri",
            Email = "dup.customer@example.com",
            Phone1 = "5551000404",
            IsDeleted = false
        };

        var otherCustomer = new Customer
        {
            CustomerName = "Baska Musteri",
            Email = "other.customer@example.com",
            Phone1 = "5551000499",
            IsDeleted = false
        };

        var activeContact = new Contact
        {
            FirstName = "Ali",
            LastName = "Veli",
            FullName = "Ali Veli",
            Email = "dup.customer@example.com",
            Mobile = "5551000404",
            IsDeleted = false
        };

        var (service, _) = await BuildServiceAsync(db =>
        {
            db.Customers.Add(matchedCustomer);
            db.Customers.Add(otherCustomer);
            db.SaveChanges();
            activeContact.CustomerId = otherCustomer.Id;
            db.Contacts.Add(activeContact);
        });

        var request = BuildRequest("Musteri", "dup.customer@example.com", "5551000404", "Ali Veli");

        var result = await service.CreateCustomerFromMobileAsync(request);

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
    }

    [Fact]
    public async Task CreateFromMobile_ShouldReuseActiveContact_WhenItAlreadyBelongsToMatchedCustomer()
    {
        var activeCustomer = new Customer
        {
            CustomerName = "Musteri",
            Email = "same.customer@example.com",
            Phone1 = "5551000505",
            IsDeleted = false
        };

        var activeContact = new Contact
        {
            FirstName = "Ali",
            LastName = "Veli",
            FullName = "Ali Veli",
            Email = "same.customer@example.com",
            Mobile = "5551000505",
            CustomerId = activeCustomer.Id,
            IsDeleted = false
        };

        var (service, context) = await BuildServiceAsync(db =>
        {
            db.Customers.Add(activeCustomer);
            db.SaveChanges();
            activeContact.CustomerId = activeCustomer.Id;
            db.Contacts.Add(activeContact);
        });

        var request = BuildRequest("Musteri", "same.customer@example.com", "5551000505", "Ali Veli");

        var result = await service.CreateCustomerFromMobileAsync(request);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(activeCustomer.Id, result.Data!.CustomerId);
        Assert.Equal(activeContact.Id, result.Data.ContactId);
        Assert.False(result.Data.CustomerCreated);
        Assert.False(result.Data.ContactCreated);

        var existingContact = await context.Contacts.FirstAsync(x => x.Id == activeContact.Id);
        Assert.Equal(activeCustomer.Id, existingContact.CustomerId);
        Assert.False(existingContact.IsDeleted);
    }

    [Fact]
    public async Task CreateFromMobile_ShouldReturnConflict_WhenCustomerContactMatchBelongsToDifferentCompany()
    {
        var otherCustomer = new Customer
        {
            CustomerName = "Baska Sirket",
            Email = "shared.customer@example.com",
            Phone1 = "5551000606",
            IsDeleted = false
        };

        var (service, _) = await BuildServiceAsync(db =>
        {
            db.Customers.Add(otherCustomer);
        });

        var request = BuildRequest("Dogru Sirket", "shared.customer@example.com", "5551000606", "Ali Veli");

        var result = await service.CreateCustomerFromMobileAsync(request);

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
    }
}
