﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace RailworksDownloader {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "16.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class SQLqueries {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal SQLqueries() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("RailworksDownloader.SQLqueries", typeof(SQLqueries).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to ALTER TABLE {0} ADD COLUMN {1} {2};.
        /// </summary>
        internal static string AddColumn {
            get {
                return ResourceManager.GetString("AddColumn", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to DELETE FROM dependencies;.
        /// </summary>
        internal static string DeleteAllDeps {
            get {
                return ResourceManager.GetString("DeleteAllDeps", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to DELETE FROM installed_files WHERE file_name = @file_name;.
        /// </summary>
        internal static string DeleteInstalledFilesWhere {
            get {
                return ResourceManager.GetString("DeleteInstalledFilesWhere", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to DELETE FROM {0} WHERE id = @id;.
        /// </summary>
        internal static string DeletePkg {
            get {
                return ResourceManager.GetString("DeletePkg", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to DELETE FROM {0} WHERE package_id = @id;.
        /// </summary>
        internal static string DeletePkgDeps {
            get {
                return ResourceManager.GetString("DeletePkgDeps", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to DELETE FROM {0} WHERE package_id = @id;.
        /// </summary>
        internal static string DeletePkgFiles {
            get {
                return ResourceManager.GetString("DeletePkgFiles", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to INSERT INTO checksums (folder, chcksum, last_write) VALUES (@folder,@chcksum,@last_write) ON CONFLICT(folder) DO UPDATE SET folder = @folder, chcksum = @chcksum, last_write = @last_write;.
        /// </summary>
        internal static string InsertChckSum {
            get {
                return ResourceManager.GetString("InsertChckSum", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to INSERT INTO dependencies (path, isScenario) VALUES (@path,@isScenario);.
        /// </summary>
        internal static string InsertDeps {
            get {
                return ResourceManager.GetString("InsertDeps", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to INSERT INTO installed_files (package_id, file_name) VALUES (@package_id,@file_name) ON CONFLICT(id) DO UPDATE SET id = id;.
        /// </summary>
        internal static string InsertInstalledFiles {
            get {
                return ResourceManager.GetString("InsertInstalledFiles", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to INSERT INTO {0} (
        ///    id,
        ///    file_name,
        ///    display_name,
        ///    category,
        ///    era,
        ///    country,
        ///    version,
        ///    owner,
        ///    datetime,
        ///    description,
        ///    target_path
        ///) VALUES (
        ///    @id,
        ///    @file_name,
        ///    @display_name,
        ///    @category,
        ///    @era,
        ///    @country,
        ///    @version,
        ///    @owner,
        ///    @datetime,
        ///    @description,
        ///    @target_path
        ///) ON CONFLICT(id) DO UPDATE SET 
        ///file_name = @file_name,
        ///display_name = @display_name,
        ///category = @category,
        ///era = @era,
        ///country = @country,
        ///versi [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string InsertPkg {
            get {
                return ResourceManager.GetString("InsertPkg", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to INSERT INTO {0} (package_id, dependency_package_id) VALUES (@package_id, @dependency_package_id);.
        /// </summary>
        internal static string InsertPkgDeps {
            get {
                return ResourceManager.GetString("InsertPkgDeps", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to INSERT INTO {0} (package_id, file_name) VALUES (@package_id,@file_name);.
        /// </summary>
        internal static string InsertPkgFiles {
            get {
                return ResourceManager.GetString("InsertPkgFiles", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to INSERT INTO {0} (
        ///    id,
        ///    file_name,
        ///    display_name,
        ///    category,
        ///    era,
        ///    country,
        ///    version,
        ///    owner,
        ///    datetime,
        ///    description,
        ///    target_path,
        ///    paid,
        ///    steamappid
        ///) VALUES (
        ///    @id,
        ///    @file_name,
        ///    @display_name,
        ///    @category,
        ///    @era,
        ///    @country,
        ///    @version,
        ///    @owner,
        ///    @datetime,
        ///    @description,
        ///    @target_path,
        ///    @paid,
        ///    @steamappid
        ///) ON CONFLICT(id) DO UPDATE SET 
        ///file_name = @file_name,
        ///display_name = @display_name,
        ///cate [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string InsertRemotePkg {
            get {
                return ResourceManager.GetString("InsertRemotePkg", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to PRAGMA table_info({0});.
        /// </summary>
        internal static string ListTableRows {
            get {
                return ResourceManager.GetString("ListTableRows", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to dependency_list.
        /// </summary>
        internal static string LocalDeps {
            get {
                return ResourceManager.GetString("LocalDeps", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to file_list.
        /// </summary>
        internal static string LocalFiles {
            get {
                return ResourceManager.GetString("LocalFiles", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to package_list.
        /// </summary>
        internal static string LocalPackages {
            get {
                return ResourceManager.GetString("LocalPackages", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to remote_dependency_list.
        /// </summary>
        internal static string RemoteDeps {
            get {
                return ResourceManager.GetString("RemoteDeps", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to remote_file_list.
        /// </summary>
        internal static string RemoteFiles {
            get {
                return ResourceManager.GetString("RemoteFiles", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to remote_package_list.
        /// </summary>
        internal static string RemotePackages {
            get {
                return ResourceManager.GetString("RemotePackages", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to SELECT * FROM checksums;.
        /// </summary>
        internal static string SelectAllChckSums {
            get {
                return ResourceManager.GetString("SelectAllChckSums", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to SELECT * FROM dependencies;.
        /// </summary>
        internal static string SelectAllDeps {
            get {
                return ResourceManager.GetString("SelectAllDeps", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to SELECT * FROM {0};.
        /// </summary>
        internal static string SelectAllPkgs {
            get {
                return ResourceManager.GetString("SelectAllPkgs", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to SELECT * FROM installed_files WHERE package_id = @package_id;.
        /// </summary>
        internal static string SelectInstalledFilesWhere {
            get {
                return ResourceManager.GetString("SelectInstalledFilesWhere", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to SELECT * FROM {0} WHERE package_id = @package_id;.
        /// </summary>
        internal static string SelectPkgDepsWhere {
            get {
                return ResourceManager.GetString("SelectPkgDepsWhere", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to SELECT * FROM {0} WHERE package_id = @package_id;.
        /// </summary>
        internal static string SelectPkgFilesWhere {
            get {
                return ResourceManager.GetString("SelectPkgFilesWhere", resourceCulture);
            }
        }
    }
}
