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
        var faker = new ProductDetails().GetBogusFaker();

        foreach (var product in faker.GenerateLazy(50))
        {
            var productGrain = _grainFactory.GetGrain<IProductGrain>(product.Id);
            await productGrain.CreateOrUpdateProductAsync(product);
            await CraeteStripeProduct(product);
        }
    }

    private async Task CraeteStripeProduct(ProductDetails product)
    {
        var prodCreateOptions = new ProductCreateOptions
        {
            Name = product.Name,
            Description = product.Description,
            Shippable = true,
            Images = new List<string> { product.ImageUrl },
            Metadata = new Dictionary<string, string>
            {
                ["uniqueId"] = product.Id,
                ["category"] = product.Category.ToString()
            }
        };

        var newProduct = await _prodSvc.CreateAsync(prodCreateOptions);

        var priceCreateOptions = new PriceCreateOptions
        {
            Product = newProduct.Id,
            UnitAmountDecimal = (product.UnitPrice * 100),            
            Currency = "usd",
        };

        var prodPrice = await _priceSvc.CreateAsync(priceCreateOptions);
    }
}
