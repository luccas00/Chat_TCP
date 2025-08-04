using Avalonia.Controls;
using Avalonia.Threading;
using System.Collections.Generic;
using System.Net;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace ClienteChatLinux.Views
{
    public partial class MainWindow : Window
    {
        private TcpClient client;
        private NetworkStream stream;
        private TcpListener privateServer;
        private Thread receiveThread;
        private Thread privateServerThread;
        private string nickname;
        private int privatePort;

        public MainWindow()
        {
            InitializeComponent();
            StartUdpDiscoveryListener();
            HookEvents();
        }

        private void HookEvents()
        {
            DiscoverButton.Click += OnDiscover;
            ConnectButton.Click += OnConnect;
            DisconnectButton.Click += OnDisconnect;
            ListUsersButton.Click += OnListUsers;
            BroadcastButton.Click += OnBroadcast;
            PrivateChatButton.Click += OnPrivateChat;
        }

        private void OnDiscover(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                var ip = DiscoverServer(3000);
                Dispatcher.UIThread.Post(() =>
                {
                    if (ip != null) ServerIpBox.Text = ip;
                    else MessagesBox.Text += "[Aviso] Nenhuma resposta do servidor.\n";
                });
            });
        }

        private void OnConnect(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NicknameBox.Text)) return;
            nickname = NicknameBox.Text.Trim();
            var ip = ServerIpBox.Text.Trim();
            var port = (int)PortBox.Value;

            try
            {
                // Servidor Privado TCP
                privateServer = new TcpListener(IPAddress.Any, 0);
                privateServer.Start();
                privatePort = ((IPEndPoint)privateServer.LocalEndpoint).Port;
                privateServerThread = new Thread(PrivateServerLoop) { IsBackground = true };
                privateServerThread.Start();

                // Conexão principal
                client = new TcpClient();
                client.Connect(ip, port);
                stream = client.GetStream();
                var dados = $"{nickname};{privatePort}";
                var bytes = Encoding.UTF8.GetBytes(dados);
                stream.Write(bytes, 0, bytes.Length);

                // Recebimento de dados
                receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
                receiveThread.Start();

                UpdateUIOnConnect();
            }
            catch (Exception ex)
            {
                MessagesBox.Text += $"[Erro] Falha ao conectar: {ex.Message}\n";
            }
        }

        private void OnDisconnect(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            try { stream?.Close(); client?.Close(); privateServer?.Stop(); } catch { }
            Dispatcher.UIThread.Post(ResetUI);
        }

        private void OnListUsers(object sender, Avalonia.Interactivity.RoutedEventArgs e)
            => SendCommand("/lista");

        private void OnBroadcast(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var text = BroadcastBox.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;
            var msg = $"[Broadcast] {nickname}: {text}";
            stream.Write(Encoding.UTF8.GetBytes(msg), 0, msg.Length);
            BroadcastBox.Text = "";
        }

        private void OnPrivateChat(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (UsersList.SelectedItem == null) return;
            var item = UsersList.SelectedItem.ToString();
            var parts = item.Substring(item.IndexOf('(') + 1).TrimEnd(')').Split(':');
            var ip = parts[0]; var port = int.Parse(parts[1]);
            string remoteNick = item.Substring(0, item.IndexOf('(')).Trim();
            var win = new PrivateChatWindow(nickname, remoteNick, ip, port);
            win.Show();

        }

        private string DiscoverServer(int timeoutMs)
        {
            using var udp = new UdpClient();
            udp.EnableBroadcast = true;
            udp.Client.ReceiveTimeout = timeoutMs;
            var ep = new IPEndPoint(IPAddress.Broadcast, 30001);
            var payload = Encoding.UTF8.GetBytes("DISCOVER_SERVER");
            udp.Send(payload, payload.Length, ep);
            try
            {
                var remote = new IPEndPoint(IPAddress.Any, 0);
                var data = udp.Receive(ref remote);
                return Encoding.UTF8.GetString(data);
            }
            catch { return null; }
        }

        private void StartUdpDiscoveryListener()
        {
            new Thread(() =>
            {
                using var udp = new UdpClient(30000);
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                var ep = new IPEndPoint(IPAddress.Any, 0);
                while (true)
                {
                    try
                    {
                        var data = udp.Receive(ref ep);
                        var ip = Encoding.UTF8.GetString(data);
                        Dispatcher.UIThread.Post(() => ServerIpBox.Text = ip);
                    }
                    catch { }
                }
            })
            { IsBackground = true }.Start();
        }

        private void PrivateServerLoop()
        {
            while (true)
            {
                try
                {
                    var tcp = privateServer.AcceptTcpClient();
                    Dispatcher.UIThread.Post(() =>
                    {
                        var win = new PrivateChatWindow(nickname, tcp);
                        win.Show();
                    });
                }
                catch { break; }
            }
        }

        private void ReceiveLoop()
        {
            var buf = new byte[1024];
            while (true)
            {
                try
                {
                    var count = stream.Read(buf, 0, buf.Length);
                    if (count == 0) break;
                    var msg = Encoding.UTF8.GetString(buf, 0, count);
                    if (msg.Contains(";") && msg.Contains("\n"))
                        Dispatcher.UIThread.Post(() => UpdateUserList(msg));
                    else
                        Dispatcher.UIThread.Post(() => MessagesBox.Text += msg + "\n");
                }
                catch { break; }
            }
            Dispatcher.UIThread.Post(ResetUI);
        }

        private void SendCommand(string cmd)
            => stream?.Write(Encoding.UTF8.GetBytes(cmd), 0, cmd.Length);

        private void UpdateUIOnConnect()
        {
            Title = $"Chat TCP - {nickname}";
            NicknameBox.IsEnabled = ServerIpBox.IsEnabled = PortBox.IsEnabled = DiscoverButton.IsEnabled = ConnectButton.IsEnabled = false;
            DisconnectButton.IsEnabled = ListUsersButton.IsEnabled = BroadcastButton.IsEnabled = PrivateChatButton.IsEnabled = BroadcastBox.IsEnabled = true;
        }

        private void ResetUI()
        {
            Title = "Chat TCP Cliente - Desconectado";
            NicknameBox.IsEnabled = ServerIpBox.IsEnabled = PortBox.IsEnabled = DiscoverButton.IsEnabled = ConnectButton.IsEnabled = true;
            DisconnectButton.IsEnabled = ListUsersButton.IsEnabled = BroadcastButton.IsEnabled = PrivateChatButton.IsEnabled = BroadcastBox.IsEnabled = false;
            MessagesBox.Text = "";
            UsersList.ItemsSource = null;
        }

        private void UpdateUserList(string data)
        {
            // Constrói a lista de strings
            List<string> lista = data
                .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line =>
                {
                    var c = line.Split(';');
                    return $"{c[0]} ({c[1]}:{c[2]})";
                })
                .ToList();

            // Atualiza ItemsSource, não Items
            UsersList.ItemsSource = lista;
        }

    }
}
