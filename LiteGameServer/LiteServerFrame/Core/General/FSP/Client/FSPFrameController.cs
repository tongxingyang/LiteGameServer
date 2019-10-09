using CommonData.Proto;

namespace LiteServerFrame.Core.General.FSP.Client
{
    public class FSPFrameController
    {
        private int clientFrameRateMultiple = 2;
        private bool isInBuffing = false;
        private int newestFrameID;
        private bool enableSpeedUp = true;
        private int defaultSpeed = 1;
        private bool isInSpeedUp = false;
        private bool enableAutoBuff = true;
        private int autoBuffCnt = 0;
        private int autoBuffInterval = 15;

        public bool IsInBuffing => isInBuffing;
        public bool IsInSpeedUp => isInSpeedUp;
        public int JitterBufferSize { get; set; } = 0;
        public int NewestFrameID => newestFrameID;
        
        public void Start(FSPParam param)
        {
            SetParam(param);
        }

        private void SetParam(FSPParam param)
        {
            clientFrameRateMultiple = param.clientFrameRateMultiple;
            JitterBufferSize = param.jitterBufferSize;
            enableSpeedUp = param.enableSpeedUp;
            defaultSpeed = param.defaultSpeed;
            enableAutoBuff = param.enableAutoBuffer;
        }

        public void AddFrameID(int frameID)
        {
            newestFrameID = frameID;
        }

        public int GetFrameSpeed(int clientCurrentFrameID)
        {
            int speed = 0;
            var newFrameCount = newestFrameID - clientCurrentFrameID;
            if (!isInBuffing)
            {
                if (newFrameCount == 0)
                {
                    isInBuffing = true;
                    autoBuffCnt = autoBuffInterval;
                }
                else
                {
                    newFrameCount -= defaultSpeed;
                    int speedUpFrameNum = newFrameCount - JitterBufferSize;
                    if (speedUpFrameNum >= clientFrameRateMultiple)
                    {
                        if (enableSpeedUp)
                        {
                            speed = 2;
                            if (speedUpFrameNum > 100)
                            {
                                speed = 8;
                            }
                            else if (speedUpFrameNum > 50)
                            {
                                speed = 4;
                            }
                        }
                        else
                        {
                            speed = defaultSpeed;
                        }
                    }
                    else
                    {
                        speed = defaultSpeed;

                        if (enableAutoBuff)
                        {
                            autoBuffCnt--;
                            if (autoBuffCnt <= 0)
                            {
                                autoBuffCnt = autoBuffInterval;
                                if (speedUpFrameNum < clientFrameRateMultiple - 1)
                                {
                                    speed = 0;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                int speedUpFrameNum = newFrameCount - JitterBufferSize;
                if (speedUpFrameNum > 0)
                {
                    isInBuffing = false;
                }
            }
            
            isInBuffing = speed > defaultSpeed;
            return speed;
        }
    }
}