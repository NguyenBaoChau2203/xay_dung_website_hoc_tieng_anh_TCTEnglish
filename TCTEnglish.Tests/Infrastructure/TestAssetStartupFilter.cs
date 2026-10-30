using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace TCTEnglish.Tests.Infrastructure;

/// <summary>Assets are immutable during a test host's lifetime; do not allocate OS watchers.</summary>
public sealed class TestAssetStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        var environment = app.ApplicationServices.GetService(typeof(IWebHostEnvironment)) as IWebHostEnvironment;
        if (environment is not null)
        {
            environment.WebRootFileProvider = new ImmutableFileProvider(environment.WebRootFileProvider);
            environment.ContentRootFileProvider = new ImmutableFileProvider(environment.ContentRootFileProvider);
        }
        next(app);
    };

    private sealed class ImmutableFileProvider(IFileProvider inner) : IFileProvider
    {
        public IFileInfo GetFileInfo(string subpath) => inner.GetFileInfo(subpath);
        public IDirectoryContents GetDirectoryContents(string subpath) => inner.GetDirectoryContents(subpath);
        public IChangeToken Watch(string filter) => NullChangeToken.Singleton;
    }
}
