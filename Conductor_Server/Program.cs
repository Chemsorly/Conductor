using System;

namespace Conductor_Server
{
    class Program
    {
        static void Main(string[] args)
        {
            //init
            Core core = new Core();
            core.NewLogMessageEvent += delegate (string message)
            {
                if (message != null)
                {
                    Console.WriteLine(message);
                }
            };
            core.Initialize();

            Console.ReadLine();
        }
    }
}
