namespace LiteServerFrame.Core.General.Client
{
    public class MessageIndexGenerator
    {
        private static uint lastIndex;

        public static uint GetNextMessageIndex()
        {
            return ++lastIndex;
        }
    }
}