using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Moolah.PayPal
{
    [Serializable]
    public class Address
    {
        public string Name { get; set; }
        public string Street1 { get; set; }
        public string Street2 { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Zip { get; set; }
        public string CountryCode { get; set; }
        public string PhoneNumber { get; set; }
    }
}
