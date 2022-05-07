// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT License.

using Stripe;

namespace Orleans.ShoppingCart.Silo.StartupTasks;

public sealed class SeedProductStoreTask : IStartupTask
{
    private readonly IGrainFactory _grainFactory;
    private readonly Stripe.ProductService _prodSvc;
    private readonly Stripe.PriceService _priceSvc;

    public SeedProductStoreTask(IGrainFactory grainFactory, IStripeClient stripeClient)
    {
        _grainFactory = grainFactory;
        _prodSvc = new(stripeClient);
        _priceSvc = new(stripeClient);
    }

    async Task IStartupTask.Execute(CancellationToken cancellationToken)
    {
        var searchOptions = new PriceSearchOptions
        {
            Limit = 10,
            Expand = new List<string> { "data.product" },
            Query = "metadata['demo_product']:'true'"

        };
        var searchResults = await _priceSvc.SearchAsync(searchOptions);
        if (searchResults.Any())
        {
            // Populate grains from Stripe products
            while (true)
            {
                foreach (var stripePrice in searchResults)
                {
                    var stripeProduct = stripePrice.Product;
                    var product = new ProductDetails
                    {
                        Id = stripeProduct.Metadata["uniqueId"],
                        Name = stripeProduct.Name,
                        Description = stripeProduct.Description,
                        Quantity = int.Parse(stripeProduct.Metadata["uniqueId"]),
                        ImageUrl = stripeProduct.Images.First(),
                        Category = (ProductCategory)Enum.Parse(typeof(ProductCategory), stripeProduct.Metadata["category"]),
                        DetailsUrl = stripeProduct.Metadata["detailsUrl"],
                        StripePriceId = stripePrice.Id
                    };

                    var productGrain = _grainFactory.GetGrain<IProductGrain>(product.Id);
                    await productGrain.CreateOrUpdateProductAsync(product);
                }

                if (!searchResults.HasMore) break;
                searchOptions.Page = searchResults.NextPage;
                searchResults = await _priceSvc.SearchAsync(searchOptions);
            }
        }
        else
        {
            // Generate fake products from Bogus
            var faker = new ProductDetails().GetBogusFaker();

            foreach (var product in faker.GenerateLazy(50))
            {
                var stripePrice = await CreateStripeProduct(product);
                product.StripePriceId = stripePrice.Id;
                var productGrain = _grainFactory.GetGrain<IProductGrain>(product.Id);
                await productGrain.CreateOrUpdateProductAsync(product);
            }
        }
    }

    private async Task<Price> CreateStripeProduct(ProductDetails product)
    {
        // Create products in Stripe account
        var prodCreateOptions = new ProductCreateOptions
        {
            Name = product.Name,
            Description = product.Description,
            Shippable = true,
            Images = new List<string> { product.ImageUrl },
            Metadata = new Dictionary<string, string>
            {
                ["uniqueId"] = product.Id,
                ["category"] = product.Category.ToString(),
                ["demo_product"] = "true",
                ["detailsUrl"] = product.DetailsUrl,
                ["quantity"] = product.Quantity.ToString()
            }
        };

        var newProduct = await _prodSvc.CreateAsync(prodCreateOptions);

        // Attach USD price to product
        var priceCreateOptions = new PriceCreateOptions
        {
            Product = newProduct.Id,
            Expand = new List<string> { "product" },
            UnitAmountDecimal = (product.UnitPrice * 100),
            Currency = "usd",
            Metadata = new Dictionary<string, string>
            {
                ["demo_product"] = "true",
            }
        };

        var prodPrice = await _priceSvc.CreateAsync(priceCreateOptions);
        return prodPrice;
    }
}
