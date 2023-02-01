using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DownloadStationClient.API.Data
{
    public class Package
    {
        public int PackageId { get; set; }

        public string FileName { get; set; }

        public string DisplayName { get; set; }

        public int Category { get; set; }

        [JsonIgnore]
        public string CategoryString
        {
            get
            {
                switch (Category)
                {
                    case 0:
                        return Localization.Strings.CatLoco;
                    case 1:
                        return Localization.Strings.CatWag;
                    case 2:
                        return Localization.Strings.CatRVDep;
                    case 3:
                        return Localization.Strings.CatScenery;
                    case 4:
                        return Localization.Strings.CatTrackObj;
                    case 5:
                        return Localization.Strings.CatEnv;
                    default:
                        return Localization.Strings.CatOther;
                }
            }
        }

        public int Era { get; set; }

        [JsonIgnore]
        public string EraString
        {
            get
            {
                switch (Era)
                {
                    case 1:
                        return Localization.Strings.Era1;
                    case 2:
                        return Localization.Strings.Era2;
                    case 3:
                        return Localization.Strings.Era3;
                    case 4:
                        return Localization.Strings.Era4;
                    case 5:
                        return Localization.Strings.Era5;
                    case 6:
                        return Localization.Strings.Era6;
                    default:
                        return Localization.Strings.EraNon;
                }
            }
        }

        public int Country { get; set; }

        [JsonIgnore]
        public string CountryString
        {
            get
            {
                switch (Country)
                {
                    default:
                        return Localization.Strings.CountryNon;
                }
            }
        }

        public int Version { get; set; }

        public int Owner { get; set; }

        public DateTime Datetime { get; set; }

        public string Description { get; set; }

        public string TargetPath { get; set; }

        public bool IsPaid { get; set; }

        public int SteamAppID { get; set; }

        public List<string> FilesContained { get; set; }

        public List<int> Dependencies { get; set; }

        public Package(int package_id, string display_name, int category, int era, int country, int owner, string date_time, string target_path, List<string> deps_contained, string file_name = "", string description = "", int version = 1, bool isPaid = false, int steamappid = -1, List<int> dependencies = null)
        {
            PackageId = package_id;
            FileName = file_name;
            DisplayName = display_name;
            Category = category;
            Era = era;
            Country = country;
            Version = version;
            Owner = owner;
            Datetime = Convert.ToDateTime(date_time);
            Description = description;
            TargetPath = target_path;
            IsPaid = isPaid;
            SteamAppID = steamappid;
            FilesContained = deps_contained;
            Dependencies = dependencies ?? new List<int>();
        }

        public Package(QueryContent packageJson)
        {
            PackageId = packageJson.ID;
            FileName = packageJson.FileName;
            DisplayName = packageJson.DisplayName;
            Category = packageJson.Category;
            Era = packageJson.Era;
            Country = packageJson.Country;
            Version = packageJson.Version;
            Owner = packageJson.Owner;
            Datetime = Convert.ToDateTime(packageJson.Created);
            Description = packageJson.Description;
            TargetPath = packageJson.TargetPath;
            IsPaid = packageJson.Paid;
            SteamAppID = packageJson.SteamAppID ?? 0;
            if (packageJson.Files.Count > 0)
                FilesContained = packageJson.Files.Select(x => Utils.NormalizePath(x)).Distinct().ToList();
            else
                FilesContained = new List<string>();
            Dependencies = packageJson.Dependencies;
        }
    }
}
