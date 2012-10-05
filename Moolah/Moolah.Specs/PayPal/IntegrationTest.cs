﻿using Machine.Specifications;
using Moolah.PayPal;

namespace Moolah.Specs.PayPal
{
    [Behaviors]
    public class SuccessfulExpressCheckoutBehavior
    {
        It should_get_a_successful_response = () =>
            Response.Status.ShouldEqual(PaymentStatus.Pending);

        It should_not_specify_a_failure_message = () =>
            Response.FailureMessage.ShouldBeNull();

        It should_get_a_checkout_token = () =>
            Response.PayPalToken.ShouldNotBeEmpty();

        It should_provide_a_redirect_url = () =>
            {
                Response.RedirectUrl.ShouldNotBeEmpty();
                System.Diagnostics.Debug.WriteLine("Redirect to: " + Response.RedirectUrl);
            };

        protected static PayPalExpressCheckoutToken Response;
    }

    [Subject(typeof(PayPalExpressCheckout))]
    public class When_starting_an_express_checkout_for_an_amount
    {
        Behaves_like<SuccessfulExpressCheckoutBehavior> a_successful_express_checkout;

        Because of = () =>
            Response = Gateway.SetExpressCheckout(16m,CurrencyCodeType.GBP,  "http://localhost/cancel", "http://localhost/confirm");

        Establish context = () =>
            Gateway = new PayPalExpressCheckout(new PayPalConfiguration(PaymentEnvironment.Test));

        protected static PayPalExpressCheckoutToken Response;
        static PayPalExpressCheckout Gateway;
    }

    public class When_starting_express_checkout_without_line_details : ExpressCheckoutContext
    {
        Behaves_like<SuccessfulExpressCheckoutBehavior> a_successful_express_checkout;

        Establish context = () =>
                OrderDetails = new OrderDetails
                {
                    OrderDescription = "Totals only",
                    OrderTotal = 14.98m,
                    CurrencyCodeType = CurrencyCodeType.GBP
                };
    }

    public class When_starting_express_checkout_specifying_unit_prices : ExpressCheckoutContext
    {
        Behaves_like<SuccessfulExpressCheckoutBehavior> a_successful_express_checkout;

        Establish context = () =>
                OrderDetails = new OrderDetails
                                   {
                                       OrderDescription = "Some order",
                                       Items = new[]
                                                   {
                                                       new OrderDetailsItem
                                                           {
                                                               Description = "First Item",
                                                               Name = "FIRST",
                                                               Number = 1,
                                                               Quantity = 2,
                                                               UnitPrice = 1.99m,
                                                               ItemUrl = "http://localhost/product?1"
                                                           },
                                                       new OrderDetailsItem
                                                           {
                                                               Description = "Second Item",
                                                               Name = "2ND",
                                                               Number = 2,
                                                               Quantity = 1,
                                                               UnitPrice = 11m, 
                                                               ItemUrl = "http://localhost/product?2"
                                                           }
                                                   },
                                       ShippingTotal = 2m,
                                       CurrencyCodeType = CurrencyCodeType.GBP,
                                       OrderTotal = 16.98m // OrderTotal = (sum of unitprice * quantity) + shippingtotal
                                   };
    }

    public class When_starting_express_checkout_specifying_unit_prices_and_tax_total_for_an_order : ExpressCheckoutContext
    {
        Behaves_like<SuccessfulExpressCheckoutBehavior> a_successful_express_checkout;

        Establish context = () =>
            OrderDetails = new OrderDetails
            {
                OrderDescription = "Tax Total Specified",
                Items = new[]
                    {
                        new OrderDetailsItem
                            {
                                Description = "First Item",
                                Name = "FIRST",
                                Number = 1,
                                Quantity = 2,
                                UnitPrice = 1.99m,
                                ItemUrl = "http://localhost/product?1"
                            },
                        new OrderDetailsItem
                            {
                                Description = "Second Item",
                                Name = "2ND",
                                Number = 2,
                                Quantity = 1,
                                UnitPrice = 11m, 
                                ItemUrl = "http://localhost/product?2"
                            }
                    },
                TaxTotal = 3m,
                CurrencyCodeType = CurrencyCodeType.GBP,
                OrderTotal = 17.98m // Order Total must equal sum of unit price * quantity + taxtotal
            };
    }

    public class When_starting_express_checkout_with_shipping_discount : ExpressCheckoutContext
    {
        Behaves_like<SuccessfulExpressCheckoutBehavior> a_successful_express_checkout;

        Establish context = () =>
            {
                OrderDetails = new OrderDetails
                                   {
                                       Items = new[]
                                                   {
                                                       new OrderDetailsItem { Quantity = 1, UnitPrice = 10m }
                                                   },
                                       ShippingTotal = 2m,
                                       ShippingDiscount = -1m,
                                       CurrencyCodeType = CurrencyCodeType.GBP,
                                       OrderTotal = 11m
                                   };            
            };

    }

    public class When_starting_express_checkout_with_discounts : ExpressCheckoutContext
    {
        Behaves_like<SuccessfulExpressCheckoutBehavior> a_successful_express_checkout;

        Establish context = () =>
        {
            OrderDetails = new OrderDetails
            {
                Items = new[]
                    {
                        new OrderDetailsItem { Quantity = 1, UnitPrice = 10m }
                    },
                Discounts = new[]
                    {
                        new DiscountDetails{ Amount = 2m, Description = "Loyalty discount" }
                    },
                OrderTotal = 8m,
                CurrencyCodeType = CurrencyCodeType.GBP
            };
        };

    }

    [Subject(typeof(PayPalExpressCheckout))]
    public abstract class ExpressCheckoutContext
    {
        Because of = () =>
            Response = Gateway.SetExpressCheckout(OrderDetails, "http://localhost/cancel", "http://localhost/confirm");

        Establish context = () =>
            Gateway = new PayPalExpressCheckout(new PayPalConfiguration(PaymentEnvironment.Test));

        protected static PayPalExpressCheckoutToken Response;
        protected static OrderDetails OrderDetails;
        static PayPalExpressCheckout Gateway;
    }
}
