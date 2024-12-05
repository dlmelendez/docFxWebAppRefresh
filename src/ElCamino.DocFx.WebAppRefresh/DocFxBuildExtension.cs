// MIT License Copyright (c) David Melendez. All rights reserved. See License in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    public static class DocFxBuildExtension
    {
        private static bool IsReloading = false;

        private static readonly FileSystemWatcher watcher = new();

        public static IApplicationBuilder UseDocFxBuildRefresh(this IApplicationBuilder app, 
            string contentRootPath, 
            string webRootPath, 
            string docFxJsonFileName = "docfx.json",
            IEnumerable<string> excludeFoldersUnderWebRoot = null)
        {

            var logger = GetOrCreateLogger(app, nameof(DocFxBuildExtension));

            watcher.Path = contentRootPath;

            // Watch for changes in LastAccess and LastWrite times, and
            // the renaming of files or directories.
            watcher.NotifyFilter = NotifyFilters.CreationTime
                                | NotifyFilters.LastWrite
                                | NotifyFilters.FileName
                                | NotifyFilters.DirectoryName;

            watcher.IncludeSubdirectories = true;

            var OnChanged = new FileSystemEventHandler((o, args) => {
                if (excludeFoldersUnderWebRoot != null)
                {
                    foreach (var excludeFolder in excludeFoldersUnderWebRoot)
                    {
                        if (args.FullPath.StartsWith(Path.Combine(webRootPath, excludeFolder), StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }
                    }
                }
                if (!IsReloading && !args.FullPath.StartsWith(webRootPath, StringComparison.OrdinalIgnoreCase))
                {
                    IsReloading = true;
                    using (Process process = new())
                    {
                        process.StartInfo = new("dotnet", $" docfx {docFxJsonFileName}")
                        {
                            WorkingDirectory = contentRootPath,
                            UseShellExecute = false,
                            RedirectStandardOutput = true
                        };
                        process.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
                        {
                            logger.LogInformation(message: e.Data);
                        });

                        process.ErrorDataReceived += new DataReceivedEventHandler((sender, e) =>
                        {
                            logger.LogError(message: e.Data);
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
