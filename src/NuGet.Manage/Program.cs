using Microsoft.Framework.Runtime.Common.CommandLine;
using NuGet.Protocol.Core.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace NuGet.Manage
{
    public class Program
    {
        public static int Main(string[] args) {
            var app = new CommandLineApplication();

            app.Command("install", c =>
            {
                var configFileOption = c.Option("-c|--configfile", "Configuration file to use", CommandOptionType.SingleValue);
                var packageArgument = c.Argument("package", "Package to install");

                c.OnExecute(async () =>
                {
                    if (!configFileOption.HasValue()) {
                        Console.Error.WriteLine("No configuration file specified. Unable to continue.");
                        return 1;
                    }

                    if (string.IsNullOrEmpty(packageArgument.Value)) {
                        Console.Error.WriteLine("No package specified. Unable to continue.");
                        return 1;
                    }

                    var sourceRepositories = new List<SourceRepository>();
                    NuGetHelper.PrepareSourceRepositoriesFrom(sourceRepositories, configFileOption.Value());

                    var package = await NuGetHelper.DetermineNewestPackage(sourceRepositories, packageArgument.Value);

                    HttpClientHandler handler;
                    if (package.SourceRepository.PackageSource.Credentials != null) {
                        handler = new HttpClientHandler()
                        {
                            Credentials = new NetworkCredential(package.SourceRepository.PackageSource.Credentials.Username, package.SourceRepository.PackageSource.Credentials.Password)
                        };
                    } else {
                        handler = new HttpClientHandler();
                    }

                    using (handler) {
                        using (var httpClient = new HttpClient(handler)) {
                            using (var stream = await httpClient.GetStreamAsync(package.SourcePackageDependencyInfo.DownloadUri)) {
                                using (var zipArchive = new ZipArchive(stream)) {
                                    var targetDirectory = Path.Combine(Directory.GetCurrentDirectory(), $"{package.SourcePackageDependencyInfo.Id}.{package.SourcePackageDependencyInfo.Version.ToFullString()}");

                                    zipArchive.ExtractToDirectory(targetDirectory);
                                }
                            }
                        }
                    }

                    return 0;
                });
            });

            return app.Execute(args);
        }
    }
}
