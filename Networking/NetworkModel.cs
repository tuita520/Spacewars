using System;
using System.Net;
using System.Net.Sockets;
using System.Text;


namespace NetworkController
{
    public delegate void NetworkAction(SocketState state);

    /// <summary>
    /// The state that holds the listener and it's delegate process.
    /// </summary>
    class ConnectionState
    {
        /// <summary>
        /// Action performed when client is 
        /// </summary>
        public NetworkAction clientProcessor;
        public TcpListener listener;
        public ConnectionState(TcpListener s)
        {
            listener = s;
        }
    }

    /// <summary>
    /// This class holds all the necessary state to represent a socket connection
    /// Note that all of its fields are public because we are using it like a "struct"
    /// It is a simple collection of fields
    /// </summary>
    public class SocketState
    {
        /// <summary>
        /// Action performed when recieving data from a callback
        /// </summary>
        public NetworkAction messageProcessor;
        public Socket theSocket;
        public int ID;
        public byte[] messageBuffer = new byte[1024];
        public StringBuilder sb = new StringBuilder();
        public SocketState(Socket s, int id)
        {
            theSocket = s;
            ID = id;
        }
    }

    public static class Networking
    {

        public const int DEFAULT_PORT = 11000;


        /// <summary>
        /// Creates a Socket object for the given host string
        /// </summary>
        /// <param name="hostName">The host name or IP address</param>
        /// <param name="socket">The created Socket</param>
        /// <param name="ipAddress">The created IPAddress</param>
        public static void MakeSocket(string hostName, out Socket socket, out IPAddress ipAddress)
        {
            ipAddress = IPAddress.None;
            socket = null;
            try
            {
                // Establish the remote endpoint for the socket.
                IPHostEntry ipHostInfo;
                // Determine if the server address is a URL or an IP
                try
                {
                    ipHostInfo = Dns.GetHostEntry(hostName);
                    bool foundIPV4 = false;
                    foreach (IPAddress addr in ipHostInfo.AddressList)
                        if (addr.AddressFamily != AddressFamily.InterNetworkV6)
                        {
                            foundIPV4 = true;
                            ipAddress = addr;
                            break;
                        }
                    // Didn't find any IPV4 addresses
                    if (!foundIPV4)
                    {
                        System.Diagnostics.Debug.WriteLine("Invalid addres: " + hostName);
                        throw new ArgumentException("Invalid address");
                    }
                }
                catch (Exception)
                {
                    // see if host name is actually an ipaddress, i.e., 155.99.123.456
                    System.Diagnostics.Debug.WriteLine("using IP");
                    ipAddress = IPAddress.Parse(hostName);
                }
                // Create a TCP/IP socket.
                socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
                // Disable Nagle's algorithm - can speed things up for tiny messages, 
                // such as for a game
                socket.NoDelay = true;
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("Unable to create socket. Error occured: " + e);
                throw new ArgumentException("Invalid address");
            }
        }

        /// <summary>
        /// Begin a listener loop and give it a process to execute once the client is connected.
        /// It is the message processor's duty to maintain a data loop using Get Data
        /// </summary>
        /// <param name="theMessageProcessor"></param>
        public static void Server_Awaiting_Client_Loop(NetworkAction theMessageProcessor)
        {
            TcpListener listener = new TcpListener(IPAddress.Any, DEFAULT_PORT);
            Console.WriteLine("Server waiting for clients");
            listener.Start();
            ConnectionState listenerState = new ConnectionState(listener) { clientProcessor = theMessageProcessor };
            listener.BeginAcceptSocket(Accept_New_Client, listenerState);

        }
        /// <summary>
        /// Start attempting to connect to the server
        /// </summary>
        /// <param name="hostName"> server to connect to </param>
        /// <returns></returns>
        public static Socket ConnectToServer(NetworkAction theMessageProcessor, string hostName)
        {
            System.Diagnostics.Debug.WriteLine("connecting  to " + hostName);
            Socket clientSocket;
            IPAddress ipAddress;
            MakeSocket(hostName, out clientSocket, out ipAddress);
            SocketState ClientState = new SocketState(clientSocket, -1);
            ClientState.messageProcessor = theMessageProcessor;
            ClientState.theSocket.BeginConnect(ipAddress, DEFAULT_PORT, ConnectedCallback, ClientState);
            return ClientState.theSocket;
        }

        private static void Accept_New_Client(IAsyncResult ar)
        {
            Accept_New_Client(ar, -1);//client ID must be set in serverworld, networking shouldn't know about serverworld
        }
        private static void Accept_New_Client(IAsyncResult ar, int clientID)
        {//Note: Make Socket is never called because we are recieving the socket the the client made
            Console.WriteLine("Contact from client");
            ConnectionState listenerState = (ConnectionState)ar.AsyncState;
            //Get the new client, put it in a state, and give it the delegate the listener was given to start things off
            Socket clientSocket = listenerState.listener.EndAcceptSocket(ar);
            SocketState ClientState = new SocketState(clientSocket, clientID);
            ClientState.messageProcessor = listenerState.clientProcessor;
            ClientState.messageProcessor(ClientState);//call the delegate to finish the handshake
            //Keep the loop going
            listenerState.listener.BeginAcceptSocket(Accept_New_Client, listenerState);
        }

        /// <summary>
        /// GetData is just a wrapper for BeginReceive.
        /// This is the public entry point for asking for data.
        /// Necessary so that we can separate networking concerns from client/server concerns.
        /// </summary>
        /// <param name="theClientState"></param>
        public static void GetData(SocketState theClientState)
        {
            try
            {
                theClientState.theSocket.BeginReceive(theClientState.messageBuffer, 0, theClientState.messageBuffer.Length, SocketFlags.None, ReceiveCallback, theClientState);
            }
            catch (SocketException e)
            {
                Console.WriteLine(e.Message);
                return;
            }
        }
        /// <summary>
        /// SendData is a wrapper for BeginSend. This is the public entry point for sending data.
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="data"></param>
        public static void Send_Data(Socket socket, String data)
        {
            byte[] messageBytes = Encoding.UTF8.GetBytes(data);
            socket.BeginSend(messageBytes, 0, messageBytes.Length, SocketFlags.None, SendCallback, socket);
        }

        /// <summary>
        /// This function is "called" by the operating system when the remote site acknowledges connect request
        /// </summary>
        /// <param name="ar"></param>
        private static void ConnectedCallback(IAsyncResult ar)
        {
            SocketState state = (SocketState)ar.AsyncState;
            try
            {
                state.theSocket.EndConnect(ar);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("Unable to connect to server. Error occured: " + e);
                return;
            }
            state.messageProcessor(state);
        }
        /// <summary>
        /// This function is "called" by the operating system when data is available on the socket
        /// </summary>
        /// <param name="ar"></param>
        public static void ReceiveCallback(IAsyncResult ar)
        {
            int bytesRead;
            SocketState state;
            try
            {
                state = (SocketState)ar.AsyncState;
                bytesRead = state.theSocket.EndReceive(ar);
            }
            catch (SocketException e)
            {
                Console.WriteLine(e.Message);
                return;
            }

            // If the socket is still open
            if (bytesRead > 0)
            {
                string theMessage = Encoding.UTF8.GetString(state.messageBuffer, 0, bytesRead);
                state.sb.Append(theMessage);
                state.messageProcessor(state);
            }

        }
        /// <summary>
        /// A callback invoked when a send operation completes
        /// </summary>
        /// <param name="ar"></param>
        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                Socket s = (Socket)ar.AsyncState;
                s.EndSend(ar);
            }
            catch (SocketException e)
            {
                Console.WriteLine(e.Message);
                return;

            }


        }
    }


}