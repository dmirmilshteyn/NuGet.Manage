using NuGet.Protocol.Core.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using NuGet.Configuration;
using NuGet.Frameworks;
using System.Threading;
using NuGet.Protocol;

namespace NuGet.Manage
{
    public class NuGetHelper
    {
        public class PackageInformation
        {
            public SourcePackageDependencyInfo SourcePackageDependencyInfo { get; }
            public SourceRepository SourceRepository { get; }

            public PackageInformation(SourcePackageDependencyInfo sourcePackageDependencyInfo, SourceRepository sourceRepository) {
                SourcePackageDependencyInfo = sourcePackageDependencyInfo;
                SourceRepository = sourceRepository;
            }
        }

        public static void PrepareSourceRepositoriesFrom(List<SourceRepository> sourceRepositories, string configFile) {
            PackageSourceProvider p = new PackageSourceProvider(new Settings(Path.GetDirectoryName(configFile), Path.GetFileName(configFile), true));
            var sources = p.LoadPackageSources();

            foreach (var source in sources) {
                sourceRepositories.Add(new SourceRepository(source, Repository.Provider.GetCoreV3()));
            }
        }

        public static async Task<PackageInformation> DetermineNewestPackage(List<SourceRepository> sourceRepositories, string packageId) {
            SourcePackageDependencyInfo latestPackage = null;
            SourceRepository selectedSourceRepository = null;

            foreach (var sourceRepository in sourceRepositories) {
                if (sourceRepository.PackageSource?.Credentials?.Password != null) {
                    await sourceRepository.GetResourceAsync<HttpHandlerResource>();
                }
                var resource = await sourceRepository.GetResourceAsync<DependencyInfoResource>();
                if (resource != null) {
                    var results = await resource.ResolvePackages(packageId, NuGetFramework.AnyFramework, new Common.NullLogger(), CancellationToken.None);

                    if (results.Count() > 0) {
                        foreach (var result in results) {
                            if (latestPackage == null) {
                                latestPackage = result;
                                selectedSourceRepository = sourceRepository;
                            } else {
                                if (result.Version.CompareTo(latestPackage.Version) > 0) {
                                    latestPackage = result;
                                    selectedSourceRepository = sourceRepository;
                                }
                            }
                        }
                        break;
                    }
                }
            }

            if (latestPackage == null && selectedSourceRepository == null) {
                return null;
            } else {
                return new PackageInformation(latestPackage, selectedSourceRepository);
            }
        }
    }
}
