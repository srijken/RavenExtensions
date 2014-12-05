using System;

namespace RavenDB.OData
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Formatting;
    using System.Net.Http.Headers;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client;

    public class InlineRavenCountHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                                                               CancellationToken cancellationToken)
        {
            if (!ShouldInlineCount(request))
                return base.SendAsync(request, cancellationToken);

            // Otherwise, we have a continuation to work our magic...
            return base.SendAsync(request, cancellationToken).ContinueWith(
                t =>
                {
                    var response = t.Result;

                    // Is this a response we can work with?
                    if (!ResponseIsValid(response)) return response;

                    var pagedResultsValue = GetValueFromObjectContent(response.Content);

                    // Can we find the underlying type of the results?
                    var queryable = pagedResultsValue as IQueryable;
                    if (queryable != null)
                    {
                        Type queriedType = queryable.ElementType;

                        // we need to work with an instance of IRavenQueryable to support statistics
                        var genericQueryableType =
                            typeof (Raven.Client.Linq.IRavenQueryable<>).MakeGenericType(queriedType);

                        if (genericQueryableType.IsInstanceOfType(queryable))
                        {
                            RavenQueryStatistics stats;

                            // register our statistics object with the Raven query provider.
                            // After the query executes, this object will contain the appropriate stats data
                            dynamic dynamicResults = pagedResultsValue;
                            dynamicResults.Statistics(out stats);


                            // Create the return object.
                            var resultsValueMethod =
                                GetType().GetMethod(
                                    "CreateResultValue", BindingFlags.Instance | BindingFlags.NonPublic)
                                    .MakeGenericMethod(
                                        new[] {queriedType});

                            // Create the result value with dynamic type
                            var resultValue = resultsValueMethod.Invoke(
                                this, new[] {stats, pagedResultsValue});

                            // Push the new content and return the response
                            response.Content = CreateObjectContent(
                                resultValue, ((ObjectContent) response.Content).Formatter,
                                response.Content.Headers.ContentType);
                            return response;

                        }
                        return response;
                    }
                    return response;
                });
        }

        private bool ResponseIsValid(HttpResponseMessage response)
        {
            // Only do work if the response is OK
            if (response == null || response.StatusCode != HttpStatusCode.OK) return false;

            // Only do work if we are an ObjectContent
            return response.Content is ObjectContent;
        }

        private bool ShouldInlineCount(HttpRequestMessage request)
        {
            var queryParams = request.RequestUri.ParseQueryString();

            var inlinecount = queryParams["$inlinecount"];
            return String.Compare(inlinecount, "allpages", StringComparison.OrdinalIgnoreCase) == 0;
        }

        // Dynamically invoked for the T returned by the resulting ApiController
        private ResultValue<T> CreateResultValue<T>(RavenQueryStatistics stats, IQueryable<T> pagedResults)
        {
            var genericType = typeof (ResultValue<>);
            var constructedType = genericType.MakeGenericType(new[] {typeof (T)});

            var ctor = constructedType
                .GetConstructors().First();

            var instance = ctor.Invoke(null);

            var resultsProperty = constructedType.GetProperty("Results");
            resultsProperty.SetValue(instance, pagedResults.ToArray(), null);

            var countProperty = constructedType.GetProperty("Count");
            countProperty.SetValue(instance, stats.TotalResults, null);

            return instance as ResultValue<T>;
        }

        private object GetValueFromObjectContent(HttpContent content)
        {
            var objContent = content as ObjectContent;
            if (objContent == null) return null;

            return objContent.Value;
        }

        private ObjectContent CreateObjectContent(object value, MediaTypeFormatter formatter, MediaTypeHeaderValue mthv)
        {
            if (value == null) return null;

            return new ObjectContent(value.GetType(), value, formatter, mthv);
        }
    }
}

