using System;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Routing;

namespace ADFSample
{
    public class Global : System.Web.HttpApplication
    {
        public void Application_Start(object sender, EventArgs e)
        {
            AreaRegistration.RegisterAllAreas();
            WebApiConfig.Register(GlobalConfiguration.Configuration);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
        }
    }
}
