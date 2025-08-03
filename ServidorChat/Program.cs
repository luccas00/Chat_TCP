using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Chat_TCP
{
    class Program
    {
        static TcpListener listenerChat;
        static TcpListener listenerApi;
        static List<(TcpClient cliente, string apelido, string ip, int portaPrivada)> clientes = new();
        static object locker = new();

        static void Main()
        {
            const int portaChat = 1998;
            const int portaApi = 2998;

            listenerChat = new TcpListener(IPAddress.Any, portaChat);
            listenerApi = new TcpListener(IPAddress.Any, portaApi);

            listenerChat.Start();
            listenerApi.Start();

            // 1) Resolve o IPv4 ativo (não loopback) da máquina
            string ipWifi = Dns.GetHostAddresses(Dns.GetHostName())
                .FirstOrDefault(a =>
                    a.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(a))
                ?.ToString()
                ?? throw new InvalidOperationException("Nenhum IPv4 ativo encontrado");

            // 2) Inicia listener UDP de discovery
            StartDiscoveryResponder(ipWifi);

            // 3) Thread de broadcast UDP, vinculando à NIC correta
            Thread udpBroadcastThread = new(() =>
            {
                using var udp = new UdpClient(new IPEndPoint(IPAddress.Parse(ipWifi), 0));
                udp.EnableBroadcast = true;
                var broadcastEP = new IPEndPoint(IPAddress.Broadcast, 30000);
                byte[] payload = Encoding.UTF8.GetBytes(ipWifi);

                while (true)
                {
                    udp.Send(payload, payload.Length, broadcastEP);
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Broadcast UDP periódico enviado: {ipWifi}");
                    Thread.Sleep(TimeSpan.FromSeconds(10));
                }
            })
            { IsBackground = true };
            udpBroadcastThread.Start();

            // 4) Informações iniciais
            Console.WriteLine("Servidor Online");
            Console.WriteLine($"Ouvindo Chat na porta: {portaChat}");
            Console.WriteLine($"Ouvindo API na porta: {portaApi}");
            Console.WriteLine($"IP do Servidor: {ipWifi}");
            Console.WriteLine("Listener UDP de Discovery ativo na porta 30001.");
            Console.WriteLine("Broadcast UDP iniciado para descoberta de IP do servidor.");
            Console.WriteLine("Aguardando conexões...");

            // 5) Threads para aceitar conexões TCP
            Thread tChat = new(() => AceitarConexoes(listenerChat)) { IsBackground = false };
            Thread tApi = new(() => AceitarConexoes(listenerApi)) { IsBackground = false };

            tChat.Start();
            tApi.Start();

            tChat.Join();
            tApi.Join();
        }

        static void StartDiscoveryResponder(string serverIp)
        {
            var udp = new UdpClient(30001); // Porta de discovery
            Thread responder = new(() =>
            {
                var remoteEP = new IPEndPoint(IPAddress.Any, 0);
                while (true)
                {
                    try
                    {
                        byte[] request = udp.Receive(ref remoteEP);
                        string msg = Encoding.UTF8.GetString(request);
                        if (msg == "DISCOVER_SERVER")
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Discovery request recebido de {remoteEP.Address}. Respondendo com {serverIp}");
                            byte[] reply = Encoding.UTF8.GetBytes(serverIp);
                            udp.Send(reply, reply.Length, remoteEP);
                        }
                    }
                    catch
                    {
                        // Falha no receive: log ou retry
                    }
                }
            })
            { IsBackground = true };
            responder.Start();
        }

        static void AceitarConexoes(TcpListener listener)
        {
            while (true)
            {
                TcpClient cliente = listener.AcceptTcpClient();
                NetworkStream stream = cliente.GetStream();
                byte[] buffer = new byte[1024];

                int bytesLidos = stream.Read(buffer, 0, buffer.Length);
                string dados = Encoding.UTF8.GetString(buffer, 0, bytesLidos);
                var partes = dados.Split(';');

                if (partes.Length != 2 || !int.TryParse(partes[1], out int portaPrivada))
                {
                    Console.WriteLine("Formato inválido recebido, desconectando cliente.");
                    cliente.Close();
                    continue;
                }

                string apelido = partes[0];
                string ipCliente = ((IPEndPoint)cliente.Client.RemoteEndPoint).Address.ToString();

                lock (locker)
                {
                    clientes.Add((cliente, apelido, ipCliente, portaPrivada));
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Novo usuário: {apelido} ({ipCliente}:{portaPrivada})");
                }

                Thread thread = new(() => AtenderCliente(cliente)) { IsBackground = true };
                thread.Start();
            }
        }

        static void AtenderCliente(TcpClient cliente)
        {
            try
            {
                NetworkStream stream = cliente.GetStream();
                byte[] buffer = new byte[1024];
                int bytesLidos;

                while ((bytesLidos = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    string mensagem = Encoding.UTF8.GetString(buffer, 0, bytesLidos).Trim();

                    switch (mensagem)
                    {
                        case "/count":
                            int totalCount;
                            lock (locker) totalCount = clientes.Count;
                            stream.Write(Encoding.UTF8.GetBytes($"Usuarios Conectados: {totalCount}"));
                            break;

                        case "/lista":
                            var sb = new StringBuilder();
                            lock (locker)
                                clientes.ForEach(c => sb.AppendLine($"{c.apelido};{c.ip};{c.portaPrivada}"));
                            stream.Write(Encoding.UTF8.GetBytes(sb.ToString()));
                            break;

                        default:
                            if (mensagem.StartsWith("/desconectar "))
                            {
                                var apelidoRem = mensagem["/desconectar ".Length..].Trim();
                                TcpClient toRemove = null;
                                lock (locker)
                                {
                                    toRemove = clientes
                                        .FirstOrDefault(c => c.apelido.Equals(apelidoRem, StringComparison.OrdinalIgnoreCase))
                                        .cliente;
                                    clientes.RemoveAll(c => c.cliente == toRemove);
                                }
                                if (toRemove != null)
                                {
                                    toRemove.Close();
                                    var resp = $"Usuário {apelidoRem} desconectado com sucesso.";
                                    stream.Write(Encoding.UTF8.GetBytes(resp));
                                    Console.WriteLine($"Usuário {apelidoRem} desconectado via comando.");
                                }
                                else
                                {
                                    stream.Write(Encoding.UTF8.GetBytes($"Usuário {apelidoRem} não encontrado."));
                                }
                            }
                            else if (mensagem == "/status")
                            {
                                int cnt;
                                lock (locker) cnt = clientes.Count;
                                var uptime = DateTime.Now - Process.GetCurrentProcess().StartTime;
                                var status = $"Servidor online - Usuários conectados: {cnt} - Uptime: {uptime}";
                                stream.Write(Encoding.UTF8.GetBytes(status));
                            }
                            else
                            {
                                Console.WriteLine($"Mensagem recebida: {mensagem}");
                                Broadcast(mensagem, cliente);
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro: {ex.Message}");
            }
            finally
            {
                lock (locker)
                {
                    var rem = clientes.Find(c => c.cliente == cliente);
                    if (rem.cliente != null)
                        clientes.Remove(rem);
                }
                cliente.Close();
                Console.WriteLine($"Cliente desconectado. Total atual: {clientes.Count}");
            }
        }

        static void Broadcast(string mensagem, TcpClient remetente)
        {
            byte[] dados = Encoding.UTF8.GetBytes(mensagem);
            lock (locker)
            {
                foreach (var c in clientes.Where(c => c.cliente != remetente))
                {
                    try
                    {
                        c.cliente.GetStream().Write(dados, 0, dados.Length);
                    }
                    catch
                    {
                        // Ignorar falhas individuais
                    }
                }
            }
        }
    }
}
