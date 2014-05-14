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


Enabling OData on a Web API call using a RavenDB store
------------------------------------------------------

    [Queryable(HandleNullPropagation = HandleNullPropagationOption.False, AllowedQueryOptions = AllowedQueryOptions.All, EnableConstantParameterization = false, MaxTop = 1024)]
    public Raven.Client.Linq.IRavenQueryable<Company> GetCompanies()
    {
        return _session.Query<Company>();
    }

Calling the API
---------------
See the [OData uri conventions][2] or [breeze.js][1] on how to call the API.

[1]: http://www.breezejs.com
[2]: http://www.odata.org/documentation/odata-version-2-0/uri-conventions/