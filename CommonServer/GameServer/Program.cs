using CommonServer.Shared;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace GameServer
{
    internal class Program
    {
        static NetworkService networkMain;

        static async Task Main(string[] args)
        {
            networkMain = new NetworkService(args);

            Console.WriteLine("[SERVER] 게임 서버 시작");

            await networkMain.StartAsync();
        }
    }
}