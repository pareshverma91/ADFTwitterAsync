using System.Web.Http;
using System.Web.Routing;

namespace ADFSample
{
    public static class WebApiConfig
    {
        public static void Register (HttpConfiguration config)
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
        }
    }
}