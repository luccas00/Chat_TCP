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
        private Button btnDiscover;
        private Button btnDisconnect;

        private string apelido;
        private int portaPrivada;

        public ClienteChat()
        {
            Text = "Chat TCP Cliente - Desconectado";
            Width = 600;
            Height = 500;
            InitializeUI();
            StartUdpDiscovery();
            
        }

        //public string DiscoverServer(int timeoutMs = 3000)
        //{
        //    using var udp = new UdpClient();
        //    udp.EnableBroadcast = true;
        //    var discoverEP = new IPEndPoint(IPAddress.Broadcast, 30001);
        //    byte[] payload = Encoding.UTF8.GetBytes("DISCOVER_SERVER");

        //    Console.WriteLine("Enviando broadcast para descobrir servidor...");

        //    // Envia broadcast discovery
        //    udp.Send(payload, payload.Length, discoverEP);

        //    // Define timeout para reduzir latência
        //    var asyncResult = udp.BeginReceive(null, null);
        //    if (asyncResult.AsyncWaitHandle.WaitOne(timeoutMs))
        //    {
        //        IPEndPoint serverEP = null;
        //        byte[] response = udp.EndReceive(asyncResult, ref serverEP);
        //        // Payload contem o IP do servidor
        //        return Encoding.UTF8.GetString(response);
        //    }

        //    return null; // Nenhuma resposta no SLA definido
        //}

        // CLIENTE: dispara hand-shake e retorna o IP enviado pelo servidor
        public string DiscoverServer(int timeoutMs = 3000)
        {
            using var udp = new UdpClient(AddressFamily.InterNetwork); // porta efêmera
            udp.EnableBroadcast = true;
            udp.Client.ReceiveTimeout = timeoutMs;

            byte[] payload = Encoding.UTF8.GetBytes("DISCOVER_SERVER");
            var broadcastIp = IPAddress.Parse("192.168.1.255");
            // envia para o listener de discovery do servidor (porta 30001)
            //udp.Send(payload, payload.Length, new IPEndPoint(broadcastIp, 30001));
            udp.Send(payload, payload.Length, new IPEndPoint(IPAddress.Broadcast, 30001));

            try
            {
                var remoteEP = new IPEndPoint(IPAddress.Any, 0);
                byte[] response = udp.Receive(ref remoteEP);
                // decodifica o serverIp que o servidor colocou no payload de reply
                return Encoding.UTF8.GetString(response);
            }
            catch (SocketException)
            {
                return null; // sem resposta dentro do SLA
            }
        }


        private void StartUdpDiscovery()
        {
            Thread udpListener = new(() =>
            {
                using var udp = new UdpClient(30000);
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                var remoteEP = new IPEndPoint(IPAddress.Any, 0);
                while (true)
                {
                    try
                    {
                        byte[] data = udp.Receive(ref remoteEP);
                        string discoveredIp = Encoding.UTF8.GetString(data);
                        Invoke((MethodInvoker)(() => txtServerIp.Text = discoveredIp));
                    }
                    catch { /* ignore */ }
                }
            })
            { IsBackground = true };
            udpListener.Start();

        }


        private void InitializeUI()
        {
            // 1) Linha única com Apelido, Servidor IP e Porta
            var panelInputs = new Panel
            {
                Dock = DockStyle.Top,
                Height = 35
            };
            Controls.Add(panelInputs);

            txtNickname = new TextBox
            {
                PlaceholderText = "Apelido",
                Width = 150,
                Top = 5
            };
            txtServerIp = new TextBox
            {
                PlaceholderText = "Servidor IP",
                Width = 200,
                Top = 5
            };
            numServerPort = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 65535,
                Value = 1998,
                Width = 80,
                Top = 5
            };

            // calcula posição horizontal para centralizar os três
            int spacing = 10;
            int totalWidth = txtNickname.Width + txtServerIp.Width + numServerPort.Width + spacing * 2;
            int startX = (ClientSize.Width - totalWidth) / 2;
            txtNickname.Left = startX;
            txtServerIp.Left = txtNickname.Right + spacing;
            numServerPort.Left = txtServerIp.Right + spacing;

            panelInputs.Controls.AddRange(new Control[]
            {
            txtNickname,
            txtServerIp,
            numServerPort
            });

            // 2) Segunda linha: três botões lado a lado (Buscar Servidor, Conectar, Desconectar)
            var panelButtons = new Panel
            {
                Dock = DockStyle.Top,
                Height = 45
            };
            Controls.Add(panelButtons);

            btnDiscover = new Button
            {
                Text = "Buscar Servidor",
                Width = 140,
                Height = 30,
                Top = 5
            };
            btnDiscover.Click += BtnDiscover_Click;

            btnConnect = new Button
            {
                Text = "Conectar",
                Width = 140,
                Height = 30,
                Top = 5
            };
            btnConnect.Click += BtnConnect_Click;

            btnDisconnect = new Button
            {
                Text = "Desconectar",
                Width = 140,
                Height = 30,
                Top = 5,
                Enabled = false
            };
            btnDisconnect.Click += BtnDisconnect_Click;

            // layout centralizado na ordem: Buscar, Conectar, Desconectar
            int spacingBtn = 10;
            int totalWidthBtn = btnDiscover.Width + btnConnect.Width + btnDisconnect.Width + spacingBtn * 2;
            int startXBtn = (ClientSize.Width - totalWidthBtn) / 2;

            btnDiscover.Left = startXBtn;
            btnConnect.Left = btnDiscover.Right + spacingBtn;
            btnDisconnect.Left = btnConnect.Right + spacingBtn;

            panelButtons.Controls.AddRange(new Control[]
            {
            btnDiscover,
            btnConnect,
            btnDisconnect
            });


            // 3) Texto do chat e demais controles continuam como antes
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

        private void ResetUI()
        {
            Invoke((MethodInvoker)(() =>
            {
                this.Text = "Chat TCP Cliente - Desconectado";

                // reativa inputs
                txtNickname.Enabled = true;
                txtServerIp.Enabled = true;
                numServerPort.Enabled = true;
                btnConnect.Enabled = true;
                btnDiscover.Enabled = true;

                // desativa ações de chat
                btnDisconnect.Enabled = false;  // ← desabilita Desconectar
                btnListar.Enabled = false;
                lstUsuarios.Enabled = false;
                btnBroadcast.Enabled = false;
                txtBroadcast.Enabled = false;
                btnPrivado.Enabled = false;

                // limpa conteúdo
                txtMensagens.Clear();
                lstUsuarios.Items.Clear();
            }));
        }

        private void BtnDiscover_Click(object sender, EventArgs e)
        {
            string servidor = DiscoverServer(3000);
            if (!string.IsNullOrEmpty(servidor))
                txtServerIp.Text = servidor;
            else
                MessageBox.Show("Nenhuma resposta do servidor.", "Busca de Servidor", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        }

        private void BtnDisconnect_Click(object sender, EventArgs e)
        {
            // fecha streams e sockets
            try
            {
                stream?.Close();
                cliente?.Close();
                servidorPrivado?.Stop();
            }
            catch { /* ignora */ }

            //MessageBox.Show("Desconectado do servidor.");

            // reseta UI para estado inicial
            ResetUI();
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

                this.Text = $"Chat TCP Cliente - {apelido} - Conectado";
                btnListar.Enabled = true;
                lstUsuarios.Enabled = true;
                btnBroadcast.Enabled = true;
                txtBroadcast.Enabled = true;
                btnPrivado.Enabled = true;
                btnConnect.Enabled = false;
                btnDisconnect.Enabled = true;
                btnDiscover.Enabled = false;
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
                ResetUI();
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


        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.Run(new ClienteChat());
        }
    }
}
