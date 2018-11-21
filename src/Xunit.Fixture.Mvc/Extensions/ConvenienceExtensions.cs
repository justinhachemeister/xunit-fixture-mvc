﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using AutoFixture;
using AutoFixture.Dsl;
using AutoFixture.Kernel;
using Bogus;

namespace Xunit.Fixture.Mvc.Extensions
{
    /// <summary>
    /// Extensions for <see cref="IMvcFunctionalTestFixture"/>.
    /// </summary>
    public static class ConvenienceExtensions
    {
        private static readonly Faker Faker = new Faker();

        /// <summary>
        /// Adds the specified property value to the specified key.
        /// </summary>
        /// <param name="fixture">The fixture.</param>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns></returns>
        public static IMvcFunctionalTestFixture HavingProperty(this IMvcFunctionalTestFixture fixture, string key, string value)
        {
            fixture.Properties[key] = value;
            return fixture;
        }

        /// <summary>
        /// Adds the property value generated by the specified factory to the specified key.
        /// </summary>
        /// <param name="fixture">The fixture.</param>
        /// <param name="key">The key.</param>
        /// <param name="valueFactory">The value factory.</param>
        /// <returns></returns>
        public static IMvcFunctionalTestFixture HavingProperty(this IMvcFunctionalTestFixture fixture, string key, Func<string> valueFactory) =>
            fixture.HavingProperty(key, valueFactory());

        /// <summary>
        /// Runs the specified fixture action if a property exists in the fixture that matches the specified value predicate.
        /// </summary>
        /// <param name="fixture">The fixture.</param>
        /// <param name="key">The key.</param>
        /// <param name="valuePredicate">The value predicate.</param>
        /// <param name="action">The action.</param>
        /// <returns></returns>
        public static IMvcFunctionalTestFixture WhenHavingProperty(this IMvcFunctionalTestFixture fixture,
                                                                   string key,
                                                                   Func<string, bool> valuePredicate,
                                                                   Action<IMvcFunctionalTestFixture> action)
        {
            if (fixture.Properties.TryGetValue(key, out var value) && valuePredicate(value))
            {
                action(fixture);
            }

            return fixture;
        }

        /// <summary>
        /// Runs the specified fixture action if a property exists in the fixture that matches the specified value.
        /// </summary>
        /// <param name="fixture">The fixture.</param>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="action">The action.</param>
        /// <returns></returns>
        public static IMvcFunctionalTestFixture WhenHavingProperty(this IMvcFunctionalTestFixture fixture,
                                                                   string key,
                                                                   string value,
                                                                   Action<IMvcFunctionalTestFixture> action) =>
            fixture.WhenHavingProperty(key, x => x == value, action);

        /// <summary>
        /// Picks a random model from the specified collection.
        /// </summary>
        /// <typeparam name="TModel">The type of the model.</typeparam>
        /// <param name="models">The models.</param>
        /// <returns></returns>
        public static TModel PickRandom<TModel>(this ICollection<TModel> models) =>
            Faker.Random.CollectionItem(models);

        /// <summary>
        /// Picks a random model from the specified collection and optionally mutates it.
        /// </summary>
        /// <typeparam name="TModel">The type of the model.</typeparam>
        /// <param name="fixture">The fixture.</param>
        /// <param name="models">The models.</param>
        /// <param name="model">The model.</param>
        /// <param name="mutationFunc">The mutation function.</param>
        /// <returns></returns>
        public static IMvcFunctionalTestFixture HavingRandom<TModel>(this IMvcFunctionalTestFixture fixture, ICollection<TModel> models,
                                                                     out TModel model,
                                                                     Action<TModel> mutationFunc = null)
        {
            model = models.PickRandom();
            mutationFunc?.Invoke(model);
            return fixture;
        }

        /// <summary>
        /// Creates an auto fixture constructed instance of the specified model.
        /// </summary>
        /// <typeparam name="TModel">The type of the model.</typeparam>
        /// <param name="fixture">The fixture.</param>
        /// <returns></returns>
        public static TModel Create<TModel>(this IMvcFunctionalTestFixture fixture) => fixture.AutoFixture.Create<TModel>();

        /// <summary>
        /// Creates a collection of auto fixture constructed instances of the specified model.
        /// </summary>
        /// <typeparam name="TModel">The type of the model.</typeparam>
        /// <param name="fixture">The fixture.</param>
        /// <returns></returns>
        public static ICollection<TModel> CreateMany<TModel>(this IMvcFunctionalTestFixture fixture) => fixture.AutoFixture.CreateMany<TModel>().ToList();

        /// <summary>
        /// Creates an auto fixture constructed instance of the specified model.
        /// </summary>
        /// <typeparam name="TModel">The type of the model.</typeparam>
        /// <param name="fixture">The fixture.</param>
        /// <param name="model">The model.</param>
        /// <param name="configurator">The configurator.</param>
        /// <returns></returns>
        public static IMvcFunctionalTestFixture HavingModel<TModel>(this IMvcFunctionalTestFixture fixture,
                                                                    out TModel model,
                                                                    Action<TModel> configurator = null)
        {
            model = fixture.Create<TModel>();
            configurator?.Invoke(model);
            return fixture;
        }

        /// <summary>
        /// Creates a collection of auto fixture constructed instances of the specified model.
        /// </summary>
        /// <typeparam name="TModel">The type of the model.</typeparam>
        /// <param name="fixture">The fixture.</param>
        /// <param name="models">The models.</param>
        /// <param name="configurator">The configurator.</param>
        /// <returns></returns>
        public static IMvcFunctionalTestFixture HavingModels<TModel>(this IMvcFunctionalTestFixture fixture,
                                                                     out ICollection<TModel> models,
                                                                     Action<TModel> configurator = null)
        {
            models = fixture.CreateMany<TModel>();

            if (configurator != null)
            {
                foreach (var model in models)
                {
                    configurator(model);
                }
            }

            return fixture;
        }

        /// <summary>
        /// Configures the HTTP client to have the specified path base.
        /// </summary>
        /// <param name="fixture">The fixture.</param>
        /// <param name="path">The path.</param>
        /// <returns></returns>
        public static IMvcFunctionalTestFixture HavingPathBase(this IMvcFunctionalTestFixture fixture, string path) =>
            fixture.HavingClientConfiguration(o => o.BaseAddress = new UriBuilder("http://localhost")
            {
                Path = path.TrimStart('/').TrimEnd('/') + '/'
            }.Uri);

        /// <summary>
        /// Configures the request to use the specified string as a bearer token in the authorization header.
        /// </summary>
        /// <param name="fixture">The fixture.</param>
        /// <param name="token">The token.</param>
        /// <returns></returns>
        public static IMvcFunctionalTestFixture HavingBearerToken(this IMvcFunctionalTestFixture fixture, string token) =>
            fixture.HavingConfiguredHttpMessage(message => message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token));

        /// <summary>
        /// Adds the specified composer transformation function as an AutoFixture customization.
        /// </summary>
        /// <typeparam name="TModel">The type of the model.</typeparam>
        /// <param name="fixture">The fixture.</param>
        /// <param name="composer">The composer.</param>
        /// <returns></returns>
        public static IMvcFunctionalTestFixture HavingAutoFixtureCustomization<TModel>(this IMvcFunctionalTestFixture fixture,
                                                                                       Func<ICustomizationComposer<TModel>, ISpecimenBuilder> composer)
        {
            fixture.AutoFixture.Customize(composer);
            return fixture;
        }

        /// <summary>
        /// Configures the specified bootstrap function to be:
        /// 1. Added to the test server DI container.
        /// 2. Resolved and run once the test server is constructed.
        /// </summary>
        /// <param name="fixture">The fixture.</param>
        /// <param name="bootstrapAction">The action to perform on the service provider during bootstrap.</param>
        /// <returns></returns>
        public static IMvcFunctionalTestFixture HavingBootstrap(this IMvcFunctionalTestFixture fixture, Action<IServiceProvider> bootstrapAction) =>
            fixture.HavingBootstrap(p =>
            {
                bootstrapAction(p);
                return Task.CompletedTask;
            });
    }
}