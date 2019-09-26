using System;
using System.Collections.Generic;
using System.Text;
using LiteServerFrame.Utility;
using ProtoBuf;

namespace LiteServerFrame.Core.General.Base
{
    [ProtoContract]
    public class RPCMessage
    {
        [ProtoMember(1)]
        public string name;
        
        [ProtoMember(2)]
        public List<RPCArg> rawargs = new List<RPCArg>();
        
        private static readonly List<object> tempObjList = new List<object>();
        
        public object[] args
        {
            get
            {
                tempObjList.Clear();
                foreach (RPCArg arg in rawargs)
                {
                    tempObjList.Add(arg.value);
                }
                return tempObjList.ToArray();
            }

            set
            {
                rawargs = new List<RPCArg>();
                object[] list = value;
                foreach (object arg in list)
                {
                    var raw_arg = new RPCArg {value = arg};
                    rawargs.Add(raw_arg);
                }
            }
        }
    }

    [ProtoContract]
    public class RPCArg
    {
        [ProtoMember(1)]
        public enRPCArgType type;
        [ProtoMember(2)]
        public byte[] rawValue;

        public object value
        {
            get
            {
                if (rawValue == null || rawValue.Length == 0)
                {
                    return null;
                }

                NetBufferReader reader = new NetBufferReader(rawValue);
                switch (type)
                {
                    case enRPCArgType.Int: return reader.ReadInt();
                    case enRPCArgType.UInt: return reader.ReadUInt();
                    case enRPCArgType.Long: return reader.ReadLong();
                    case enRPCArgType.ULong: return reader.ReadULong();
                    case enRPCArgType.Short: return reader.ReadShort();
                    case enRPCArgType.UShort: return reader.ReadUShort();
                    case enRPCArgType.Double: return reader.ReadDouble();
                    case enRPCArgType.Float: return reader.ReadFloat();
                    case enRPCArgType.String: return Encoding.UTF8.GetString(rawValue);
                    case enRPCArgType.Byte: return reader.ReadByte();
                    case enRPCArgType.Bool: return reader.ReadByte() != 0;
                    case enRPCArgType.ByteArray: return rawValue;
                    case enRPCArgType.PBObject: return rawValue;
                    default: return rawValue;
                }

            }
            set
            {
                object v = value;
                if (v is int)
                {
                    type = enRPCArgType.Int;
                    rawValue = BitConverter.GetBytes((int) v);
                    NetBuffer.ReverseOrder(rawValue);
                }
                else if (v is uint)
                {
                    type = enRPCArgType.UInt;
                    rawValue = BitConverter.GetBytes((uint) v);
                    NetBuffer.ReverseOrder(rawValue);
                }
                else if (v is long)
                {
                    type = enRPCArgType.Long;
                    rawValue = BitConverter.GetBytes((long) v);
                    NetBuffer.ReverseOrder(rawValue);
                }
                else if (v is ulong)
                {
                    type = enRPCArgType.ULong;
                    rawValue = BitConverter.GetBytes((ulong) v);
                    NetBuffer.ReverseOrder(rawValue);
                }
                else if (v is short)
                {
                    type = enRPCArgType.Short;
                    rawValue = BitConverter.GetBytes((short) v);
                    NetBuffer.ReverseOrder(rawValue);
                }
                else if (v is ushort)
                {
                    type = enRPCArgType.UShort;
                    rawValue = BitConverter.GetBytes((ushort) v);
                    NetBuffer.ReverseOrder(rawValue);
                }
                else if (v is double)
                {
                    type = enRPCArgType.Double;
                    rawValue = BitConverter.GetBytes((double) v);
                }
                else if (v is float)
                {
                    type = enRPCArgType.Float;
                    rawValue = BitConverter.GetBytes((float) v);
                    NetBuffer.ReverseOrder(rawValue);
                }
                else if (v is string)
                {
                    type = enRPCArgType.String;
                    rawValue = Encoding.UTF8.GetBytes((string) v);
                }
                else if (v is byte)
                {
                    type = enRPCArgType.Byte;
                    rawValue = new[] {(byte) v};
                }
                else if (v is bool)
                {
                    type = enRPCArgType.Bool;
                    rawValue = new[] {(bool) v ? (byte) 1 : (byte) 0};
                }
                else if (v is byte[])
                {
                    type = enRPCArgType.ByteArray;
                    rawValue = new byte[((byte[]) v).Length];
                    Buffer.BlockCopy((byte[]) v, 0, rawValue, 0, rawValue.Length);
                }
                else
                {
                    var bytes = ProtoBuffUtility.Serialize(v);
                    if (bytes != null)
                    {
                        type = enRPCArgType.PBObject;
                        rawValue = new byte[bytes.Length];
                        Buffer.BlockCopy(bytes, 0, rawValue, 0, rawValue.Length);
                    }
                    else
                    {
                        type = enRPCArgType.Unkown;
                    }
                }
            }
        }
    }
}