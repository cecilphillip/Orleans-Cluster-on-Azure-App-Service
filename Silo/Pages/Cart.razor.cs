// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT License.

using Stripe;
using Stripe.Checkout;

namespace Orleans.ShoppingCart.Silo.Pages;

public sealed partial class Cart
{
    private HashSet<CartItem>? _cartItems;

    [Inject]
    public ShoppingCartService ShoppingCart { get; set; } = null!;

    [Inject]
    public IStripeClient CustomStripeClient { get; set; } = null!;

    [Inject]
    public NavigationManager Navigator { get; set; } = null!;

    [Inject]
    public ComponentStateChangedObserver Observer { get; set; } = null!;

    protected override Task OnInitializedAsync() => GetCartItemsAsync();

    private Task GetCartItemsAsync() =>
        InvokeAsync(async () =>
        {
            _cartItems = await ShoppingCart.GetAllItemsAsync();
            StateHasChanged();
        });

    private async Task OnItemRemovedAsync(ProductDetails product)
    {
        await ShoppingCart.RemoveItemAsync(product);
        await Observer.NotifyStateChangedAsync();

        _ = _cartItems?.RemoveWhere(item => item.Product == product);
    }

    private async Task OnItemUpdatedAsync((int Quantity, ProductDetails Product) tuple)
    {
        await ShoppingCart.AddOrUpdateItemAsync(tuple.Quantity, tuple.Product);
        await GetCartItemsAsync();
    }

    private async Task OnCheckoutAsync()
    {
        if (_cartItems is null) return;

        var baseUrl = Navigator.BaseUri;

        var lineItems = _cartItems.Select(item => new SessionLineItemOptions
        {
            Price = item.Product.StripePriceId,
            Quantity = item.Quantity
        }).ToList();

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            LineItems = lineItems,
            ShippingAddressCollection = new()
            {
                AllowedCountries = new List<string> { "US" }
            },
            SuccessUrl = baseUrl + "success?session_id={CHECKOUT_SESSION_ID}",
            CancelUrl = baseUrl
        };

        var sessionService = new SessionService(CustomStripeClient);
        var session = await sessionService.CreateAsync(options);
                
        Navigator.NavigateTo(session.Url);
    }

    private async Task EmptyCartAsync()
    {
        await ShoppingCart.EmptyCartAsync();
        await Observer.NotifyStateChangedAsync();

        _cartItems?.Clear();
    }
}
