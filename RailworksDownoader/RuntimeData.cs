using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RailworksDownoader
{
    class RuntimeData
    {
        internal Railworks Railworks { get; set; }

        internal List<RouteCrawler> Routes { get; set; }

        internal RuntimeData()
        {
            Railworks = new Railworks();
            Routes = new List<RouteCrawler>();
        }
    }
}
