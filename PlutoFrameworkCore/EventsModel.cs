using PlutoFramework.Constants;
using PlutoFramework.Model.AjunaExt;
using PlutoFramework.Model.Temp;
using PlutoFramework.Types;
using Substrate.NetApi;
using Substrate.NetApi.Model.Rpc;
using Substrate.NetApi.Model.Types;
using Substrate.NetApi.Model.Types.Base;
using Substrate.NetApi.Model.Types.Primitive;
using System.Numerics;

namespace PlutoFramework.Model
{
    public class ExtrinsicDetails
    {
        public IEnumerable<ExtrinsicEvent> Events { get; set; }
        public BigInteger BlockNumber { get; set; }
        public uint? ExtrinsicIndex { get; set; }
    }
    public enum EventSafety
    {
        NothingUpdateUIBug,
        Safe,
        Ok,
        Unknown,
        Warning,
        Harmful,
    }
    public class ExtrinsicEvent
    {
        public string PalletName { get; set; }
        public string EventName { get; set; }
        public List<EventParameter> Parameters { get; set; }
        public EventSafety Safety { get; set; }

        public ExtrinsicEvent(string palletName, string eventName, List<EventParameter> parameters)
        {
            PalletName = palletName;
            EventName = eventName;
            Parameters = parameters;

            SetSafety();
        }

        private void SetSafety()
        {
            Safety = (PalletName, EventName, Parameters) switch
            {
                ("System", "ExtrinsicSuccess", _) => EventSafety.Safe,
                ("System", "ExtrinsicFailed", _) => EventSafety.Harmful,
                ("System", _, _) => EventSafety.Ok,
                ("Balances", "Deposit", _) => EventSafety.Safe,
                ("Balances", _, _) => EventSafety.Warning,
                ("Assets", _, _) => EventSafety.Warning,
                ("Tokens", _, _) => EventSafety.Warning,
                ("PolkadotXcm", _, _) => EventSafety.Warning,
                _ => EventSafety.Unknown,
            };
        }
    }

    public class EventParameter
    {
        public required string Name { get; set; }
        public required string Value { get; set; }
        public required byte[] EncodedValue { get; set; }
    }
    public static class EventsModel
    {

        public static List<EventParameter> GetParametersList(object? parameters, TypeField[] eventTypeFields)
        {
            if (parameters == null)
            {
                return new List<EventParameter>();
            }

            var parametersList = new List<EventParameter>();

            var pValues = eventTypeFields.Length switch
            {
                0 => [],
                1 => [(IType)parameters],
                _ => (IType[])parameters.GetProperty("Value")
            };

            for (int i = 0; i < pValues.Length; i++)
            {
                try
                {
                    var parameter = pValues[i];
                    var eventTypeField = eventTypeFields[i];

                    Type type = parameter.GetType();

                    var eventParameter = type.Name switch
                    {
                        "EnumMultiAddress" => (int)(parameter.GetProperty("Value") ?? -1) switch
                        {
                            0 => new EventParameter
                            {
                                Name = eventTypeField.Name,
                                Value = Utils.GetAddressFrom(((IType)parameter.GetProperty("Value2")).Encode()),
                                EncodedValue = parameter.Encode(),
                            },
                            _ => new EventParameter
                            {
                                Name = eventTypeField.Name,
                                Value = parameter.ToString(),
                                EncodedValue = parameter.Encode(),
                            }
                        },
                        "AccountId32" => new EventParameter
                        {
                            Name = eventTypeField.Name,
                            Value = Utils.GetAddressFrom(((IType)parameter.GetProperty("Value")).Encode()),
                            EncodedValue = parameter.Encode(),
                        },
                        "BaseCom`1" => new EventParameter
                        {
                            Name = eventTypeField.Name,
                            Value = parameter.GetProperty("Value").GetProperty("Value").ToString(),
                            EncodedValue = parameter.Encode(),
                        },
                        "Arr32U8" => new EventParameter
                        {
                            Name = eventTypeField.Name,
                            Value = Utils.Bytes2HexString(parameter.Encode()),
                            EncodedValue = parameter.Encode(),
                        },
                        "H256" => new EventParameter
                        {
                            Name = eventTypeField.Name,
                            Value = Utils.Bytes2HexString(((IType)parameter.GetProperty("Value")).Encode()),
                            EncodedValue = parameter.Encode(),
                        },
                        _ => new EventParameter
                        {
                            Name = eventTypeField.Name,
                            Value = parameter.ToString(),
                            EncodedValue = parameter.Encode(),
                        }
                    };

                    parametersList.Add(eventParameter);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }

            return parametersList;
        }

        public static object? GetProperty<T>(
            this T t,
            string propertyName
            )
        {
            return t?.GetType().GetProperty(propertyName)?.GetValue(t);
        }

        public static string GetValueString<T>(
            this T t,
            string propertyName = "Value"
            )
        {
            return t?.GetType().GetProperty(propertyName)?.GetValue(t)?.ToString() ?? "Unknown";
        }

        /// <summary>
        /// Gets all extrinsic events in the block
        /// </summary>
        /// <returns>all events for the given extrinsic</returns>
        /// <exception cref="ExtrinsicIndexNotFoundException"></exception>
        public static async Task<ExtrinsicDetails> GetExtrinsicEventsAsync(
            this SubstrateClientExt substrateClient,
            Hash blockHash,
            byte[] extrinsicHash,
            CancellationToken token = default
        )
        {
            string blockHashString = Utils.Bytes2HexString(blockHash);

            Console.WriteLine("block hash: " + blockHashString);

            var eventsParameters = RequestGenerator.GetStorage("System", "Events", Substrate.NetApi.Model.Meta.Storage.Type.Plain);

            string eventsBytes = await substrateClient.SubstrateClient.InvokeAsync<string>("state_getStorage", new object[2] { eventsParameters, blockHashString }, token);

            Console.WriteLine("Events bytes: " + eventsBytes);

            try
            {
                BlockData block = await substrateClient.SubstrateClient.Chain.GetBlockAsync(blockHash, CancellationToken.None);

                Console.WriteLine("block number: " + block.Block.Header.Number.Value);

                uint? extrinsicIndex = null;
                for (uint i = 0; i < block.Block.Extrinsics.Count(); i++)
                {
                    // Same extrinsic
                    if (Utils.Bytes2HexString(HashExtension.Blake2(block.Block.Extrinsics[i].Encode(), 256)).Equals(Utils.Bytes2HexString(extrinsicHash)))
                    {
                        extrinsicIndex = i;

                        break;
                    }
                }
                ;

                Console.WriteLine("Extrinsic index found: " + extrinsicIndex);

                return await GetExtrinsicEventsForClientAsync(substrateClient, extrinsicIndex, eventsBytes, blockNumber: block.Block.Header.Number.Value, token);
            }
            catch
            {

                var block = await substrateClient.SubstrateClient.InvokeAsync<TempOldBlockData>("chain_getBlock", new object[1] { blockHash?.Value }, token);

                Console.WriteLine("block number: " + block.Block.Header.Number.Value);

                uint? extrinsicIndex = null;
                for (uint i = 0; i < block.Block.Extrinsics.Count(); i++)
                {
                    // Same extrinsic
                    if (Utils.Bytes2HexString(HashExtension.Blake2(Utils.HexToByteArray(block.Block.Extrinsics[i]), 256)).Equals(Utils.Bytes2HexString(extrinsicHash)))
                    {
                        extrinsicIndex = i;

                        break;
                    }
                }
                ;

                Console.WriteLine("Extrinsic index found: " + extrinsicIndex);

                return await GetExtrinsicEventsForClientAsync(substrateClient, extrinsicIndex, eventsBytes, blockNumber: block.Block.Header.Number.Value, token);
            }
        }

        public static async Task<ExtrinsicDetails> GetExtrinsicEventsForClientAsync(
            this SubstrateClientExt substrateClient,
            uint? extrinsicIndex,
            string? eventsBytes,
            BigInteger blockNumber,
            CancellationToken token
        )
        {
            return await GetExtrinsicDetailsAsync(substrateClient, extrinsicIndex, eventsBytes, blockNumber, token);
        }

        /// <summary>
        /// Gets extrinsic details without events
        /// </summary>
        /// <returns>all events for the given extrinsic</returns>
        public static async Task<ExtrinsicDetails> GetExtrinsicDetailsAsync(
            this SubstrateClientExt substrateClient,
            uint? extrinsicIndex,
            string? _eventsBytes,
            BigInteger blockNumber,
            CancellationToken token = default
        )
        {
            return new ExtrinsicDetails
            {
                BlockNumber = blockNumber,
                ExtrinsicIndex = extrinsicIndex,
                Events = new List<ExtrinsicEvent>()
            };
        }
    }
}

