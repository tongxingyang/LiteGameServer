using System;
using System.Collections.Generic;
using System.Text;
using CommonData.Proto;

namespace LiteServerFrame.Core.General.FSP.Server
{
    public class FSPPlayer
    {
        private uint id;
        private bool hasAuthed = false;
        private FSPSession session;
        private Action<FSPPlayer, FSPMessage> recvListener;
        private Queue<FSPFrameData> frameCache = null;
        private int lastAddFrameID = 0;
        private int authID = 0;
        
        public bool WaitForExit = false;
        public uint ID => id;
        public bool HasAuthed => hasAuthed;
        public bool IsLose() => !session.IsActived();
        
        public void Create(uint id, int authId, FSPSession session, Action<FSPPlayer, FSPMessage> listener)
        {
            this.id = id;
            this.authID = authId;
            recvListener = listener;
            this.session = session;
            this.session.SetReceiveListener(OnRecvFromSession);
            frameCache = new Queue<FSPFrameData>();
        }
        
        public void Release()
        {
            if (session != null)
            {
                session.SetReceiveListener(null);
                session.Active(false);
                session = null;
            }
        }
        
        private void OnRecvFromSession(FSPDataCToS data)
        {
            if (session.IsEndPointChanged)
            {
                session.IsEndPointChanged = false;
                hasAuthed = false;
            }

            recvListener?.Invoke(this, data.msg);
        }
        
        public void SendToClient(FSPFrameData frame)
        {
            if (frame != null)
            {
                if (!frameCache.Contains(frame))
                {
                    frameCache.Enqueue(frame);
                }
            }


            while (frameCache.Count > 0)
            {
                if (SendInternal(frameCache.Peek()))
                {
                    frameCache.Dequeue();
                }
            }
        }
        
        private bool SendInternal(FSPFrameData frame)
        {
            if (frame.frameID != 0 && frame.frameID <= lastAddFrameID)
            {
                return true;
            }

            if (session != null)
            {
                if (session.Send(frame))
                {
                    lastAddFrameID = frame.frameID;
                    return true;
                }
            }

            return false;
        }
        
        public void SetAuth(int authId)
        {
            hasAuthed = this.authID == authId;
        }
        
        public void ClearRound()
        {
            frameCache.Clear();
            lastAddFrameID = 0;
        }
        
        public string ToString(string prefix = "")
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("[{0}] Auth:{1}, IsLose:{2}, EndPoint:{3}", id, HasAuthed, IsLose(), session.RemoteEndPoint);
            return sb.ToString();
        }

    }
}