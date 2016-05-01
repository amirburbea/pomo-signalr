using System;
using Castle.Core;
using Castle.MicroKernel;
using Castle.MicroKernel.Context;
using Castle.MicroKernel.Registration;
using Castle.MicroKernel.SubSystems.Configuration;
using Castle.Windsor;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PoMo.Common.Json;
using PoMo.Server.Properties;

namespace PoMo.Server.Web
{
    public sealed class WindsorInstaller : IWindsorInstaller
    {
        void IWindsorInstaller.Install(IWindsorContainer container, IConfigurationStore store)
        {
            container
                .Register(Component.For<IContractResolver>().ImplementedBy<JsonContractResolver>())
                .Register(Component.For<JsonSerializer>().UsingFactory((IContractResolver contractResolver) => new JsonSerializer { ContractResolver = contractResolver }))
                .Register(Component.For<IWebManager>().ImplementedBy<WebManager>())
                .Register(Component.For<IWebSettings>().Instance(Settings.Default))
                .Register(Classes.FromThisAssembly().BasedOn<IOwinStartup>())
                .Kernel.Resolver.AddSubResolver(new OwinStartupDependencyResolver(container.Kernel));
        }

        private sealed class OwinStartupDependencyResolver : ISubDependencyResolver
        {
            private readonly IKernel _kernel;

            public OwinStartupDependencyResolver(IKernel kernel)
            {
                this._kernel = kernel;
            }

            bool ISubDependencyResolver.CanResolve(CreationContext context, ISubDependencyResolver contextHandlerResolver, ComponentModel model, DependencyModel dependency)
            {
                return dependency.TargetType == typeof(IOwinStartup[]);
            }

            object ISubDependencyResolver.Resolve(CreationContext context, ISubDependencyResolver contextHandlerResolver, ComponentModel model, DependencyModel dependency)
            {
                IHandler[] handlers = this._kernel.GetAssignableHandlers(typeof(IOwinStartup));
                return Array.ConvertAll(handlers, handler => (IOwinStartup)this._kernel.Resolve(handler.ComponentModel.Implementation));
            }
        }
    }
}