using System;
using System.Collections.Generic;
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

            // 1) Resolve o IPv4 da interface Wi-Fi operante
            //string ipWifi = NetworkInterface.GetAllNetworkInterfaces()
            //    .Where(nic =>
            //        nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 &&
            //        nic.OperationalStatus == OperationalStatus.Up)
            //    .SelectMany(nic =>
            //        nic.GetIPProperties()
            //           .UnicastAddresses
            //           .Where(u => u.Address.AddressFamily == AddressFamily.InterNetwork))
            //    .Select(u => u.Address.ToString())
            //    .FirstOrDefault()
            //    ?? "127.0.0.1"; // fallback caso não encontre Wi-Fi

            string interfaceAlvo = "enp2s0";

            string ipWifi = Dns.GetHostEntry(Dns.GetHostName())
                .AddressList
                .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                ?.ToString() ?? "127.0.0.1";


            StartDiscoveryResponder(ipWifi);
            
            // 2) Use esse ipWifi no broadcast UDP
            Thread udpBroadcastThread = new(() =>
            {
                using var udp = new UdpClient();
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

            Console.WriteLine("Servidor Online");
            Console.WriteLine($"Ouvindo Chat na porta: {portaChat}");
            Console.WriteLine($"Ouvindo API na porta: {portaApi}");
            Console.WriteLine($"IP do Servidor: {ipWifi}");
            Console.WriteLine("Listener UDP de Discovery ativo na porta 30001.");
            Console.WriteLine("Broadcast UDP iniciado para descoberta de IP do servidor.");
            Console.WriteLine("Aguardando conexões...");

            Thread tChat = new(() => AceitarConexoes(listenerChat));
            Thread tApi  = new(() => AceitarConexoes(listenerApi));

            tChat.Start();
            tApi.Start();

            tChat.Join();
            tApi.Join();
        }


        static void StartDiscoveryResponder(string serverIp)
        {
            var udp = new UdpClient(AddressFamily.InterNetwork);
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, 30001));

            Thread responder = new(() =>
            {
                var remoteEP = new IPEndPoint(IPAddress.Any, 0);
                while (true)
                {
                    try
                    {
                        byte[] request = udp.Receive(ref remoteEP);
                        if (Encoding.UTF8.GetString(request) == "DISCOVER_SERVER")
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Discovery request de {remoteEP.Address}. Respondendo com {serverIp}");
                            byte[] reply = Encoding.UTF8.GetBytes(serverIp);
                            udp.Send(reply, reply.Length, remoteEP);
                        }
                    }
                    catch { /* log ou retry */ }
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

                Thread thread = new(() => AtenderCliente(cliente));
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

                    if (mensagem == "/count")
                    {
                        int total;
                        lock (locker)
                            total = clientes.Count;

                        byte[] resposta = Encoding.UTF8.GetBytes($"Usuarios Conectados: {total}");
                        stream.Write(resposta, 0, resposta.Length);
                    }
                    else if (mensagem == "/lista")
                    {
                        StringBuilder sb = new();
                        lock (locker)
                        {
                            foreach (var c in clientes)
                                sb.AppendLine($"{c.apelido};{c.ip};{c.portaPrivada}");
                        }

                        byte[] resposta = Encoding.UTF8.GetBytes(sb.ToString());
                        stream.Write(resposta, 0, resposta.Length);
                    }
                    else if (mensagem.StartsWith("/desconectar "))
                    {
                        string apelidoParaDesconectar = mensagem.Substring(13).Trim();

                        TcpClient clienteParaRemover = null;
                        lock (locker)
                        {
                            foreach (var c in clientes)
                            {
                                if (c.apelido.Equals(apelidoParaDesconectar, StringComparison.OrdinalIgnoreCase))
                                {
                                    clienteParaRemover = c.cliente;
                                    break;
                                }
                            }
                            if (clienteParaRemover != null)
                                clientes.RemoveAll(c => c.cliente == clienteParaRemover);
                        }

                        if (clienteParaRemover != null)
                        {
                            clienteParaRemover.Close();
                            byte[] resposta = Encoding.UTF8.GetBytes($"Usuário {apelidoParaDesconectar} desconectado com sucesso.");
                            stream.Write(resposta, 0, resposta.Length);
                            Console.WriteLine($"Usuário {apelidoParaDesconectar} desconectado via comando.");
                        }
                        else
                        {
                            byte[] resposta = Encoding.UTF8.GetBytes($"Usuário {apelidoParaDesconectar} não encontrado.");
                            stream.Write(resposta, 0, resposta.Length);
                        }
                    }
                    else if (mensagem == "/status")
                    {
                        int total;
                        lock (locker)
                            total = clientes.Count;

                        string status = $"Servidor online - Usuários conectados: {total} - Tempo uptime: {DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime}";
                        byte[] resposta = Encoding.UTF8.GetBytes(status);
                        stream.Write(resposta, 0, resposta.Length);
                    }
                    else
                    {
                        Console.WriteLine($"Mensagem recebida: {mensagem}");
                        Broadcast(mensagem, cliente);
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
                    var clienteRemover = clientes.Find(c => c.cliente == cliente);
                    if (clienteRemover.cliente != null)
                        clientes.Remove(clienteRemover);
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
                foreach (var c in clientes)
                {
                    if (c.cliente != remetente)
                    {
                        try
                        {
                            NetworkStream stream = c.cliente.GetStream();
                            stream.Write(dados, 0, dados.Length);
                        }
                        catch
                        {
                            // Ignorar cliente desconectado
                        }
                    }
                }
            }
        }
    }
}


