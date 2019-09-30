using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace CommonData.Proto
{
    [ProtoContract]
    public class FSPMessage
    {
        [ProtoMember(1)] public int cmd;
        [ProtoMember(2)] public int[] args;
        [ProtoMember(3)] public int custom;

        public uint playerID
        {
            get { return (uint) custom; }
            set { custom = (int) value; }
        }

        public int clientFrameID
        {
            get { return custom; }
            set { custom = value; }
        }

        public override string ToString()
        {
            if (args != null)
            {
                return $"cmd:{cmd}, args:{args}, custom:{custom}";
            }
            return $"cmd:{cmd}, args:[], custom:{custom}";
        }
    }

    [ProtoContract]
    public class FSPDataSToC
    {
        [ProtoMember(1)] public List<FSPFrameData> frames = new List<FSPFrameData>();
//        [ProtoMember(1)] public FSPFrameData frame;
    }

    [ProtoContract]
    public class FSPDataCToS
    {
        [ProtoMember(1)] public uint sessionID = 0;
        [ProtoMember(2)] public List<FSPMessage> msgs = new List<FSPMessage>();
//        [ProtoMember(2)] public FSPMessage msg;
    }

    /// <summary>
    /// 服务器发送给客户端一个逻辑帧中所有用户的操作数据
    /// </summary>
    [ProtoContract]
    public class FSPFrameData
    {
        [ProtoMember(1)] public int frameID;
        [ProtoMember(2)] public List<FSPMessage> msgs = new List<FSPMessage>();


        public bool IsEmpty()
        {
            return (msgs == null || msgs.Count == 0);
        }


        public bool Contains(int cmd)
        {
            if (!IsEmpty())
            {
                foreach (FSPMessage message in msgs)
                {
                    if (message.cmd == cmd)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public override string ToString()
        {
            string tmp = "";
            for (int i = 0; i < msgs.Count - 1; i++)
            {
                tmp += msgs[i] + ",";
            }

            if (msgs.Count > 0)
            {
                tmp += msgs[msgs.Count - 1].ToString();
            }

            return $"frameId:{frameID}, msgs:[{tmp}]";
        }
    }

    [ProtoContract]
    public class FSPParam
    {
        [ProtoMember(1)] public string host;
        [ProtoMember(2)] public int port;
        [ProtoMember(3)] public uint sessionID;
        [ProtoMember(4)] public int serverFrameInterval = 66;
        [ProtoMember(5)] public int serverTimeout = 15000;
        [ProtoMember(6)] public int clientFrameRateMultiple = 2;
        [ProtoMember(7)] public int authID = 0;
        [ProtoMember(8)] public bool useLocal = false;
        [ProtoMember(9)] public int maxFrameID = 1800;
        [ProtoMember(10)] public bool enableSpeedUp = true;
        [ProtoMember(11)] public int defaultSpeed = 1;
        [ProtoMember(12)] public int jitterBufferSize = 0;
        [ProtoMember(13)] public bool enableAutoBuffer = true;

        public string ToString(string prefix = "")
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("\n{0}host:{1}:{2}", prefix, host, port);
            sb.AppendFormat("\n{0}serverFrameInterval:{1}", prefix, serverFrameInterval);
            sb.AppendFormat("\n{0}clientFrameRateMultiple:{1}", prefix, clientFrameRateMultiple);
            sb.AppendFormat("\n{0}serverTimeout:{1}", prefix, serverTimeout);
            sb.AppendFormat("\n{0}maxFrameId:{1}", prefix, maxFrameID);
            return sb.ToString();
        }
    }
}