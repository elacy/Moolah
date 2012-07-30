﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Web;
using System.Linq;

namespace Moolah.PayPal
{
    public interface IPayPalResponseParser
    {
        PayPalExpressCheckoutToken SetExpressCheckout(NameValueCollection payPalResponse);
        PayPalExpressCheckoutDetails GetExpressCheckoutDetails(NameValueCollection payPalResponse);
        IPaymentResponse DoExpressCheckoutPayment(NameValueCollection payPalResponse);
    }

    public class PayPalResponseParser : IPayPalResponseParser
    {
        private readonly PayPalConfiguration _configuration;

        public PayPalResponseParser(PayPalConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException("configuration");
            _configuration = configuration;
        }

        public PayPalExpressCheckoutToken SetExpressCheckout(NameValueCollection payPalResponse)
        {
            if (payPalResponse == null) throw new ArgumentNullException("payPalResponse");

            var response = new PayPalExpressCheckoutToken { PayPalResponse = payPalResponse };

            parsePayPalAck(payPalResponse,
                success: () =>
                {
                    response.Status = PaymentStatus.Pending;
                    response.PayPalToken = payPalResponse["TOKEN"];
                    response.RedirectUrl = string.Format(_configuration.CheckoutUrlFormat,
                                                         HttpUtility.UrlEncode(response.PayPalToken));
                },
                fail: message =>
                {
                    response.Status = PaymentStatus.Failed;
                    response.IsSystemFailure = true;
                    response.FailureMessage = message;
                });

            return response;
        }

        public PayPalExpressCheckoutDetails GetExpressCheckoutDetails(NameValueCollection payPalResponse)
        {
            PayPalExpressCheckoutDetails response = null;

            parsePayPalAck(payPalResponse,
                success: () =>
                {
                    response = new PayPalExpressCheckoutDetails(payPalResponse)
                    {
                        CustomerPhoneNumber = payPalResponse["PHONENUM"],
                        CustomerMarketingEmail = payPalResponse["BUYERMARKETINGEMAIL"],
                        PayPalEmail = payPalResponse["EMAIL"],
                        PayPalPayerId = payPalResponse["PAYERID"],
                        CustomerTitle = payPalResponse["SALUTATION"],
                        CustomerFirstName = payPalResponse["FIRSTNAME"],
                        CustomerLastName = payPalResponse["LASTNAME"],
                        DeliveryName = payPalResponse["PAYMENTREQUEST_0_SHIPTONAME"],
                        DeliveryStreet1 = payPalResponse["PAYMENTREQUEST_0_SHIPTOSTREET"],
                        DeliveryStreet2 = payPalResponse["PAYMENTREQUEST_0_SHIPTOSTREET2"],
                        DeliveryCity = payPalResponse["PAYMENTREQUEST_0_SHIPTOCITY"],
                        DeliveryState = payPalResponse["PAYMENTREQUEST_0_SHIPTOSTATE"],
                        DeliveryPostcode = payPalResponse["PAYMENTREQUEST_0_SHIPTOZIP"],
                        DeliveryCountryCode = payPalResponse["PAYMENTREQUEST_0_SHIPTOCOUNTRYCODE"],
                        DeliveryPhoneNumber = payPalResponse["PAYMENTREQUEST_0_SHIPTOPHONENUM"],
                        OrderDetails = parseOrderDetails(payPalResponse)
                    };
                },
                fail: message =>
                {
                    throw new Exception(message);
                });

            return response;
        }

        OrderDetails parseOrderDetails(NameValueCollection payPalResponse)
        {
            var orderDetails = new OrderDetails
            {
                OrderTotal = decimal.Parse(payPalResponse["PAYMENTREQUEST_0_AMT"]),
                TaxTotal = parseOptionalDecimalValueFromResponse("PAYMENTREQUEST_0_TAXAMT", payPalResponse),
                ShippingTotal = parseOptionalDecimalValueFromResponse("PAYMENTREQUEST_0_SHIPPINGAMT", payPalResponse),
                ShippingDiscount = parseOptionalDecimalValueFromResponse("PAYMENTREQUEST_0_SHIPDISCAMT", payPalResponse),
                AllowNote = parseOptionalBooleanValueFromResponse("ALLOWNOTE", payPalResponse),
                OrderDescription = parseOptionalStringValueFromResponse("PAYMENTREQUEST_0_DESC", payPalResponse),
                CurrencyCodeType = parseOptionalEnumValueFromResponse<CurrencyCodeType>("PAYMENTREQUEST_0_CURRENCYCODE", payPalResponse),
                CustomField = payPalResponse["PAYMENTREQUEST_0_CUSTOM"]
            };

            var orderDetailsItems = new Dictionary<int, OrderDetailsItem>();
            var discountDetailses = new Dictionary<int, DiscountDetails>();

            foreach (var key in payPalResponse.AllKeys.Where(x => x.StartsWith("L_PAYMENTREQUEST_0_AMT")).OrderBy(x => x))
            {
                var itemNumber = int.Parse(key.Substring("L_PAYMENTREQUEST_0_AMT".Length));
                var amount = decimal.Parse(payPalResponse[key]);
                var isDiscount = amount < 0;

                if (isDiscount)
                    discountDetailses.Add(itemNumber, new DiscountDetails { Amount = amount });
                else
                    orderDetailsItems.Add(itemNumber, new OrderDetailsItem { UnitPrice = amount });
            }

            parseLineNumbers(payPalResponse, orderDetailsItems);
            parseLineDescritions(payPalResponse, orderDetailsItems);
            parseLineItemUrls(payPalResponse, orderDetailsItems);
            parseLineNames(payPalResponse, orderDetailsItems, discountDetailses);
            parseLineQuantities(payPalResponse, orderDetailsItems, discountDetailses);
            parseLineTaxAmounts(payPalResponse, orderDetailsItems, discountDetailses);

            if (orderDetailsItems.Values.Any())
                orderDetails.Items = orderDetailsItems.Values;
            if (discountDetailses.Values.Any())
                orderDetails.Discounts = discountDetailses.Values;

            return orderDetails;
        }

        void parseLineNumbers(NameValueCollection payPalResponse, IDictionary<int, OrderDetailsItem> orderDetailsItems)
        {
            foreach (var key in payPalResponse.AllKeys.Where(x => x.StartsWith("L_PAYMENTREQUEST_0_NUMBER")).OrderBy(x => x))
            {
                var itemNumber = int.Parse(key.Substring("L_PAYMENTREQUEST_0_NUMBER".Length));

                if (orderDetailsItems.ContainsKey(itemNumber))
                    orderDetailsItems[itemNumber].Number = parseOptionalIntegerValueFromResponse(key, payPalResponse);
            }
        }

        void parseLineDescritions(NameValueCollection payPalResponse, IDictionary<int, OrderDetailsItem> orderDetailsItems)
        {
            foreach (var key in payPalResponse.AllKeys.Where(x => x.StartsWith("L_PAYMENTREQUEST_0_DESC")).OrderBy(x => x))
            {
                var itemNumber = int.Parse(key.Substring("L_PAYMENTREQUEST_0_DESC".Length));

                if (orderDetailsItems.ContainsKey(itemNumber))
                    orderDetailsItems[itemNumber].Description = parseOptionalStringValueFromResponse(key, payPalResponse);
            }
        }

        void parseLineItemUrls(NameValueCollection payPalResponse, IDictionary<int, OrderDetailsItem> orderDetailsItems)
        {
            foreach (var key in payPalResponse.AllKeys.Where(x => x.StartsWith("L_PAYMENTREQUEST_0_ITEMURL")).OrderBy(x => x))
            {
                var itemNumber = int.Parse(key.Substring("L_PAYMENTREQUEST_0_ITEMURL".Length));

                if (orderDetailsItems.ContainsKey(itemNumber))
                    orderDetailsItems[itemNumber].ItemUrl = parseOptionalStringValueFromResponse(key, payPalResponse);
            }
        }

        void parseLineNames(NameValueCollection payPalResponse, IDictionary<int, OrderDetailsItem> orderDetailsItems, IDictionary<int, DiscountDetails> discountDetailses)
        {
            foreach (var key in payPalResponse.AllKeys.Where(x => x.StartsWith("L_PAYMENTREQUEST_0_NAME")).OrderBy(x => x))
            {
                var itemNumber = int.Parse(key.Substring("L_PAYMENTREQUEST_0_NAME".Length));

                if (orderDetailsItems.ContainsKey(itemNumber))
                    orderDetailsItems[itemNumber].Name = parseOptionalStringValueFromResponse(key, payPalResponse);
                if (discountDetailses.ContainsKey(itemNumber))
                    discountDetailses[itemNumber].Description = parseOptionalStringValueFromResponse(key, payPalResponse);
            }
        }

        void parseLineQuantities(NameValueCollection payPalResponse, IDictionary<int, OrderDetailsItem> orderDetailsItems, IDictionary<int, DiscountDetails> discountDetailses)
        {
            foreach (var key in payPalResponse.AllKeys.Where(x => x.StartsWith("L_PAYMENTREQUEST_0_QTY")).OrderBy(x => x))
            {
                var itemNumber = int.Parse(key.Substring("L_PAYMENTREQUEST_0_QTY".Length));

                if (orderDetailsItems.ContainsKey(itemNumber))
                    orderDetailsItems[itemNumber].Quantity = parseOptionalIntegerValueFromResponse(key, payPalResponse);
                if (discountDetailses.ContainsKey(itemNumber))
                    discountDetailses[itemNumber].Quantity = parseOptionalIntegerValueFromResponse(key, payPalResponse);
            }
        }

        void parseLineTaxAmounts(NameValueCollection payPalResponse, IDictionary<int, OrderDetailsItem> orderDetailsItems, IDictionary<int, DiscountDetails> discountDetailses)
        {
            foreach (var key in payPalResponse.AllKeys.Where(x => x.StartsWith("L_PAYMENTREQUEST_0_TAXAMT")).OrderBy(x => x))
            {
                var itemNumber = int.Parse(key.Substring("L_PAYMENTREQUEST_0_TAXAMT".Length));

                if (orderDetailsItems.ContainsKey(itemNumber))
                    orderDetailsItems[itemNumber].Tax = parseOptionalDecimalValueFromResponse(key, payPalResponse);
                if (discountDetailses.ContainsKey(itemNumber))
                    discountDetailses[itemNumber].Tax = parseOptionalDecimalValueFromResponse(key, payPalResponse);
            }
        }
        
        int? parseOptionalIntegerValueFromResponse(string fieldName, NameValueCollection payPalResponse)
        {
            return string.IsNullOrWhiteSpace(payPalResponse[fieldName])
                       ? (int?)null
                       : int.Parse(payPalResponse[fieldName]);
        }

        decimal? parseOptionalDecimalValueFromResponse(string fieldName, NameValueCollection payPalResponse)
        {
            return string.IsNullOrWhiteSpace(payPalResponse[fieldName])
                       ? (decimal?)null
                       : decimal.Parse(payPalResponse[fieldName]);
        }

        bool? parseOptionalBooleanValueFromResponse(string fieldName, NameValueCollection payPalResponse)
        {
            return string.IsNullOrWhiteSpace(payPalResponse[fieldName])
                       ? (bool?)null
                       : payPalResponse[fieldName] == "1";
        }
        T parseOptionalEnumValueFromResponse<T>(string fieldName, NameValueCollection payPalResponse) where T : struct
        {
            T value;
            Enum.TryParse(payPalResponse[fieldName], out value);
            return value;
        }
        string parseOptionalStringValueFromResponse(string fieldName, NameValueCollection payPalResponse)
        {
            return string.IsNullOrWhiteSpace(payPalResponse[fieldName])
                       ? null
                       : payPalResponse[fieldName];
        }


        public IPaymentResponse DoExpressCheckoutPayment(NameValueCollection payPalResponse)
        {
            var response = new PayPalPaymentResponse(payPalResponse);

            parsePayPalAck(payPalResponse, 
                success: () =>
                {
                    response.TransactionReference = payPalResponse["PAYMENTINFO_0_TRANSACTIONID"];

                    var rawPaymentStatus = payPalResponse["PAYMENTINFO_0_PAYMENTSTATUS"];
                    PayPalPaymentStatus paymentStatus;
                    if (Enum.TryParse(rawPaymentStatus, true, out paymentStatus))
                    {
                        response.Status = paymentStatus == PayPalPaymentStatus.Pending
                                              ? PaymentStatus.Pending
                                              : PaymentStatus.Successful;
                    }
                    else
                    {
                        response.Status = PaymentStatus.Failed;
                        response.FailureMessage = "An error occurred processing your PayPal payment.";
                    }
                    response.ErrorCode = parseOptionalStringValueFromResponse("PAYMENTREQUEST_0_ERRORCODE", payPalResponse);
                    response.ErrorShortMsg = parseOptionalStringValueFromResponse("PAYMENTREQUEST_0_SHORTMESSAGE", payPalResponse);
                    response.ErrorLongMsg = parseOptionalStringValueFromResponse("PAYMENTREQUEST_0_LONGMESSAGE", payPalResponse);
                    response.ErrorSeverityCode = parseOptionalStringValueFromResponse("PAYMENTREQUEST_0_SEVERITYCODE", payPalResponse);
                },
                fail: message =>
                {
                    response.Status = PaymentStatus.Failed;
                    response.IsSystemFailure = true;
                    response.FailureMessage = message;
                });

            return response;
        }

        private static void parsePayPalAck(NameValueCollection payPalResponse, Action success, Action<string> fail)
        {
            PayPalAck payPalStatus;
            if (!Enum.TryParse(payPalResponse["ACK"], true, out payPalStatus))
                throw new InvalidOperationException("Invalid PayPal ACK value: " + payPalResponse["ACK"]);

            switch (payPalStatus)
            {
                case PayPalAck.Success:
                case PayPalAck.SuccessWithWarning:
                    success();
                    break;
                default:
                    var failureMessage = string.Format(
                        "PayPal error code: {0}\n" +
                        "Short message: {1}\n" +
                        "Long message: {2}",
                        payPalResponse["L_ERRORCODE0"], payPalResponse["L_SHORTMESSAGE0"], payPalResponse["L_LONGMESSAGE0"]);
                    fail(failureMessage);
                    break;
            }
        }
    }
}