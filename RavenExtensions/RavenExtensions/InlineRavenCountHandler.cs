using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            var queryParams = request.RequestUri.ParseQueryString();

            if (!ShouldInlineCount(request))
                return base.SendAsync(request, cancellationToken);

            // Otherwise, we have a continuation to work our magic...
            return base.SendAsync(request, cancellationToken).ContinueWith(
                t =>
                {
                    var response = t.Result;

                    // Is this a response we can work with?
                    if (!ResponseIsValid(response)) return response;

                    var pagedResultsValue = this.GetValueFromObjectContent(response.Content);
                    Type queriedType;

                    // Can we find the underlying type of the results?
                    if (pagedResultsValue is IQueryable)
                    {
                        queriedType = ((IQueryable) pagedResultsValue).ElementType;

                        // we need to work with an instance of IRavenQueryable to support statistics
                        var genericQueryableType =
                            typeof (Raven.Client.Linq.IRavenQueryable<>).MakeGenericType(queriedType);

                        if (genericQueryableType.IsInstanceOfType(pagedResultsValue))
                        {
                            RavenQueryStatistics stats = null;

                            // register our statistics object with the Raven query provider.
                            // After the query executes, this object will contain the appropriate stats data
                            dynamic dynamicResults = pagedResultsValue;
                            dynamicResults.Statistics(out stats);


                            // Create the return object.
                            var resultsValueMethod =
                                this.GetType().GetMethod(
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
                        else
                            return response;
                    }
                    else
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
            return string.Compare(inlinecount, "allpages", true) == 0;
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

        // We need this because ObjectContent's Value property is internal
        private object GetValueFromObjectContent(HttpContent content)
        {
            var objContent = content as ObjectContent;
            if (objContent == null) return null;

            return objContent.Value;

            var valueProperty = typeof (ObjectContent).GetProperty("Value",
                                                                   BindingFlags.Instance | BindingFlags.NonPublic);
            if (valueProperty == null) return null;

            return valueProperty.GetValue(content, null);
        }

        // We need this because ObjectContent's constructors are internal
        private ObjectContent CreateObjectContent(object value, MediaTypeFormatter formatter, MediaTypeHeaderValue mthv)
        {
            if (value == null) return null;

            return new ObjectContent(value.GetType(), value, formatter, mthv);

            var ctor = typeof (ObjectContent).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
                                             .FirstOrDefault(
                                                 ci =>
                                                 {
                                                     var parameters = ci.GetParameters();
                                                     if (parameters.Length != 3) return false;
                                                     if (parameters[0].ParameterType != typeof (Type)) return false;
                                                     if (parameters[1].ParameterType != typeof (object)) return false;
                                                     if (parameters[2].ParameterType != typeof (MediaTypeHeaderValue))
                                                         return false;
                                                     return true;
                                                 });

            if (ctor == null) return null;

            return ctor.Invoke(new[] {value.GetType(), value, mthv}) as ObjectContent;
        }
    }
}

