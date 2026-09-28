using Bookstore.Data;
using Bookstore.Data.FileServices;
using Bookstore.Data.ImageResizeService;
using Bookstore.Data.ImageValidationServices;
using Bookstore.Data.Repositories;
using Bookstore.Domain;
using Bookstore.Domain.Addresses;
using Bookstore.Domain.Books;
using Bookstore.Domain.Carts;
using Bookstore.Domain.Customers;
using Bookstore.Domain.Offers;
using Bookstore.Domain.Orders;
using Bookstore.Domain.ReferenceData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bookstore.Web
{
    public static class DependencyInjectionSetup
    {
        public static IServiceCollection AddBookstoreServices(
            this IServiceCollection services, IWebHostEnvironment env)
        {
            // DbContext — scoped so all repositories in one request share the same EF unit of work
            services.AddScoped(sp =>
            {
                var connStr = ConfigurationManager.Configuration
                                  .GetConnectionString("BookstoreDatabaseConnection")
                              ?? "Server=localhost;Initial Catalog=BobsBookstore;MultipleActiveResultSets=true;Integrated Security=SSPI;TrustServerCertificate=True;";
                return new ApplicationDbContext(connStr);
            });

            // Repositories
            services.AddScoped<IBookRepository, BookRepository>();
            services.AddScoped<IOrderRepository, OrderRepository>();
            services.AddScoped<IOfferRepository, OfferRepository>();
            services.AddScoped<IAddressRepository, AddressRepository>();
            services.AddScoped<ICustomerRepository, CustomerRepository>();
            services.AddScoped<IShoppingCartRepository, ShoppingCartRepository>();
            services.AddScoped<IReferenceDataRepository, ReferenceDataRepository>();

            // Image resize — single implementation
            services.AddScoped<IImageResizeService, ImageResizeService>();

            // File service — local vs S3 based on configuration
            var fileServiceMode = ConfigurationManager.Configuration["Services:FileService"]
                                  ?? ConfigurationManager.Configuration["Services/FileService"]
                                  ?? "local";
            if (fileServiceMode == "aws")
            {
                services.AddScoped<IFileService, S3FileService>();
            }
            else
            {
                services.AddScoped<IFileService>(sp =>
                    new LocalFileService(env.ContentRootPath));
            }

            // Image validation — local vs Rekognition based on configuration
            var imageValidationMode = ConfigurationManager.Configuration["Services:ImageValidationService"]
                                      ?? ConfigurationManager.Configuration["Services/ImageValidationService"]
                                      ?? "local";
            if (imageValidationMode == "aws")
            {
                services.AddScoped<IImageValidationService, RekognitionImageValidationService>();
            }
            else
            {
                services.AddScoped<IImageValidationService, LocalImageValidationService>();
            }

            // Domain services
            services.AddScoped<IBookService, BookService>();
            services.AddScoped<IOfferService, OfferService>();
            services.AddScoped<IOrderService, OrderService>();
            services.AddScoped<IAddressService, AddressService>();
            services.AddScoped<ICustomerService, CustomerService>();
            services.AddScoped<IReferenceDataService, ReferenceDataService>();
            services.AddScoped<IShoppingCartService, ShoppingCartService>();

            return services;
        }
    }
}
