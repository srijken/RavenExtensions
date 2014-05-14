RavenExtensions
===============

Installing in OWIN
------------------
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            var configuration = new HttpConfiguration();

            // configure web api here

            // This is required to enable OData
            configuration.EnableQuerySupport();

            // This enables the $inlinecount=allpages option
            configuration.MessageHandlers.Add(new InlineRavenCountHandler());
        }
    }

Installing using App_Start\WebApiConfig
---------------------------------------

    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // This is required to enable OData
            config.EnableQuerySupport();

            // This enables the $inlinecount=allpages option
            config.MessageHandlers.Add(new InlineRavenCountHandler());
        }
    }