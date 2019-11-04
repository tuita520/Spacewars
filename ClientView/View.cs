using SpaceWars;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace View
{
    public partial class View : Form
    {
        ClientWorld clientWorld;
        private bool rightKey;
        private bool leftKey;
        private bool thrustKey;
        private bool fireKey;

        public View()
        {
            InitializeComponent();
            //positioning
            Size screenSize = Screen.PrimaryScreen.Bounds.Size;
            screenSize.Height /= 2; screenSize.Width /= 2;
            this.Size = screenSize;
            this.StartPosition = FormStartPosition.CenterScreen;

            clientWorld = new ClientWorld() { View = this };//client world needs a reference back to the form to change form properties based on server's instructions
            clientWorld.RecievedMessageFromServer += new ClientWorld.MessageHandler(Send_New_Message);
        }

        private void ConnectButton_Click(object sender, EventArgs e)
        {
            if (serverAddress.Text == "")
            {
                MessageBox.Show("Please enter a server address");
                return;
            }
            else if (PlayerName.Text == "")
            {
                MessageBox.Show("Please enter a Player Name");
                return;
            }
            clientWorld.StartGame(PlayerName.Text, serverAddress.Text);

            ConnectButton.Dispose();
            serverAddress.Dispose();
            PlayerName.Dispose();
        }
        private void Send_New_Message(EventArgs e)
        {
            string newMessage = "";
            if (thrustKey)
                newMessage += "T";
            if (rightKey)
                newMessage += "R";
            if (leftKey)
                newMessage += "L";
            if (fireKey)
                newMessage += "F";
            if (newMessage != "")
                clientWorld.PerformMessageSend(newMessage);
        }
        private void View_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.W || e.KeyData == Keys.Up)
            {
                thrustKey = true;
            }
            if (e.KeyData == Keys.A || e.KeyData == Keys.Left)
            {
                leftKey = true;
            }
            if (e.KeyData == Keys.D || e.KeyData == Keys.Right)
            {
                rightKey = true;
            }
            if (e.KeyData == Keys.F)
            {
                fireKey = true;
            }
            if (e.KeyData == Keys.Space)
                 fireKey = fireKey; //line for breakpoint during debugging, press space to stop
        }
        private void View_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.W || e.KeyData == Keys.Up)
            {
                thrustKey = false;
            }
            if (e.KeyData == Keys.A || e.KeyData == Keys.Left)
            {
                leftKey = false;
            }
            if (e.KeyData == Keys.D || e.KeyData == Keys.Right)
            {
                rightKey = false;
            }
            if (e.KeyData == Keys.F)
            {
                fireKey = false;
            }
        }
    }
}
