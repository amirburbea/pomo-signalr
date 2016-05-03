using System.Windows;
using System.Windows.Threading;
using Castle.MicroKernel.Registration;
using Castle.MicroKernel.SubSystems.Configuration;
using Castle.Windsor;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PoMo.Client.Properties;
using PoMo.Common.Json;

namespace PoMo.Client
{
    public sealed class WindsorInstaller : IWindsorInstaller
    {
        void IWindsorInstaller.Install(IWindsorContainer container, IConfigurationStore store)
        {
            container
                .Register(Component.For<IContractResolver>().ImplementedBy<JsonContractResolver>())
                .Register(Component.For<JsonSerializer>().UsingFactory((IContractResolver contractResolver) => new JsonSerializer { ContractResolver = contractResolver }))
                .Register(Component.For<IConnectionManager>().ImplementedBy<ConnectionManager>())
                .Register(Component.For<Application>().ImplementedBy<App>().OnCreate(app => ((App)app).InitializeComponent()))
                .Register(Component.For<IWebSettings>().Instance(Settings.Default))
                .Register(Component.For<Dispatcher>().UsingFactory((Application app) => app.Dispatcher));
        }
    }
}