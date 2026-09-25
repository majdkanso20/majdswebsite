using MajdsApp.Tests.Support;
using Xunit;

namespace MajdsApp.Tests.Plugins;

/// <summary>
/// One API host for every test that needs the sample plugin running. EF Core builds its model once per process and a plugin's
/// entity must be in it exactly once, so two hosts that each load the sample plugin in one process clash. Everything that runs
/// a host with the plugin therefore shares this one, and tests that change its state put it back.
/// </summary>
public class PluginHostFactory : ApiFactory
{
    public PluginHostFactory() : base(null, folder => PluginPackages.InstallFolder(folder)) { }
}

[CollectionDefinition(Name)]
public class PluginHostCollection : ICollectionFixture<PluginHostFactory>
{
    public const string Name = "plugin-host";
}
