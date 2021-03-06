﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Xunit.Abstractions;
using Xunit.Fixture.Mvc.Infrastructure;

namespace Xunit.Fixture.Mvc
{
    /// <summary>
    /// A functional test fixture for MVC
    /// </summary>
    /// <typeparam name="TStartup">The type of the startup.</typeparam>
    /// <seealso cref="IMvcFunctionalTestFixture" />
    public class MvcFunctionalTestFixture<TStartup> : IMvcFunctionalTestFixture
        where TStartup : class
    {
        private readonly ITestOutputHelper _output;
        private readonly IServiceCollection _extraServices = new ServiceCollection();
        private readonly IList<Action<ITestOutputHelper, IConfigurationBuilder>> _configurationBuilderDelegates = new List<Action<ITestOutputHelper, IConfigurationBuilder>>();
        private readonly IList<Action<WebApplicationFactoryClientOptions>> _clientConfigurationDelegates = new List<Action<WebApplicationFactoryClientOptions>>();
        private readonly IList<Action<HttpResponseMessage>> _responseAssertions = new List<Action<HttpResponseMessage>>();
        private readonly IDictionary<Type, IList<Action<object>>> _resultAssertions = new Dictionary<Type, IList<Action<object>>>();
        private readonly IList<(Type serviceType, Func<object, Task> assertion)> _postRequestAssertions = new List<(Type serviceType, Func<object, Task> assertion)>();
        private readonly HttpRequestMessage _message = new HttpRequestMessage();

        private bool _actStepConfigured;
        private LogLevel _minimumLogLevel = LogLevel.Debug;
        private string _environment;

        /// <summary>
        /// Initializes a new instance of the <see cref="MvcFunctionalTestFixture{TStartup}"/> class.
        /// </summary>
        /// <param name="output">The test output helper.</param>
        public MvcFunctionalTestFixture(ITestOutputHelper output)
        {
            _output = output;
            HavingServices(services => services.AddSingleton(new MvcFunctionalTestFixtureHttpRequestMessage(_message)));
        }

        /// <summary>
        /// Gets the auto fixture.
        /// </summary>
        /// <value>
        /// The auto fixture.
        /// </value>
        public IFixture AutoFixture { get; } = new AutoFixture.Fixture();

        /// <summary>
        /// Gets the properties.
        /// </summary>
        public IDictionary<string, string> Properties { get; } = new Dictionary<string, string>();

        /// <summary>
        /// Configures the host test server to use the specified environment.
        /// </summary>
        /// <param name="environment">The environment.</param>
        /// <returns></returns>
        public IMvcFunctionalTestFixture HavingAspNetEnvironment(string environment) =>
            FluentSetup(() => _environment = environment);

        /// <summary>
        /// Configures the host test server configuration.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <returns></returns>
        public IMvcFunctionalTestFixture HavingConfiguration(Action<ITestOutputHelper, IConfigurationBuilder> action) =>
            FluentSetup(() => _configurationBuilderDelegates.Add(action));

        /// <summary>
        /// Configures an instance of the specified bootstrap service to be:
        /// 1. Added to the test server DI container.
        /// 2. Resolved and run once the test server is constructed.
        /// </summary>
        /// <typeparam name="TTestDataBootstrapService">The type of the test data bootstrap.</typeparam>
        /// <returns></returns>
        public IMvcFunctionalTestFixture HavingBootstrap<TTestDataBootstrapService>()
            where TTestDataBootstrapService : class, ITestServerBootstrap =>
            HavingServices(services => services.AddScoped<ITestServerBootstrap, TTestDataBootstrapService>());

        /// <summary>
        /// Configures the specified bootstrap function to be:
        /// 1. Added to the test server DI container.
        /// 2. Resolved and run once the test server is constructed.
        /// </summary>
        /// <param name="bootstrapAction">The action to perform on the service provider during bootstrap.</param>
        /// <returns></returns>
        public IMvcFunctionalTestFixture HavingBootstrap(Func<IServiceProvider, Task> bootstrapAction) =>
            HavingServices(services => services.AddScoped<ITestServerBootstrap>(provider => new SimpleTestServerBootstrap(provider, bootstrapAction)));

        /// <summary>
        /// Configures test server DI container services.
        /// </summary>
        /// <param name="servicesDelegate">The services delegate.</param>
        /// <returns></returns>
        public IMvcFunctionalTestFixture HavingServices(Action<IServiceCollection> servicesDelegate) =>
            FluentSetup(() => servicesDelegate(_extraServices));

        /// <summary>
        /// Adds the specified configurator for the test server client.
        /// </summary>
        /// <param name="configurator">The configurator.</param>
        /// <returns></returns>
        public IMvcFunctionalTestFixture HavingClientConfiguration(Action<WebApplicationFactoryClientOptions> configurator) =>
            FluentSetup(() => _clientConfigurationDelegates.Add(configurator));

        /// <summary>
        /// Configures the HTTP request message.
        /// </summary>
        /// <param name="configurator">The configurator.</param>
        /// <returns></returns>
        public IMvcFunctionalTestFixture HavingConfiguredHttpMessage(Action<HttpRequestMessage> configurator)
        {
            configurator(_message);
            return this;
        }

        /// <summary>
        /// Configures the fixture perform the specified HTTP action.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="uri">The URI.</param>
        /// <param name="content">The HTTP content.</param>
        public IMvcFunctionalTestFixture When(HttpMethod method, string uri, HttpContent content)
        {
            _actStepConfigured = true;
            return HavingConfiguredHttpMessage(message =>
                                               {
                                                   message.Method = method;
                                                   message.RequestUri = new Uri(uri, UriKind.Relative);
                                                   message.Content = content;
                                               });
        }

        /// <summary>
        /// Adds assertions that will be run on the HTTP response.
        /// </summary>
        /// <param name="assertions">The assertions.</param>
        public IMvcFunctionalTestFixture ResponseShould(params Action<HttpResponseMessage>[] assertions)
        {
            foreach (var assertion in assertions)
            {
                _responseAssertions.Add(assertion);
            }

            return this;
        }

        /// <summary>
        /// Adds assertions that will be run on the HTTP, JSON response body.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="assertions">The assertions.</param>
        /// <exception cref="ArgumentException">TResult</exception>
        public IMvcFunctionalTestFixture ShouldReturnJson<TResult>(params Action<TResult>[] assertions)
        {
            if (!_resultAssertions.TryGetValue(typeof(TResult), out var list))
            {
                list = _resultAssertions[typeof(TResult)] = new List<Action<object>>();
            }

            foreach (var assertion in assertions)
            {
                list.Add(o => assertion(o.Should().BeAssignableTo<TResult>().Which));
            }

            return this;
        }

        /// <summary>
        /// Adds an assertion that will be run after the request has completed, resolving a service from DI.
        /// </summary>
        /// <typeparam name="TService">The type of the service.</typeparam>
        /// <param name="assertion">The assertion.</param>
        /// <returns></returns>
        public IMvcFunctionalTestFixture PostRequestResolvedServiceShould<TService>(Func<TService, Task> assertion)
            where TService : class => FluentSetup(() => _postRequestAssertions.Add((typeof(TService), o => assertion((TService)o))));

        /// <summary>
        /// Sets the log minimum level.
        /// </summary>
        /// <param name="logLevel">The log level.</param>
        /// <returns></returns>
        public IMvcFunctionalTestFixture HavingMinimumLogLevel(LogLevel logLevel) => FluentSetup(() => _minimumLogLevel = logLevel);

        /// <summary>
        /// Runs this fixture.
        /// </summary>
        /// <returns></returns>
        public async Task RunAsync()
        {
            if (!_actStepConfigured)
            {
                throw new InvalidOperationException($"Must call {nameof(When)} to configure an HTTP request");
            }

            using (var loggerProvider = _output == null ? NullLoggerProvider.Instance as ILoggerProvider : new TestOutputHelperLoggerProvider(_output))
            using (var factory = new FixtureWebApplicationFactory(_output, loggerProvider, _environment, _extraServices, _configurationBuilderDelegates, _clientConfigurationDelegates, _minimumLogLevel))
            using (var client = factory.CreateClient()) // this actually builds the test server.
            {
                var logger = loggerProvider.CreateLogger(GetType().ToString());

                // Bootstrap.
                using (var scope = factory.Server.Host.Services.CreateScope())
                {
                    foreach (var bootstrap in scope.ServiceProvider.GetService<IEnumerable<ITestServerBootstrap>>())
                    {
                        logger.LogInformation($"Bootstrapping {bootstrap.GetType()}");
                        await bootstrap.BootstrapAsync();
                    }
                }

                using (var aggregator = new ExceptionAggregator())
                {
                    logger.LogInformation($"Sending request {_message}");
                    var response = await client.SendAsync(_message);

                    // Response assertions.
                    foreach (var assertion in _responseAssertions)
                    {
                        aggregator.Try(() => assertion(response));
                    }

                    // Response body (result) assertions.
                    var responseBody = await response.Content.ReadAsStringAsync();
                    logger.LogInformation("Received: " + responseBody);

                    foreach (var kvp in _resultAssertions)
                    {
                        try
                        {
                            var result = JsonConvert.DeserializeObject(responseBody, kvp.Key);
                            foreach (var assertion in kvp.Value)
                            {
                                aggregator.Try(() => assertion(result));
                            }
                        }
                        catch (JsonException e)
                        {
                            aggregator.Add(e);
                        }
                    }

                    // Post request assertions.
                    if (_postRequestAssertions.Any())
                    {
                        using (var scope = factory.Server.Host.Services.CreateScope())
                        {
                            foreach (var (serviceType, assertion) in _postRequestAssertions)
                            {
                                logger.LogInformation($"Running post request assertion on service: {serviceType}");
                                var service = scope.ServiceProvider.GetRequiredService(serviceType);
                                await aggregator.TryAsync(() => assertion(service));
                            }
                        }
                    }
                }
            }
        }

        private IMvcFunctionalTestFixture FluentSetup(Action action)
        {
            action();
            return this;
        }

        private class FixtureWebApplicationFactory : WebApplicationFactory<TStartup>
        {
            private readonly ITestOutputHelper _output;
            private readonly ILoggerProvider _loggerProvider;
            private readonly string _environment;
            private readonly IServiceCollection _extraServices;
            private readonly IEnumerable<Action<ITestOutputHelper, IConfigurationBuilder>> _configurationBuilderDelegates;
            private readonly LogLevel _minimumLogLevel;

            public FixtureWebApplicationFactory(ITestOutputHelper output,
                                                ILoggerProvider loggerProvider,
                                                string environment,
                                                IServiceCollection extraServices,
                                                IEnumerable<Action<ITestOutputHelper, IConfigurationBuilder>> configurationBuilderDelegates,
                                                IEnumerable<Action<WebApplicationFactoryClientOptions>> clientConfigurationDelegates,
                                                LogLevel minimumLogLevel)
            {
                _output = output;
                _loggerProvider = loggerProvider;
                _environment = environment;
                _extraServices = extraServices;
                _configurationBuilderDelegates = configurationBuilderDelegates;
                _minimumLogLevel = minimumLogLevel;

                foreach (var configurator in clientConfigurationDelegates)
                {
                    configurator(ClientOptions);
                }
            }

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration(configurationBuilder =>
                                                  {
                                                      foreach (var action in _configurationBuilderDelegates)
                                                      {
                                                          action(_output, configurationBuilder);
                                                      }
                                                  })
                       .UseEnvironment(_environment ?? EnvironmentName.Production)
                       .ConfigureLogging(b => b.SetMinimumLevel(_minimumLogLevel).AddProvider(_loggerProvider))
                       .ConfigureServices(services =>
                                          {
                                              foreach (var service in _extraServices)
                                              {
                                                  services.Add(service);
                                              }
                                          });
            }
        }
    }
}