using System;
using System.Collections.Generic;
using System.IO;

namespace LiteServerFrame.Core.General.Base
{
    public class LengthEncoding
    {
        public static byte[] LengthEncode(byte[] data)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter br = new BinaryWriter(ms))
                {
                    br.Write(data.Length);
                    br.Write(data);
                    byte[] result = new byte[ms.Length];
                    Buffer.BlockCopy(ms.GetBuffer(), 0, result, 0, (int) ms.Length);
                    return result;
                }
            }
        }

        public static byte[] LengthDecode(ref List<byte> data)
        {
            if (data.Count < 4) return null;
            using (MemoryStream ms = new MemoryStream(data.ToArray()))  
            {
                using (BinaryReader br = new BinaryReader(ms))
                {
                    int length = br.ReadInt32();
                    if (length > ms.Length - ms.Position)
                    {
                        return null;
                    }
                    byte[] result = br.ReadBytes(length);
                    data.Clear();
                    data.AddRange(br.ReadBytes((int)(ms.Length - ms.Position)));
                    return result;
                }
            }
        }
    }
}