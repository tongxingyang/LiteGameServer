using System;

namespace LiteServerFrame.ConsoleInput
{
    public class ConsoleInput
    {
        enum enInputState
        {
            Idle = 0,
            Inputing = 1
        }

        public static Action<string> onInputLine;
        public  static Action<ConsoleKey> onInputKey;

        private static enInputState ms_InputState = enInputState.Idle;
        private static string ms_InputBuffer = "";

        public static void Tick()
        {
            if (Console.KeyAvailable)
            {
                ConsoleKeyInfo info = Console.ReadKey();

                if (ms_InputState == enInputState.Inputing)
                {
                    if (info.Key == ConsoleKey.Enter)
                    {
                        ms_InputState = enInputState.Idle;
                        Console.WriteLine();
                        onInputLine.Invoke(ms_InputBuffer);
                    }
                    else if (info.Key == ConsoleKey.Escape)
                    {
                        ms_InputState = enInputState.Idle;
                        Console.WriteLine();
                    }
                    else if (info.Key >= ConsoleKey.A && info.Key <= ConsoleKey.Z)
                    {
                        ms_InputBuffer += info.KeyChar;
                    }
                    else if (info.Key >= ConsoleKey.D0 && info.Key <= ConsoleKey.D9)
                    {
                        ms_InputBuffer += info.KeyChar;
                    }
                    else 
                    {
                        ms_InputBuffer += info.KeyChar;
                    }
                }
                else if(ms_InputState == enInputState.Idle)
                {
                    onInputKey.Invoke(info.Key);
                    
                    if (info.Key == ConsoleKey.Enter)
                    {
                        ms_InputState = enInputState.Inputing;
                        ms_InputBuffer = "";
                        Console.Write("Input:");
                    }
                }
            }
        }
    }
}