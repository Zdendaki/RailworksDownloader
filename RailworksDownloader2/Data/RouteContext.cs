using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RailworksDownloader2.Data
{
    class RouteContext : DbContext
    {
        public DbSet<Package> Packages { get; set; }

        public DbSet<File> PackageFiles { get; set; }

        public DbSet<File> InstalledFiles { get; set; }


    }
}
