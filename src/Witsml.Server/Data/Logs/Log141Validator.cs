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

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Energistics.DataAccess.WITSML141;
using Energistics.DataAccess.WITSML141.ComponentSchemas;
using PDS.Framework;
using PDS.Witsml.Data.Channels;
using PDS.Witsml.Data.Logs;
using PDS.Witsml.Server.Configuration;

namespace PDS.Witsml.Server.Data.Logs
{
    /// <summary>
    /// Provides validation for <see cref="Log" /> data objects.
    /// </summary>
    /// <seealso cref="PDS.Witsml.Server.Data.DataObjectValidator{Log}" />
    [Export(typeof(IDataObjectValidator<Log>))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class Log141Validator : DataObjectValidator<Log>
    {
        private readonly IWitsmlDataAdapter<Log> _logDataAdapter;
        private readonly IWitsmlDataAdapter<Wellbore> _wellboreDataAdapter;
        private readonly IWitsmlDataAdapter<Well> _wellDataAdapter;

        private readonly string[] _illegalColumnIdentifiers = { "'", "\"", "<", ">", "/", "\\", "&", "," };
        private static readonly string _dataDelimiterErrorMessage = WitsmlSettings.DataDelimiterErrorMessage;

        /// <summary>
        /// Initializes a new instance of the <see cref="Log141Validator" /> class.
        /// </summary>
        /// <param name="logDataAdapter">The log data adapter.</param>
        /// <param name="wellboreDataAdapter">The wellbore data adapter.</param>
        /// <param name="wellDataAdapter">The well data adapter.</param>
        [ImportingConstructor]
        public Log141Validator(IWitsmlDataAdapter<Log> logDataAdapter, IWitsmlDataAdapter<Wellbore> wellboreDataAdapter, IWitsmlDataAdapter<Well> wellDataAdapter)
        {
            _logDataAdapter = logDataAdapter;
            _wellboreDataAdapter = wellboreDataAdapter;
            _wellDataAdapter = wellDataAdapter;
        }

        /// <summary>
        /// Gets or sets the Witsml configuration providers.
        /// </summary>
        /// <value>
        /// The providers.
        /// </value>
        [ImportMany]
        public IEnumerable<IWitsml141Configuration> Providers { get; set; }

        /// <summary>
        /// Validates the data object while executing GetFromStore
        /// </summary>
        /// <returns>A collection of validation results.</returns>
        protected override IEnumerable<ValidationResult> ValidateForGet()
        {
            if ( (Parser.HasElements("startIndex") || Parser.HasElements("endIndex")) && (Parser.HasElements("startDateTimeIndex") || Parser.HasElements("endDateTimeIndex") ))
            {
                yield return new ValidationResult(ErrorCodes.MixedStructuralRangeIndices.ToString(), new[] { "StartIndex", "EndIndex", "StartDateTimeIndex", "EndDateTimeIndex" });
            }

            var logDatas = Parser.Properties("logData").ToArray();
            if (logDatas.Length > 1)
            {
                yield return new ValidationResult(ErrorCodes.RecurringLogData.ToString(), new[] { "LogData" });
            }

            var mnemonics = Parser.GetLogDataMnemonics();
            var mnemonicList = mnemonics?.ToArray() ?? new string[0];

            if (logDatas.Length == 1)
            {
                if (mnemonicList.Any() && DuplicateUid(mnemonicList))
                {
                    yield return new ValidationResult(ErrorCodes.DuplicateMnemonics.ToString(), new[] { "LogData", "MnemonicsList" });
                }            
            }

            if (OptionsIn.ReturnElements.Requested.Equals(Parser.ReturnElements()))
            {
                var logCurveInfoMnemonics = Parser.GetLogCurveInfoMnemonics().ToList();                
                var logCurveInfos = Parser.Properties("logCurveInfo").ToArray();

                if (logCurveInfoMnemonics.Count() != logCurveInfos.Length)
                {
                    yield return new ValidationResult(ErrorCodes.MissingMnemonicElement.ToString(), new[] { "LogCurveInfo", "Mnemonic" });
                }

                if (logDatas.Length == 1)
                {                 
                    if (logCurveInfoMnemonics.Any() && mnemonicList.Any() && !(logCurveInfoMnemonics.All(x => mnemonicList.Contains(x)) && mnemonicList.All(y => logCurveInfoMnemonics.Contains(y))))
                    {
                        yield return new ValidationResult(ErrorCodes.ColumnIdentifiersNotSame.ToString(), new[] { "LogData", "MnemonicList" });
                    }

                    if (mnemonics == null)
                    {
                        yield return new ValidationResult(ErrorCodes.MissingMnemonicList.ToString(), new[] { "LogData", "MnemonicsList" });
                    }
                }
            }
        }

        /// <summary>
        /// Validates the data object while executing AddToStore.
        /// </summary>
        /// <returns>A collection of validation results.</returns>
        protected override IEnumerable<ValidationResult> ValidateForInsert()
        {
            var logCurves = DataObject.LogCurveInfo;
            var uri = DataObject.GetUri();
            var uriWellbore = uri.Parent;
            var uriWell = uriWellbore.Parent;
            var wellbore = _wellboreDataAdapter.Get(uriWellbore);
            var indexCurve = DataObject.IndexCurve;

            var logDatas = DataObject.LogData;
            var logCurveInfoMnemonics = new List<string>();

            logCurves?.ForEach(l => logCurveInfoMnemonics.Add(l.Mnemonic.Value));

            // Validate parent uid property
            if (string.IsNullOrWhiteSpace(DataObject.UidWell))
            {
                yield return new ValidationResult(ErrorCodes.MissingParentUid.ToString(), new[] { "UidWell" });
            }
            // Validate parent uid property
            else if (string.IsNullOrWhiteSpace(DataObject.UidWellbore))
            {
                yield return new ValidationResult(ErrorCodes.MissingParentUid.ToString(), new[] { "UidWellbore" });
            }

            // Validate parent exists
            else if (!_wellDataAdapter.Exists(uriWell))
            {
                yield return new ValidationResult(ErrorCodes.MissingParentDataObject.ToString(), new[] { "UidWell" });
            }
            // Validate parent exists
            else if (wellbore == null)
            {
                yield return new ValidationResult(ErrorCodes.MissingParentDataObject.ToString(), new[] { "UidWellbore" });
            }

            else if (!wellbore.UidWell.Equals(DataObject.UidWell) || !wellbore.Uid.Equals(DataObject.UidWellbore))
            {
                yield return new ValidationResult(ErrorCodes.IncorrectCaseParentUid.ToString(), new[] { "UidWellbore" });
            }

            // Validate UID does not exist
            else if (_logDataAdapter.Exists(uri))
            {
                yield return new ValidationResult(ErrorCodes.DataObjectUidAlreadyExists.ToString(), new[] { "Uid" });
            }

            // Validate that uid for LogParam exists
            else if (DataObject.LogParam != null && DataObject.LogParam.Any(lp => string.IsNullOrWhiteSpace(lp.Uid)))
            {
                yield return new ValidationResult(ErrorCodes.MissingElementUid.ToString(), new[] { "LogParam", "Uid" });
            }

            // Validate for a bad column identifier in LogCurveInfo Mnemonics
            else if (_illegalColumnIdentifiers.Any(s => { return logCurves != null && logCurves.Any(m => m.Mnemonic.Value.Contains(s)); }))
            {
                yield return new ValidationResult(ErrorCodes.BadColumnIdentifier.ToString(), new[] { "LogCurveInfo.Mnemonic" });
            }

            // Validate that column-identifiers in LogCurveInfo are unique
            else if (DuplicateColumnIdentifier())
            {
                yield return new ValidationResult(ErrorCodes.DuplicateColumnIdentifiers.ToString(), new[] { "LogCurveInfo", "Mnemonic" });
            }

            // Validate structural-range indices for consistent index types
            else if ((DataObject.StartIndex != null || DataObject.EndIndex != null) && (DataObject.StartDateTimeIndex != null || DataObject.EndDateTimeIndex != null))
            {
                yield return new ValidationResult(ErrorCodes.MixedStructuralRangeIndices.ToString(), new[] { "StartIndex", "EndIndex", "StartDateTimeIndex", "EndDateTimeIndex" });
            }

            // Validate that the dataDelimiter does not contain any white space
            else if (!DataObject.IsValidDataDelimiter())
            {
                yield return new ValidationResult(_dataDelimiterErrorMessage, new[] { "DataDelimiter" });
            }

            // Validate if MaxDataPoints has been exceeded
            else if (logDatas != null && logDatas.Count > 0 )
            {
                yield return ValidateLogData(indexCurve, logCurves, logDatas, logCurveInfoMnemonics, DataObject.GetDataDelimiterOrDefault());
            }
        }

        /// <summary>
        /// Validates the data object while executing UpdateInStore.
        /// </summary>
        /// <returns>
        /// A collection of validation results.
        /// </returns>
        protected override IEnumerable<ValidationResult> ValidateForUpdate()
        {
            // Validate Log uid property
            if (string.IsNullOrWhiteSpace(DataObject.UidWell) || string.IsNullOrWhiteSpace(DataObject.UidWellbore) || string.IsNullOrWhiteSpace(DataObject.Uid))
            {
                yield return new ValidationResult(ErrorCodes.DataObjectUidMissing.ToString(), new[] { "Uid", "UidWell", "UidWellbore" });
            }
            else
            {
                var uri = DataObject.GetUri();
                var logCurves = DataObject.LogCurveInfo;
                var logParams = DataObject.LogParam;
                var logData = DataObject.LogData;

                var current = _logDataAdapter.Get(uri);
                var delimiter = current?.GetDataDelimiterOrDefault();

                var mergedLogCurveMnemonics = new List<string>();

                current?.LogCurveInfo.ForEach(l => mergedLogCurveMnemonics.Add(l.Mnemonic.Value));
                logCurves?.ForEach(l =>
                {
                    if (l.Mnemonic != null && !mergedLogCurveMnemonics.Contains(l.Mnemonic.Value))
                    {
                        mergedLogCurveMnemonics.Add(l.Mnemonic.Value);
                    }
                });


                // Validate Log does not exist
                if (current == null)
                {
                    yield return new ValidationResult(ErrorCodes.DataObjectNotExist.ToString(), new[] { "Uid", "UidWell", "UidWellbore" });
                }

                // Validate that uid for LogCurveInfo exists
                else if (logCurves != null && logCurves.Any(l => string.IsNullOrWhiteSpace(l.Uid)))
                {
                    yield return new ValidationResult(ErrorCodes.MissingElementUid.ToString(), new[] { "LogCurveInfo", "Uid" });
                }

                // Validate that uid for LogParam exists
                else if (logParams != null && logParams.Any(lp => string.IsNullOrWhiteSpace(lp.Uid)))
                {
                    yield return new ValidationResult(ErrorCodes.MissingElementUid.ToString(), new[] { "LogParam", "Uid" });
                }

                // Validate that uids in LogCurveInfo are unique
                else if (logCurves != null && DuplicateUid(logCurves.Select(l => l.Uid)))
                {
                    yield return new ValidationResult(ErrorCodes.ChildUidNotUnique.ToString(), new[] { "LogCurveInfo", "Uid" });
                }

                // Validate that uids in LogParam are unique
                else if (logParams != null && DuplicateUid(logParams.Select(l => l.Uid)))
                {
                    yield return new ValidationResult(ErrorCodes.ChildUidNotUnique.ToString(), new[] { "LogParam", "Uid" });
                }

                else if (DuplicateColumnIdentifier())
                {
                    yield return new ValidationResult(ErrorCodes.DuplicateColumnIdentifiers.ToString(), new[] { "LogCurveInfo", "Mnemonic" });
                }

                // Validate that the dataDelimiter does not contain any white space
                else if (!DataObject.IsValidDataDelimiter())
                {
                    yield return new ValidationResult(_dataDelimiterErrorMessage, new[] { "DataDelimiter" });
                }

                // Validate LogCurveInfo
                else if (logCurves != null)
                {
                    var indexCurve = current.IndexCurve;
                    var indexCurveUid = current.LogCurveInfo.FirstOrDefault(l => l.Mnemonic.Value == indexCurve)?.Uid;
                    var isTimeLog = current.IsTimeLog(true);
                    var exist = current.LogCurveInfo ?? new List<LogCurveInfo>();
                    var uids = exist.Select(e => e.Uid.ToUpper()).ToList();
                    var newCurves = logCurves.Where(l => !uids.Contains(l.Uid.ToUpper())).ToList();
                    var updateCurves = logCurves.Where(l => !l.Uid.EqualsIgnoreCase(indexCurveUid) && uids.Contains(l.Uid.ToUpper())).ToList();

                    if (newCurves.Count > 0 && updateCurves.Count > 0)
                    {
                        yield return new ValidationResult(ErrorCodes.AddingUpdatingLogCurveAtTheSameTime.ToString(), new[] { "LogCurveInfo", "Uid" });
                    }
                    else if (isTimeLog && newCurves.Any(c => c.MinDateTimeIndex.HasValue || c.MaxDateTimeIndex.HasValue)
                        || !isTimeLog && newCurves.Any(c => c.MinIndex != null || c.MaxIndex != null))
                    {
                        yield return new ValidationResult(ErrorCodes.IndexRangeSpecified.ToString(), new[] { "LogCurveInfo", "Index" });
                    }
                    else if (logData != null && logData.Count > 0)
                    {
                        yield return ValidateLogData(indexCurve, logCurves, logData, mergedLogCurveMnemonics, delimiter, false);
                    }
                }

                // TODO: check if this is still needed

                // Validate LogData
                else if (logData != null && logData.Count > 0)
                {
                    yield return ValidateLogData(current.IndexCurve, null, logData, mergedLogCurveMnemonics, delimiter, false);
                }
            }
        }

        private bool DuplicateUid(IEnumerable<string> uids)
        {
            return uids.GroupBy(u => u)
                .Select(group => new { Uid = group.Key, Count = group.Count() })
                .Any(g => g.Count > 1);
        }

        private bool DuplicateColumnIdentifier()
        {
            var logCurves = DataObject.LogCurveInfo;
            if (logCurves == null || logCurves.Count == 0)
                return false;

            return logCurves.Where(l => l.Mnemonic != null).GroupBy(lci => lci.Mnemonic.Value)
                .Select(group => new { Mnemonic = group.Key, Count = group.Count() })
                .Any(g => g.Count > 1);
        }

        private bool UnitsMatch(List<LogCurveInfo> logCurves, LogData logData)
        {
            var mnemonics = ChannelDataReader.Split(logData.MnemonicList);
            var units = ChannelDataReader.Split(logData.UnitList);

            for (var i = 0; i < mnemonics.Length; i++)
            {
                var mnemonic = mnemonics[i];
                var logCurve = logCurves.FirstOrDefault(l => l.Mnemonic.Value.EqualsIgnoreCase(mnemonic));
                if (logCurve == null)
                    continue;

                if (string.IsNullOrEmpty(units[i].Trim()) && string.IsNullOrEmpty(logCurve.Unit) ||
                    units[i].Trim().EqualsIgnoreCase(logCurve.Unit))
                    continue;
                return false;
            }
            return true;
        }

        private ValidationResult ValidateLogData(string indexCurve, List<LogCurveInfo> logCurves, List<LogData> logDatas, List<string> mergedLogCurveInfoMnemonics, string delimiter, bool insert = true)
        {
            var totalPoints = 0;

            if (logDatas.SelectMany(ld => ld.Data).Count() > WitsmlSettings.MaxDataNodes)
            {
                return new ValidationResult(ErrorCodes.MaxDataExceeded.ToString(), new[] { "LogData", "Data" });
            }
            else
            {
                foreach (var logData in logDatas)
                {
                    if (string.IsNullOrWhiteSpace(logData.MnemonicList))
                        return new ValidationResult(ErrorCodes.MissingColumnIdentifiers.ToString(), new[] { "LogData", "MnemonicList" });
                    else
                    {
                        var mnemonics = ChannelDataReader.Split(logData.MnemonicList);
                        if (logData.Data != null && logData.Data.Count > 0)
                            totalPoints += logData.Data.Count * ChannelDataReader.Split(logData.Data[0], delimiter).Length;

                        if (totalPoints > WitsmlSettings.MaxDataPoints)
                        {
                            return new ValidationResult(ErrorCodes.MaxDataExceeded.ToString(), new[] { "LogData", "Data" });
                        }
                        else if (mnemonics.Distinct().Count() < mnemonics.Count())
                        {
                            return new ValidationResult(ErrorCodes.MnemonicsNotUnique.ToString(), new[] { "LogData", "MnemonicList" });
                        }
                        else if (mnemonics.Any(m => _illegalColumnIdentifiers.Any(c => m.Contains(c))))
                        {
                            return new ValidationResult(ErrorCodes.BadColumnIdentifier.ToString(), new[] { "LogData", "MnemonicList" });
                        } 
                        else if (!IsValidLogDataMnemonics(mergedLogCurveInfoMnemonics, mnemonics))
                        {
                            return new ValidationResult(ErrorCodes.MissingColumnIdentifiers.ToString(), new[] { "LogData", "MnemonicList" });
                        }
                        else if (insert && logCurves != null && mnemonics.Count() > logCurves.Count)
                        {
                            return new ValidationResult(ErrorCodes.BadColumnIdentifier.ToString(), new[] { "LogData", "MnemonicList" });
                        }
                        else if (string.IsNullOrWhiteSpace(logData.UnitList))
                        {
                            return new ValidationResult(ErrorCodes.MissingUnitList.ToString(), new[] { "LogData", "UnitList" });
                        }
                        else if (!UnitSpecified(logCurves, logData))
                        {
                            return new ValidationResult(ErrorCodes.MissingUnitForMeasureData.ToString(), new[] { "LogData", "UnitList" });
                        }
                        else if (!string.IsNullOrEmpty(indexCurve) && mnemonics.All(m => m != indexCurve))
                        {
                            return new ValidationResult(ErrorCodes.IndexCurveNotFound.ToString(), new[] { "IndexCurve" });
                        }
                        else if (!mnemonics[0].EqualsIgnoreCase(indexCurve))
                        {
                            return new ValidationResult(ErrorCodes.IndexNotFirstInDataColumnList.ToString(), new[] { "LogData", "MnemonicList" });
                        }
                        else if (DuplicateUid(mnemonics))
                        {
                            return new ValidationResult(ErrorCodes.MnemonicsNotUnique.ToString(), new[] { "LogData", "MnemonicList" });
                        }                      
                        else if (logCurves != null && !UnitsMatch(logCurves, logData))
                        {
                            return new ValidationResult(ErrorCodes.UnitListNotMatch.ToString(), new[] { "LogData", "UnitList" });
                        }
                    }
                }
            }

            return null;
        }

        private bool IsValidLogDataMnemonics(List<string> logCurveInfoMnemonics, IEnumerable<string> logDataMnemonics)
        {
            Logger.Debug("Validating mnemonic list channels for existance in LogCurveInfo.");

            var isValid = !logDataMnemonics.Any(um => !logCurveInfoMnemonics.Contains(um));

            Logger.Debug(isValid
                ? "Validation of mnemonic list channels successful."
                : "Mnemonic from mnemonic list does not exist in LogCurveInfo.");

            return isValid;
        }


        private bool UnitSpecified(List<LogCurveInfo> logCurves, LogData logData)
        {
            var mnemonics = ChannelDataReader.Split(logData.MnemonicList);
            var units = ChannelDataReader.Split(logData.UnitList);

            for (var i = 0; i < mnemonics.Length; i++)
            {
                var mnemonic = mnemonics[i];
                var logCurve = logCurves.FirstOrDefault(l => l.Mnemonic.Value.EqualsIgnoreCase(mnemonic));

                // If there are not enough units to cover all of the mnemonics OR 
                //... the LogCurve has a unit and the unit is empty the the unit is not specified.
                if (units.Length <= i || (!string.IsNullOrEmpty(logCurve?.Unit) && string.IsNullOrEmpty(units[i].Trim())))
                    return false;
            }
            return true;
        }
    }
}
