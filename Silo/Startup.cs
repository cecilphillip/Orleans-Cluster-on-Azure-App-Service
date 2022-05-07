// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT License.

namespace Orleans.ShoppingCart.Silo;

public sealed class Startup
{
    public IConfiguration Configuration { get; }

    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddMudServices();
        services.AddRazorPages();
        services.AddServerSideBlazor();
        services.AddHttpContextAccessor();
        services.AddSingleton<ShoppingCartService>();
        services.AddSingleton<InventoryService>();
        services.AddSingleton<Services.ProductService>();
        services.AddScoped<ComponentStateChangedObserver>();
        services.AddSingleton<ToastService>();
        services.AddLocalStorageServices();
        services.AddApplicationInsights("Silo");
        services.AddStripe(Configuration.GetSection("Stripe"));
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapPost("/webhook", HandleWebhook);
            endpoints.MapBlazorHub();
            endpoints.MapFallbackToPage("/_Host");
        });

        async Task HandleWebhook(HttpContext context)
        {
            var payload = await new StreamReader(context.Request.Body).ReadToEndAsync();
            Stripe.Event stripeEvent;

            try
            {
                // Validate webhook source
                var webookSecret = Configuration.GetSection("Stripe")["WEBHOOK_SECRET"];
                stripeEvent = Stripe.EventUtility.ConstructEvent(
                    payload, context.Request.Headers["Stripe-Signature"], webookSecret
                );

                // Handle checkout completed webhook event
                if (stripeEvent.Type == Stripe.Events.CheckoutSessionCompleted)
                {
                    var checkoutSession = stripeEvent.Data.Object as Stripe.Checkout.Session;

                    // Check session status and handle fullfilment workflow

                    context.Response.StatusCode = StatusCodes.Status200OK;
                    await context.Response.WriteAsync($"Checkout status => {checkoutSession!.Status}");
                }
            }
            catch (Stripe.StripeException)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Bad things happened");
            }
        }
    }
}