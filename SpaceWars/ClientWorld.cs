using NetworkController;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System.Windows.Forms;
using System.Drawing;

namespace SpaceWars
{
    public class ClientWorld
    {
        private string playerName;
        private int playerID = -1;
        private int SCORE_BOARD_BUFFER = 250;//yet to be implemented
        public int shipSize = 30;
        public int starSize = 50;
        public int projSize = 20;
        private Socket serverSocket;
        public object worldLock = new object();
        private SpaceWarsPanel drawingPanel;
        private Size worldSize = new Size(-1, -1);
        private EventArgs e = null;

        public Dictionary<int, Ship> ships { get; private set; } = new Dictionary<int, Ship>();
        public Dictionary<int, Star> stars { get; private set; } = new Dictionary<int, Star>();
        public Dictionary<int, Projectile> projectiles { get; private set; } = new Dictionary<int, Projectile>();

        public Form View { private get; set; }
        public delegate void MessageHandler(EventArgs e);
        public event MessageHandler RecievedMessageFromServer;

        /// <summary>
        /// Attempts to connect to server
        /// </summary>
        /// <param name="thePlayerName"></param>
        /// <param name="theServerAdress"></param>
        public void StartGame(string thePlayerName, string theServerAdress)
        {
            playerName = thePlayerName;
            serverSocket = Networking.ConnectToServer(FirstContact, theServerAdress);
        }
        private void FirstContact(SocketState theClientState)
        {
            Networking.Send_Data(serverSocket, playerName);//server protocol is that the first recieved message must be the player name
            theClientState.messageProcessor = ProcessStartUP;
            Networking.GetData(theClientState);
        }
        /// <summary>
        /// Performs startup processing then passes future processing to the main message loop
        /// </summary>
        /// <param name="theClientState"></param>
        private void ProcessStartUP(SocketState theClientState)
        {
            Set_WorldSize_And_PlayerID(theClientState);
            drawingPanel = new SpaceWarsPanel(this) { Size = worldSize };
            View.Invoke(new MethodInvoker(() =>
            {
                View.Size = new Size(worldSize.Width + SCORE_BOARD_BUFFER, worldSize.Height + 38); //38 needed because form size takes toolbar into account
                View.Controls.Add(drawingPanel);
            }));
            theClientState.messageProcessor = ProcessMessages;//handoff to process message loop since setup is complete
            Networking.GetData(theClientState);
        }
        private void Set_WorldSize_And_PlayerID(SocketState theClientState)
        {
            string totalData = theClientState.sb.ToString();
            string[] messages = Regex.Split(totalData, @"\n");
            int.TryParse(messages[0], out playerID);
            int.TryParse(messages[1], out int size);
            worldSize = new Size(size, size);
        }
        /// <summary>
        /// This method acts as a NetworkAction delegate (see NetworkController.cs)
        /// When used as a NetworkAction, this method will be called whenever the Networking code
        /// has an event (ConnectedCallback or ReceiveCallback)
        /// For the chat client, all it does is look for messages separated by newlines, then prints them.
        /// </summary>
        /// <param name="theClientState"></param>
        private void ProcessMessages(SocketState theClientState)
        {
            string totalData = theClientState.sb.ToString();
            string[] messages = Regex.Split(totalData, @"(?<=[\n])");

            foreach (string message in messages)
            {
                if (message.Length == 0)
                    continue;
                //server protocol is that every message must end with a new line, if it doesn't, it is not the end of the message so we'll leave it in SB and process it when complete
                if (message[message.Length - 1] != '\n')
                    break;
                if (message.Contains("ship"))
                    UpdateShips(message);
                if (message.Contains("star"))
                    UpdateStars(message);
                if (message.Contains("proj"))
                    UpdateProjectiles(message);
                //messages without these keywords that still have newlines (for some reason) will be ignored and removed since the client only cares about these three things
                theClientState.sb.Remove(0, message.Length);
            }
            RecievedMessageFromServer(e);//trigger the event that causes client to send a message
            drawingPanel.Invalidate();//invalidate panel so it redraws with updated information
            Networking.GetData(theClientState);//keep loop going
        }
        private void UpdateShips(string message)
        {
            lock (worldLock)
            {
                Ship ship = JsonConvert.DeserializeObject<Ship>(message);
                ships[ship.ID] = ship;
            }
        }
        private void UpdateStars(string message)
        {
            lock (worldLock)
            {
                Star star = JsonConvert.DeserializeObject<Star>(message);
                stars[star.ID] = star;
            }
        }
        private void UpdateProjectiles(string message)
        {
            lock (worldLock)
            {
                Projectile projectile = JsonConvert.DeserializeObject<Projectile>(message);
                double locX = projectile.loc.GetX();
                double locY = projectile.loc.GetY();
                int wid = worldSize.Width / 2;
                int hei = worldSize.Height / 2;
                if (projectile.alive && -wid < locX && locX < wid && -hei < locY && locY < hei)
                    projectiles[projectile.ID] = projectile;
                else
                    projectiles.Remove(projectile.ID);
            }
        }
        /// <summary>
        /// Public access to sending client messages to the server. Formatting done by this method.
        /// </summary>
        /// <param name="message"></param>
        public void PerformMessageSend(string message)
        {
            if (message != "" && message != null)
            {
                message = message.Insert(0, "(");//server protocol for accepting commands; message syntax: (FFLLRRTT)\n
                message += ")\n";
                Networking.Send_Data(serverSocket, message);
            }
        }

       
    }

}
