// MIT License Copyright (c) David Melendez. All rights reserved. See License in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class DocFxBuildExtension
    {
        private static bool IsReloading = false;

        private static readonly FileSystemWatcher watcher = new FileSystemWatcher();

        public static string GetDocFxExecutablePath(string contentRootPath)
        {
            // Read the projects.assets.json for nuget path and docFx library version
            string assetsJsonPath = Path.Combine(contentRootPath, "obj\\project.assets.json");
            string nugetPackPath = null;
            string docFxLibPath = null;
            using (StreamReader sr = new StreamReader(new FileStream(assetsJsonPath, FileMode.Open, FileAccess.Read)))
            {
                using (JsonReader reader = new JsonTextReader(sr))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    JObject assetsObj = (JObject)serializer.Deserialize(reader);
                    //Get the default nuget package path
                    nugetPackPath = assetsObj?.Properties()
                        .Where(pp => pp.Name == "packageFolders")
                        .Descendants()
                        .Cast<JObject>()
                        .FirstOrDefault()?
                        .Properties()
                        .FirstOrDefault()?
                        .Name;
                    if (string.IsNullOrWhiteSpace(nugetPackPath)
                        || !Directory.Exists(nugetPackPath))
                    {
                        throw new DirectoryNotFoundException("packageFolders node not found in manifest or invalid nuget package path. Unable to locate default nuget package path");
                    }


                    //Get DocFx.console package and version path
                    var libPath = assetsObj?
                        .Properties()
                        .Where(pp => pp.Name == "libraries")
                        .Descendants()
                        .Cast<JObject>()
                        .Properties()
                        .Where(wp => wp.Name.StartsWith("docfx.console"))
                        .FirstOrDefault()?
                        .Name ?? string.Empty;
                    docFxLibPath = Path.Combine(nugetPackPath, libPath);
                    if (string.IsNullOrWhiteSpace(libPath)
                        || !Directory.Exists(docFxLibPath))
                    {
                        throw new DirectoryNotFoundException("Unable to locate docfx.console package directory. Make sure docfx.console package in installed with nuget package manager.");
                    }

                    string fullDocFxExePath = Path.Combine(docFxLibPath, "tools\\docfx.exe");
                    if (File.Exists(fullDocFxExePath))
                    {
                        return fullDocFxExePath;
                    }
                    else
                    {
                        throw new FileNotFoundException("Unable to locate default docfx.exe.");
                    }
                }
            }

        }

        public static IApplicationBuilder UseDocFxBuildRefresh(this IApplicationBuilder app, string contentRootPath, string webRootPath, string docFxJsonFileName = "docfx.json")
        {

            var logger = GetOrCreateLogger(app, nameof(DocFxBuildExtension));

            string docExePath = GetDocFxExecutablePath(contentRootPath);

            watcher.Path = contentRootPath;

            // Watch for changes in LastAccess and LastWrite times, and
            // the renaming of files or directories.
            watcher.NotifyFilter = NotifyFilters.CreationTime
                                | NotifyFilters.LastWrite
                                | NotifyFilters.FileName
                                | NotifyFilters.DirectoryName;

            watcher.IncludeSubdirectories = true;

            var OnChanged = new FileSystemEventHandler((o, args) => {
                string lowerPath = args.FullPath.ToLower();
                if (!IsReloading && !lowerPath.StartsWith(webRootPath.ToLower()))
                {
                    IsReloading = true;
                    using (Process process = new Process())
                    {
                        process.StartInfo = new ProcessStartInfo(docExePath,
                            Path.Combine(contentRootPath, docFxJsonFileName));
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.RedirectStandardOutput = true;
                        process.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
                        {
                            logger.LogInformation(e.Data);
                        });

                        process.ErrorDataReceived += new DataReceivedEventHandler((sender, e) =>
                        {
                            logger.LogError(e.Data);
                        });
                        process.Start();

                        process.BeginOutputReadLine();                            
                             
                        process.WaitForExit();
                    }
                    IsReloading = false;
                }
            });

               

            // Add event handlers.
            watcher.Changed += OnChanged;
            watcher.Created += OnChanged;
            watcher.Deleted += OnChanged;


            // Begin watching.
            watcher.EnableRaisingEvents = true;

               
           
            return app;
        }

        private static ILogger GetOrCreateLogger(
            IApplicationBuilder appBuilder,
            string logCategoryName)
        {
            // If the DI system gives us a logger, use it. Otherwise, set up a default one
            var loggerFactory = appBuilder.ApplicationServices.GetService<ILoggerFactory>();
            var logger = loggerFactory != null
                ? loggerFactory.CreateLogger(logCategoryName)
                : NullLogger.Instance;
            return logger;
        }
    }
}
