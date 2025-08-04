using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ClienteChatLinux;

public partial class PrivateChatWindow : Window
{
    private TcpClient client;
    private NetworkStream stream;
    private Thread receiveThread;
    private readonly string localNick;
    private readonly string remoteNick;

    public PrivateChatWindow(string localNick, string remoteNick, string ip, int port)
    {
        InitializeComponent();
        this.localNick = localNick;
        this.remoteNick = remoteNick;
        Title = $"Privado: {localNick} → {remoteNick} ({ip}:{port})";

        // Conecta e faz handshake de apelido
        client = new TcpClient();
        client.Connect(ip, port);
        stream = client.GetStream();
        var nickBytes = Encoding.UTF8.GetBytes(localNick);
        stream.Write(nickBytes, 0, nickBytes.Length);

        HookEvents();
        StartReceiveLoop();
    }

    public PrivateChatWindow(string localNick, TcpClient existingClient)
    {
        InitializeComponent();
        this.localNick = localNick;
        client = existingClient;
        stream = client.GetStream();

        // Lê apelido do peer
        var buffer = new byte[1024];
        int read = stream.Read(buffer, 0, buffer.Length);
        remoteNick = Encoding.UTF8.GetString(buffer, 0, read);
        var ep = (IPEndPoint)client.Client.RemoteEndPoint;
        Title = $"Privado: {localNick} → {remoteNick} ({ep.Address}:{ep.Port})";

        HookEvents();
        StartReceiveLoop();
    }

    private void HookEvents()
    {
        SendButton.Click += OnSend;
    }

    private void OnSend(object sender, RoutedEventArgs e)
    {
        var text = InputBox.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;

        var msg = $"{localNick}: {text}";
        var data = Encoding.UTF8.GetBytes(msg);
        stream.Write(data, 0, data.Length);

        MessagesBox.Text += $"Eu: {text}\n";
        InputBox.Text = string.Empty;
    }

    private void StartReceiveLoop()
    {
        receiveThread = new Thread(() =>
        {
            var buf = new byte[1024];
            try
            {
                while (true)
                {
                    int bytes = stream.Read(buf, 0, buf.Length);
                    if (bytes == 0) break;
                    var msg = Encoding.UTF8.GetString(buf, 0, bytes);
                    Dispatcher.UIThread.Post(() =>
                        MessagesBox.Text += $"[Privado] {msg}\n");
                }
            }
            catch { /* ignore */ }
            finally
            {
                client?.Close();
            }
        })
        { IsBackground = true };
        receiveThread.Start();

    }
}