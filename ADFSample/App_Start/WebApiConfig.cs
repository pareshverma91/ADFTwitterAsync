using Autofac;
using System;
using System.Reflection;
using System.Web.Http;
using System.Web.Routing;

namespace ADFSample
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "DefaultAPi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new
                {
                    id = RouteParameter.Optional
                }
            );

            // autofac dependency resolver
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            if (args.Name.StartsWith("Autofac"))
            {
                return typeof(Autofac.ContainerBuilder).Assembly;
            }

            return null;
        }
    }
}
