﻿using Microsoft.Extensions.DependencyInjection;
using SPTarkov.DI;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;
using SPTarkov.Server.Core.Utils.Json.Converters;
using UnitTests.Mock;

namespace UnitTests;

[TestClass]
public class DI
{
    private static IServiceProvider _serviceProvider;

    [AssemblyInitialize]
    public static void ConfigureServices(TestContext context)
    {
        if (_serviceProvider != null)
        {
            return;
        }

        var services = new ServiceCollection();

        var diHandler = new DependencyInjectionHandler(services);

        diHandler.AddInjectableTypesFromTypeAssembly(typeof(App));
        diHandler.AddInjectableTypesFromTypeList(
            [
                typeof(MockLogger<>), /* TODO: this needs to be enabled but the randomizer needs to NOT be random, typeof(MockRandomUtil)*/
            ]
        );

        diHandler.InjectAll();

        services.AddSingleton<IReadOnlyList<SptMod>>(_ => []);

        _serviceProvider = services.BuildServiceProvider();

        foreach (var onLoad in _serviceProvider.GetServices<IOnLoad>())
        {
            onLoad.OnLoad().Wait();
        }
    }

    public static T GetService<T>()
        where T : notnull
    {
        return _serviceProvider.GetRequiredService<T>();
    }
}
