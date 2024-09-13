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

using Newtonsoft.Json;

namespace QuantConnect.Lean.DataSource.Polygon.Rest
{
    public class UniversalSnapshot
    {
        /// <summary>
        /// The details for this contract.
        /// </summary>
        [JsonProperty("details")]
        public OptionContract? OptionContract { get; set; }

        /// <summary>
        /// The quantity of this contract held at the end of the last trading day.
        /// </summary>
        [JsonProperty("open_interest")]
        public decimal OpenInterest { get; set; }

        /// <summary>
        /// The ticker symbol for the asset.
        /// </summary>
        [JsonProperty("ticker")]
        public string? Ticker { get; set; }

        /// <summary>
        /// The asset class for this ticker.
        /// </summary>
        [JsonProperty("type")]
        public string? Type { get; set; }
    }
}