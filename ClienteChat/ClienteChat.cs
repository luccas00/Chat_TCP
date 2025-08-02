using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Chat_TCP
{
    public class ClienteChat : Form
    {
        private TcpClient cliente;
        private NetworkStream stream;
        private TcpListener servidorPrivado;
        private Thread threadReceber;
        private Thread threadServidorPrivado;

        private TextBox txtMensagens;
        private TextBox txtNickname;
        private TextBox txtServerIp;
        private NumericUpDown numServerPort;
        private ListBox lstUsuarios;
        private TextBox txtBroadcast;
        private Button btnConnect;
        private Button btnListar;
        private Button btnBroadcast;
        private Button btnPrivado;

        private string apelido;
        private int portaPrivada;

        public ClienteChat()
        {
            Text = "Chat TCP Cliente";
            Width = 600;
            Height = 500;
            InitializeUI();
        }

        private void InitializeUI()
        {
            var panelConfig = new Panel { Dock = DockStyle.Top, Height = 60 };
            txtNickname = new TextBox { PlaceholderText = "Apelido", Width = 100, Left = 5, Top = 5 };
            txtServerIp = new TextBox { PlaceholderText = "Servidor IP", Width = 120, Left = 115, Top = 5 };
            numServerPort = new NumericUpDown { Minimum = 1, Maximum = 65535, Value = 1998, Left = 245, Top = 5, Width = 60 };
            btnConnect = new Button { Text = "Conectar ao Servidor", Left = 315, Top = 3, Width = 100 };
            btnConnect.Click += BtnConnect_Click;
            panelConfig.Controls.AddRange(new Control[] { txtNickname, txtServerIp, numServerPort, btnConnect });
            Controls.Add(panelConfig);

            txtMensagens = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Top,
                Height = 200
            };
            Controls.Add(txtMensagens);

            var panelUsers = new Panel { Dock = DockStyle.Top, Height = 150 };
            btnListar = new Button { Text = "Listar Usuários", Enabled = false, Dock = DockStyle.Top, Height = 30 };
            btnListar.Click += (s, e) => SendCommand("/lista");
            lstUsuarios = new ListBox { Dock = DockStyle.Top, Height = 90, Enabled = false };
            btnPrivado = new Button { Text = "Conectar Chat Privado", Enabled = false, Dock = DockStyle.Top, Height = 30 };
            btnPrivado.Click += (s, e) => ConnectPrivado();
            panelUsers.Controls.AddRange(new Control[] { btnPrivado, lstUsuarios, btnListar });
            Controls.Add(panelUsers);

            var panelBroadcast = new Panel { Dock = DockStyle.Bottom, Height = 30 };
            txtBroadcast = new TextBox { Dock = DockStyle.Fill, Enabled = false };
            btnBroadcast = new Button { Text = "Broadcast", Dock = DockStyle.Right, Width = 100, Enabled = false };
            btnBroadcast.Click += (s, e) => DoBroadcast();
            panelBroadcast.Controls.AddRange(new Control[] { txtBroadcast, btnBroadcast });
            Controls.Add(panelBroadcast);
        }

        private void BtnConnect_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNickname.Text))
            {
                MessageBox.Show("Defina um apelido válido.");
                return;
            }
            apelido = txtNickname.Text.Trim();
            string ip = txtServerIp.Text.Trim();
            int port = (int)numServerPort.Value;
            try
            {
                servidorPrivado = new TcpListener(IPAddress.Any, 0);
                servidorPrivado.Start();
                portaPrivada = ((IPEndPoint)servidorPrivado.LocalEndpoint).Port;
                threadServidorPrivado = new Thread(PrivateServerLoop) { IsBackground = true };
                threadServidorPrivado.Start();

                cliente = new TcpClient();
                cliente.Connect(ip, port);
                stream = cliente.GetStream();
                var dados = $"{apelido};{portaPrivada}";
                var bytes = Encoding.UTF8.GetBytes(dados);
                stream.Write(bytes, 0, bytes.Length);

                threadReceber = new Thread(ReceiveLoop) { IsBackground = true };
                threadReceber.Start();

                btnListar.Enabled = true;
                lstUsuarios.Enabled = true;
                btnBroadcast.Enabled = true;
                txtBroadcast.Enabled = true;
                btnPrivado.Enabled = true;
                btnConnect.Enabled = false;
                txtNickname.Enabled = false;
                txtServerIp.Enabled = false;
                numServerPort.Enabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Falha ao conectar: {ex.Message}");
            }
        }

        private void PrivateServerLoop()
        {
            while (true)
            {
                try
                {
                    // Aceita conexão do cliente privado
                    var clientPriv = servidorPrivado.AcceptTcpClient();
                    Invoke((MethodInvoker)(() =>
                    {
                        // Usa construtor específico para conexões recebidas
                        var janela = new JanelaChatPrivado(apelido, clientPriv);
                        janela.Show();
                    }));
                }
                catch { break; }
            }
        }

        private void ReceiveLoop()
        {
            var buffer = new byte[1024];
            try
            {
                while (true)
                {
                    int bytes = stream.Read(buffer, 0, buffer.Length);
                    if (bytes == 0) break;
                    string msg = Encoding.UTF8.GetString(buffer, 0, bytes);
                    if (msg.Contains(";") && msg.Contains("\n"))
                        Invoke((MethodInvoker)(() => UpdateUserList(msg)));
                    else
                        Invoke((MethodInvoker)(() => txtMensagens.AppendText(msg + Environment.NewLine)));
                }
            }
            catch { }
            finally
            {
                cliente.Close();
                Invoke((MethodInvoker)(() => MessageBox.Show("Desconectado do servidor.")));
            }
        }

        private void SendCommand(string cmd)
        {
            var bytes = Encoding.UTF8.GetBytes(cmd);
            stream.Write(bytes, 0, bytes.Length);
        }

        private void DoBroadcast()
        {
            string text = txtBroadcast.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;
            string conteudo = $"[Broadcast] {apelido}: {text}";
            var bytes = Encoding.UTF8.GetBytes(conteudo);
            stream.Write(bytes, 0, bytes.Length);
            txtBroadcast.Clear();
        }

        private void ConnectPrivado()
        {
            if (lstUsuarios.SelectedItem == null)
            {
                MessageBox.Show("Selecione um usuário.");
                return;
            }
            string item = lstUsuarios.SelectedItem.ToString();
            string apelidoDest = item.Substring(0, item.IndexOf('(')).Trim();
            var addr = item.Substring(item.IndexOf('(') + 1).TrimEnd(')').Split(':');
            string ip = addr[0];
            int port = int.Parse(addr[1]);
            // Usa loopback caso IP seja local (mesma máquina)
            if (IsLocalAddress(ip))
                ip = IPAddress.Loopback.ToString();
            var janela = new JanelaChatPrivado(apelido, apelidoDest, ip, port);
            janela.Show();
        }

        private void UpdateUserList(string data)
        {
            lstUsuarios.Items.Clear();
            var lines = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var cols = line.Split(';');
                if (cols.Length == 3)
                    lstUsuarios.Items.Add($"{cols[0]} ({cols[1]}:{cols[2]})");
            }
        }

        private bool IsLocalAddress(string ip)
        {
            if (ip == IPAddress.Loopback.ToString()) return true;
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var addr in host.AddressList)
                if (addr.AddressFamily == AddressFamily.InterNetwork && addr.ToString() == ip)
                    return true;
            return false;
        }

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.Run(new ClienteChat());
        }
    }
}
