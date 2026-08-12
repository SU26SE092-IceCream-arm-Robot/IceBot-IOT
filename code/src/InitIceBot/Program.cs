using System;

namespace InitIceBot
{
    internal static class Program
    {
        private static void Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            IceBot.IceBotAdministration.Run();
        }
    }
}
