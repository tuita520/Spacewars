using NetworkController;
using System;

namespace SpaceWars
{

    class SpaceWarsServer
    {
        static void Main()
        {
            SpaceWarsServer server = new SpaceWarsServer();
            Console.Read();
        }

        public SpaceWarsServer()
        {
            ServerWorld theWorld = new ServerWorld();
            Networking.Server_Awaiting_Client_Loop(theWorld.End_HandShake);
            theWorld.Start_Game_Update_Loop();
        }
    }
}

