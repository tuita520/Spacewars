namespace View
{
    partial class View
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.PlayerName = new System.Windows.Forms.TextBox();
            this.ConnectButton = new System.Windows.Forms.Button();
            this.serverAddress = new System.Windows.Forms.TextBox();
            this.bindingSource1 = new System.Windows.Forms.BindingSource(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.bindingSource1)).BeginInit();
            this.SuspendLayout();
            // 
            // nameBox
            // 
            this.PlayerName.Location = new System.Drawing.Point(32, 29);
            this.PlayerName.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.PlayerName.Name = "nameBox";
            this.PlayerName.Size = new System.Drawing.Size(260, 38);
            this.PlayerName.TabIndex = 0;
            this.PlayerName.Text = "bicboi";
            // 
            // ConnectButton
            // 
            this.ConnectButton.Location = new System.Drawing.Point(597, 29);
            this.ConnectButton.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.ConnectButton.Name = "ConnectButton";
            this.ConnectButton.Size = new System.Drawing.Size(304, 55);
            this.ConnectButton.TabIndex = 1;
            this.ConnectButton.Text = "Connect";
            this.ConnectButton.UseVisualStyleBackColor = true;
            this.ConnectButton.Click += new System.EventHandler(this.ConnectButton_Click);
            // 
            // serverAddress
            // 
            this.serverAddress.Location = new System.Drawing.Point(315, 29);
            this.serverAddress.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.serverAddress.Name = "serverAddress";
            this.serverAddress.Size = new System.Drawing.Size(260, 38);
            this.serverAddress.TabIndex = 2;
            this.serverAddress.Text = "localhost";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(16F, 31F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(2115, 1140);
            this.Controls.Add(this.serverAddress);
            this.Controls.Add(this.ConnectButton);
            this.Controls.Add(this.PlayerName);
            this.KeyPreview = true;
            this.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.Name = "Form1";
            this.Text = "Form1";
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.View_KeyDown);
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.View_KeyUp);
            ((System.ComponentModel.ISupportInitialize)(this.bindingSource1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox PlayerName;
        private System.Windows.Forms.Button ConnectButton;
        private System.Windows.Forms.TextBox serverAddress;
        private System.Windows.Forms.BindingSource bindingSource1;

    }

}

