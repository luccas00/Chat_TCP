using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Net;

namespace Chat_TCP
{
    public class JanelaChatPrivado : Form
    {
        TextBox txtMensagens, txtEntrada;
        Button btnEnviar;
        TcpClient cliente;
        NetworkStream stream;
        Thread threadReceber;

        string apelidoLocal;
        string apelidoDestino;

        // Construtor usado para iniciar chat privado (envia conexão)
        public JanelaChatPrivado(string apelidoLocal, string apelidoDestino, string ipDestino, int portaDestino)
        {
            this.apelidoLocal = apelidoLocal;
            this.apelidoDestino = apelidoDestino;

            // Configura UI comum
            InitializeUI();
            Text = $"Chat Privado - {apelidoLocal} -> {apelidoDestino} ({ipDestino}:{portaDestino})";

            // Estabelece conexão de saída
            cliente = new TcpClient();
            cliente.Connect(ipDestino, portaDestino);
            stream = cliente.GetStream();

            // Envia apelido local no handshake
            var apelidoBytes = Encoding.UTF8.GetBytes(apelidoLocal);
            stream.Write(apelidoBytes, 0, apelidoBytes.Length);

            // Inicia thread de recebimento
            threadReceber = new Thread(ReceberMensagens) { IsBackground = true };
            threadReceber.Start();

            btnEnviar.Click += (s, e) => EnviarMensagem();
        }

        // Construtor usado para conexões recebidas (aceitas)
        public JanelaChatPrivado(string apelidoLocal, TcpClient clienteExistente)
        {
            this.apelidoLocal = apelidoLocal;
            this.cliente = clienteExistente;
            this.stream = cliente.GetStream();

            // Lê apelido de quem se conectou
            byte[] buffer = new byte[1024];
            int lidos = stream.Read(buffer, 0, buffer.Length);
            this.apelidoDestino = Encoding.UTF8.GetString(buffer, 0, lidos);

            // Configura UI comum
            InitializeUI();
            var ep = (IPEndPoint)cliente.Client.RemoteEndPoint;
            Text = $"Chat Privado - {apelidoLocal} -> {apelidoDestino} ({ep.Address}:{ep.Port})";

            // Inicia thread de recebimento
            threadReceber = new Thread(ReceberMensagens) { IsBackground = true };
            threadReceber.Start();

            btnEnviar.Click += (s, e) => EnviarMensagem();
        }

        private void InitializeUI()
        {
            Width = 400; Height = 300;
            txtMensagens = new TextBox { Multiline = true, Dock = DockStyle.Top, Height = 200, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
            txtEntrada = new TextBox { Dock = DockStyle.Fill };
            btnEnviar = new Button { Text = "Enviar", Dock = DockStyle.Right, Width = 75 };
            var painelInferior = new Panel { Dock = DockStyle.Bottom, Height = 30 };
            painelInferior.Controls.Add(txtEntrada); painelInferior.Controls.Add(btnEnviar);
            Controls.Add(txtMensagens); Controls.Add(painelInferior);
        }

        void EnviarMensagem()
        {
            string msg = txtEntrada.Text.Trim();
            if (msg == "") return;
            string conteudo = $"{apelidoLocal}: {msg}";
            byte[] dados = Encoding.UTF8.GetBytes(conteudo);
            stream.Write(dados, 0, dados.Length);
            txtMensagens.AppendText("Eu: " + msg + Environment.NewLine);
            txtEntrada.Clear();
        }

        void ReceberMensagens()
        {
            try
            {
                byte[] buffer = new byte[1024];
                while (true)
                {
                    int lidos = stream.Read(buffer, 0, buffer.Length);
                    if (lidos == 0) break;
                    string msg = Encoding.UTF8.GetString(buffer, 0, lidos);
                    Invoke((MethodInvoker)(() => txtMensagens.AppendText($"[Privado] {msg}{Environment.NewLine}")));
                }
            }
            catch { }
            finally { cliente?.Close(); }
        }
    }
}