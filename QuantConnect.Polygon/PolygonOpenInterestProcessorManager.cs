﻿/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using NodaTime;
using QuantConnect.Data.Market;
using QuantConnect.Lean.DataSource.Polygon.Rest;
using RestSharp;

namespace QuantConnect.Lean.DataSource.Polygon
{
    public class PolygonOpenInterestProcessorManager : IDisposable
    {
        /// <summary>
        /// Timer used to schedule the execution of the <see cref="ProcessOpenInterest"/> method.
        /// </summary>
        private Timer? _openInterestScheduler;

        /// <summary>
        /// Gets the time zone for New York City, USA. This is a daylight savings time zone.
        /// </summary>
        private static readonly DateTimeZone _nyTimeZone = TimeZones.NewYork;

        /// <summary>
        /// The time provider instance.
        /// </summary>
        private readonly ITimeProvider _timeProvider;

        /// <summary>
        /// The <see cref="PolygonRestApiClient"/> REST API client instance.
        /// </summary>
        private readonly PolygonRestApiClient _polygonRestApiClient;

        /// <summary>
        /// Provides the mapping between Lean symbols and Polygon.io symbols.
        /// </summary>
        private readonly PolygonSymbolMapper _symbolMapper;

        /// <summary>
        /// Subscription manager to handle the subscriptions for the Polygon data queue handler.
        /// </summary>
        private readonly PolygonSubscriptionManager _polygonSubscriptionManager;

        /// <summary>
        /// Aggregates Polygon.io trade bars into same or higher resolution bars
        /// </summary>
        private readonly PolygonAggregationManager _dataAggregator;

        /// <summary>
        /// A delegate that retrieves the tick time for a given symbol and UTC timestamp.
        /// </summary>
        /// <param name="symbol">The financial instrument or symbol for which the tick time is being retrieved.</param>
        /// <param name="utcTime">The UTC time for which the tick time is being calculated.</param>
        /// <returns>
        /// The tick time as a <see cref="DateTime"/> for the given <paramref name="symbol"/> and <paramref name="utcTime"/>.
        /// </returns>
        private readonly Func<Symbol, DateTime, DateTime> _getTickTime;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="timeProvider"></param>
        /// <param name="polygonRestApiClient"></param>
        /// <param name="symbolMapper"></param>
        /// <param name="polygonSubscriptionManager"></param>
        /// <param name="dataAggregator"></param>
        /// <param name="getTickTime"></param>
        public PolygonOpenInterestProcessorManager(ITimeProvider timeProvider, PolygonRestApiClient polygonRestApiClient, PolygonSymbolMapper symbolMapper,
            PolygonSubscriptionManager polygonSubscriptionManager, PolygonAggregationManager dataAggregator, Func<Symbol, DateTime, DateTime> getTickTime)
        {
            _timeProvider = timeProvider;
            _symbolMapper = symbolMapper;
            _dataAggregator = dataAggregator;
            _polygonRestApiClient = polygonRestApiClient;
            _polygonSubscriptionManager = polygonSubscriptionManager;
            _getTickTime = getTickTime;
            
            ScheduleNextRun();
        }

        /// <summary>
        /// Schedules the next execution of the <see cref="ProcessOpenInterest"/> method
        /// based on the current time in New York (Eastern Time).
        /// </summary>
        private void ScheduleNextRun()
        {
            var now = _timeProvider.GetUtcNow().ConvertFromUtc(_nyTimeZone);
            var nextRunTime = GetNextRunTime(now);

            TimeSpan delay = nextRunTime - now;

            // Initialize the timer to run the method at the calculated delay
            _openInterestScheduler = new Timer(RunProcessOpenInterest, null, delay, Timeout.InfiniteTimeSpan);
        }

        /// <summary>
        /// Runs the <see cref="ProcessOpenInterest"/> method and reschedules the next execution.
        /// </summary>
        private void RunProcessOpenInterest(object? _)
        {
            var subscribedSymbol = _polygonSubscriptionManager.GetSubscribedSymbols().ToList();

            ProcessOpenInterest(subscribedSymbol);

            // Reschedule for the next execution at either 9:30 AM or 3:30 PM
            ScheduleNextRun();
        }

        public void ProcessOpenInterest(IReadOnlyCollection<Symbol> subscribedSymbols)
        {
            var subscribedBrokerageSymbols = subscribedSymbols.Select(x => _symbolMapper.GetBrokerageSymbol(x));
            //var symbols = dataConfigs.Select(x => x.Symbol).ToList();
            //var subscribedSymbols = symbols.Select(x => _symbolMapper.GetBrokerageSymbol(x)).Distinct();

            var restRequest = new RestRequest($"v3/snapshot?ticker.any_of={string.Join(',', subscribedBrokerageSymbols)}", Method.GET);
            restRequest.AddQueryParameter("limit", "250");

            foreach (var universalSnapshot in _polygonRestApiClient.DownloadAndParseData<UniversalSnapshotResponse>(restRequest).SelectMany(response => response.Results))
            {
                var leanSymbol = _symbolMapper.GetLeanSymbol(universalSnapshot.Ticker!);
                var time = _getTickTime(leanSymbol, DateTime.UtcNow);
                var openInterest = new OpenInterest(time, leanSymbol, universalSnapshot.OpenInterest);

                _dataAggregator.Update(openInterest);
            }
        }

        /// <summary>
        /// Calculates the next run time (9:30 AM or 3:30 PM New York Time) based on the current time.
        /// </summary>
        /// <param name="currentTime">The current time in the New York time zone.</param>
        /// <returns>The next execution time at either 9:30 AM or 3:30 PM.</returns>
        private DateTime GetNextRunTime(DateTime currentTime)
        {
            DateTime today930AM = currentTime.Date.AddHours(9).AddMinutes(30);
            DateTime today330PM = currentTime.Date.AddHours(15).AddMinutes(30);

            if (currentTime < today930AM)
            {
                // If it's before 9:30 AM, schedule the next run for 9:30 AM today
                return today930AM;
            }
            else if (currentTime >= today930AM && currentTime < today330PM)
            {
                // If it's between 9:30 AM and 3:30 PM, schedule the next run for 3:30 PM today
                return today330PM;
            }
            else
            {
                // If it's after 3:30 PM, schedule the next run for 9:30 AM tomorrow
                return today930AM.AddDays(1);
            }
        }

        /// <summary>
        /// Disposes the resources used by the <see cref="OpenInterestProcessor"/>.
        /// </summary>
        public void Dispose()
        {
            _openInterestScheduler?.Dispose();
        }
    }
}