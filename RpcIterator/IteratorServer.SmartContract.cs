using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.IO.Json;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Iterators;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.VM.Types;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Neo.Plugins
{
    partial class IteratorServer
    {
        private class Signers : IVerifiable
        {
            private readonly Signer[] _signers;
            public Witness[] Witnesses { get; set; }
            public int Size => _signers.Length;

            public Signers(Signer[] signers)
            {
                _signers = signers;
            }

            public void Serialize(BinaryWriter writer)
            {
                throw new NotImplementedException();
            }

            public void Deserialize(BinaryReader reader)
            {
                throw new NotImplementedException();
            }

            public void DeserializeUnsigned(BinaryReader reader)
            {
                throw new NotImplementedException();
            }

            public UInt160[] GetScriptHashesForVerifying(DataCache snapshot)
            {
                return _signers.Select(p => p.Account).ToArray();
            }

            public Signer[] GetSigners()
            {
                return _signers;
            }

            public void SerializeUnsigned(BinaryWriter writer)
            {
                throw new NotImplementedException();
            }
        }

        //private static JObject ToJson(StackItem item, int max)
        //{
        //    JObject json = item.ToJson();
        //    if (item is InteropInterface interopInterface && interopInterface.GetInterface<object>() is IIterator iterator)
        //    {
        //        JArray array = new();
        //        while (max > 0 && iterator.Next())
        //        {
        //            array.Add(iterator.Value().ToJson());
        //            max--;
        //        }
        //        json["iterator"] = array;
        //        json["truncated"] = iterator.Next();
        //    }
        //    return json;
        //}

        private static Signers SignersFromJson(JArray _params, ProtocolSettings settings)
        {
            var ret = new Signers(_params.Select(u => new Signer()
            {
                Account = AddressToScriptHash(u["account"].AsString(), settings.AddressVersion),
                Scopes = (WitnessScope)Enum.Parse(typeof(WitnessScope), u["scopes"]?.AsString()),
                AllowedContracts = ((JArray)u["allowedcontracts"])?.Select(p => UInt160.Parse(p.AsString())).ToArray(),
                AllowedGroups = ((JArray)u["allowedgroups"])?.Select(p => ECPoint.Parse(p.AsString(), ECCurve.Secp256r1)).ToArray()
            }).ToArray())
            {
                Witnesses = _params
                    .Select(u => new
                    {
                        Invocation = u["invocation"]?.AsString(),
                        Verification = u["verification"]?.AsString()
                    })
                    .Where(x => x.Invocation != null || x.Verification != null)
                    .Select(x => new Witness()
                    {
                        InvocationScript = Convert.FromBase64String(x.Invocation ?? string.Empty),
                        VerificationScript = Convert.FromBase64String(x.Verification ?? string.Empty)
                    }).ToArray()
            };

            // Validate format

            _ = IO.Helper.ToByteArray(ret.GetSigners()).AsSerializableArray<Signer>();

            return ret;
        }

        //===========

        [IteratorMethod]
        protected virtual JObject InvokeIterator(JArray _params)
        {
            UInt160 script_hash = UInt160.Parse(_params[0].AsString());
            string operation = _params[1].AsString();
            ContractParameter[] args = _params.Count >= 3 ? ((JArray)_params[2]).Select(p => ContractParameter.FromJson(p)).ToArray() : System.Array.Empty<ContractParameter>();
            Signers signers = _params.Count >= 4 ? SignersFromJson((JArray)_params[3], system.Settings) : null;
            uint startIndex = _params.Count >= 5 ? Convert.ToUInt32(_params[4].AsNumber()) : 0;
            uint endIndex = _params.Count >= 6 ? Convert.ToUInt32(_params[5].AsNumber()) : 10;

            byte[] script;
            using ScriptBuilder sb = new();
            script = sb.EmitDynamicCall(script_hash, operation, args).ToArray();

            Transaction tx = signers == null ? null : new Transaction
            {
                Signers = signers.GetSigners(),
                Attributes = System.Array.Empty<TransactionAttribute>(),
                Witnesses = signers.Witnesses,
            };
            using ApplicationEngine engine = ApplicationEngine.Run(script, system.StoreView, container: tx, settings: system.Settings, gas: settings.MaxGasInvoke);
            JObject json = new();
            json["script"] = Convert.ToBase64String(script);
            json["state"] = engine.State;
            json["gasconsumed"] = engine.GasConsumed.ToString();
            json["exception"] = GetExceptionMessage(engine.FaultException);
            List<object> resultList = new();
            try
            {
                var result = engine.ResultStack.FirstOrDefault() as VM.Types.InteropInterface;
                if (result is null)
                {
                    json["result"] = null;
                }
                else
                {
                    var iterator = result.GetInterface<IIterator>();
                    for (int i = 0; iterator.Next(); i++)
                    {
                        if (i >= startIndex && i < endIndex)
                            resultList.Add(iterator.Value());
                    }
                    json["result"] = new JArray(resultList.Select(p => ((VM.Types.StackItem)p).ToJson()));
                }
            }
            catch (InvalidOperationException)
            {
                json["stack"] = "error: recursive reference";
            }
            if (engine.State != VMState.FAULT)
            {
                ProcessInvokeWithWallet(json, signers);
            }

            return json;
        }

      

        static string GetExceptionMessage(Exception exception)
        {
            return exception?.GetBaseException().Message;
        }
    }
}
