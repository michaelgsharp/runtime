// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Xunit;

namespace Microsoft.Extensions.Http.Logging
{
    public class RedactedLogValueIntegrationTest
    {
        private static class EventIds
        {
            public static readonly EventId RequestHeader = new EventId(102, "RequestHeader");
            public static readonly EventId ResponseHeader = new EventId(103, "ResponseHeader");

            public static readonly EventId RequestPipelineRequestHeader = new EventId(102, "RequestPipelineRequestHeader");
            public static readonly EventId RequestPipelineResponseHeader = new EventId(103, "RequestPipelineResponseHeader");
        }

        [Fact]
        public async Task RedactHeaderValueWithHeaderList_ValueIsRedactedBeforeLogging()
        {
            // Arrange
            var sink = new TestSink();

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging();
            serviceCollection.AddSingleton<ILoggerFactory>(new TestLoggerFactory(sink, enabled: true));

            // Act
            serviceCollection
                .AddHttpClient("test")
                .ConfigurePrimaryHttpMessageHandler(() => new TestMessageHandler())
                .RedactLoggedHeaders(new[] { "Authorization", "X-Sensitive", });

            // Assert
            var services = serviceCollection.BuildServiceProvider();

            var client = services.GetRequiredService<IHttpClientFactory>().CreateClient("test");

            var request = new HttpRequestMessage(HttpMethod.Get, "http://example.com");
            request.Headers.Authorization = new AuthenticationHeaderValue("fake", "secret value");
            request.Headers.CacheControl = new CacheControlHeaderValue() { NoCache = true, };

            await client.SendAsync(request);

            var messages = sink.Writes.ToArray();

            var message = Assert.Single(messages.Where(m =>
            {
                return
                    m.EventId == EventIds.RequestPipelineRequestHeader &&
                    m.LoggerName == "System.Net.Http.HttpClient.test.LogicalHandler";
            }));
            Assert.StartsWith(LineEndingsHelper.Normalize(
@"Request Headers:
Authorization: *
Cache-Control: no-cache
"), message.Message);

            message = Assert.Single(messages.Where(m =>
            {
                return
                    m.EventId == EventIds.RequestHeader &&
                    m.LoggerName == "System.Net.Http.HttpClient.test.ClientHandler";
            }));
            Assert.StartsWith(LineEndingsHelper.Normalize(
@"Request Headers:
Authorization: *
Cache-Control: no-cache
"), message.Message);

            message = Assert.Single(messages.Where(m =>
            {
                return
                    m.EventId == EventIds.ResponseHeader &&
                    m.LoggerName == "System.Net.Http.HttpClient.test.ClientHandler";
            }));
            Assert.StartsWith(LineEndingsHelper.Normalize(
@"Response Headers:
X-Sensitive: *
Y-Non-Sensitive: innocuous value
"), message.Message);

            message = Assert.Single(messages.Where(m =>
            {
                return
                    m.EventId == EventIds.RequestPipelineResponseHeader &&
                    m.LoggerName == "System.Net.Http.HttpClient.test.LogicalHandler";
            }));
            Assert.StartsWith(LineEndingsHelper.Normalize(
@"Response Headers:
X-Sensitive: *
Y-Non-Sensitive: innocuous value
"), message.Message);
        }

        [Fact]
        public async Task RedactHeaderValueWithPredicate_ValueIsRedactedBeforeLogging()
        {
            // Arrange
            var sink = new TestSink();

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging();
            serviceCollection.AddSingleton<ILoggerFactory>(new TestLoggerFactory(sink, enabled: true));

            // Act
            serviceCollection
                .AddHttpClient("test")
                .ConfigurePrimaryHttpMessageHandler(() => new TestMessageHandler())
                .RedactLoggedHeaders(header =>
                {
                    return header.StartsWith("Auth") || header.StartsWith("X-");
                });

            // Assert
            var services = serviceCollection.BuildServiceProvider();

            var client = services.GetRequiredService<IHttpClientFactory>().CreateClient("test");

            var request = new HttpRequestMessage(HttpMethod.Get, "http://example.com");
            request.Headers.Authorization = new AuthenticationHeaderValue("fake", "secret value");
            request.Headers.CacheControl = new CacheControlHeaderValue() { NoCache = true, };

            await client.SendAsync(request);

            var messages = sink.Writes.ToArray();

            var message = Assert.Single(messages.Where(m =>
            {
                return
                    m.EventId == EventIds.RequestPipelineRequestHeader &&
                    m.LoggerName == "System.Net.Http.HttpClient.test.LogicalHandler";
            }));
            Assert.StartsWith(LineEndingsHelper.Normalize(
@"Request Headers:
Authorization: *
Cache-Control: no-cache
"), message.Message);

            message = Assert.Single(messages.Where(m =>
            {
                return
                    m.EventId == EventIds.RequestHeader &&
                    m.LoggerName == "System.Net.Http.HttpClient.test.ClientHandler";
            }));
            Assert.StartsWith(LineEndingsHelper.Normalize(
@"Request Headers:
Authorization: *
Cache-Control: no-cache
"), message.Message);

            message = Assert.Single(messages.Where(m =>
            {
                return
                    m.EventId == EventIds.ResponseHeader &&
                    m.LoggerName == "System.Net.Http.HttpClient.test.ClientHandler";
            }));
            Assert.StartsWith(LineEndingsHelper.Normalize(
@"Response Headers:
X-Sensitive: *
Y-Non-Sensitive: innocuous value
"), message.Message);

            message = Assert.Single(messages.Where(m =>
            {
                return
                    m.EventId == EventIds.RequestPipelineResponseHeader &&
                    m.LoggerName == "System.Net.Http.HttpClient.test.LogicalHandler";
            }));
            Assert.StartsWith(LineEndingsHelper.Normalize(
@"Response Headers:
X-Sensitive: *
Y-Non-Sensitive: innocuous value
"), message.Message);
        }

        private class TestMessageHandler : HttpClientHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var response = new HttpResponseMessage();
                response.Headers.Add("X-Sensitive", "secret value");
                response.Headers.Add("Y-Non-Sensitive", "innocuous value");

                return Task.FromResult(response);
            }
        }
    }
}
