﻿//----------------------------------------------------------------------- 
// PDS.Witsml.Server, 2016.1
//
// Copyright 2016 Petrotechnical Data Systems
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//   
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//-----------------------------------------------------------------------

using System.ComponentModel.Composition;
using Energistics.DataAccess.WITSML200;
using PDS.Framework;

namespace PDS.Witsml.Server.Data.Channels
{
    /// <summary>
    /// Data provider that implements support for WITSML API functions for <see cref="ChannelSet"/>.
    /// </summary>
    /// <seealso cref="PDS.Witsml.Server.Data.EtpDataProvider{ChannelSet}" />
    [Export(typeof(IEtpDataProvider))]
    [Export(typeof(IEtpDataProvider<ChannelSet>))]
    [Export200(ObjectTypes.ChannelSet, typeof(IEtpDataProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class ChannelSet200DataProvider : EtpDataProvider<ChannelSet>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ChannelSet200DataProvider"/> class.
        /// </summary>
        /// <param name="container">The composition container.</param>
        /// <param name="dataAdapter">The data adapter.</param>
        [ImportingConstructor]
        public ChannelSet200DataProvider(IContainer container, IWitsmlDataAdapter<ChannelSet> dataAdapter) : base(container, dataAdapter)
        {
        }

        /// <summary>
        /// Sets the default values for the specified data object.
        /// </summary>
        /// <param name="dataObject">The data object.</param>
        protected override void SetDefaultValues(ChannelSet dataObject)
        {
            base.SetDefaultValues(dataObject);
            dataObject.Channel.ForEach(c => c.Uuid = c.NewUuid());
        }
    }
}
