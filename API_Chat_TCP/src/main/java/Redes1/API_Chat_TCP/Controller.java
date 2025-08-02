package Redes1.API_Chat_TCP;

import jakarta.annotation.PostConstruct;
import org.springframework.http.HttpStatus;
import org.springframework.web.bind.annotation.RestController;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;

import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.InetSocketAddress;
import java.net.Socket;
import java.nio.charset.StandardCharsets;
import java.util.List;
import java.util.concurrent.CopyOnWriteArrayList;

@RestController
@RequestMapping("/api")
public class Controller {

    private static final String HOST = "127.0.0.1";
    private static final int PORTA_SERVIDOR = 2998;
    private static final String APELIDO_API = "API_Chat_TCP";

    @GetMapping("/usuarios/listar")
    public ResponseEntity<String> listarUsuarios() {
        return executarComando("/lista");
    }

    @GetMapping("/usuarios/count")
    public ResponseEntity<String> contarUsuarios() {
        return executarComando("/count");
    }

    private ResponseEntity<String> executarComando(String comando) {
        try (Socket socket = new Socket(HOST, PORTA_SERVIDOR);
             OutputStream out = socket.getOutputStream();
             InputStream in  = socket.getInputStream()) {

            // 1. Handshake
            String handshake = APELIDO_API + ";" + socket.getLocalPort();
            out.write(handshake.getBytes(StandardCharsets.UTF_8));
            out.flush();
            Thread.sleep(100);

            // 2. Envio do comando
            out.write(comando.getBytes(StandardCharsets.UTF_8));
            out.flush();

            // 3. Leitura robusta de toda a resposta
            StringBuilder resposta = new StringBuilder();
            byte[] buffer = new byte[4096];
            int lidos;
            while ((lidos = in.read(buffer)) != -1) {
                resposta.append(new String(buffer, 0, lidos, StandardCharsets.UTF_8));
                // Se leu menos que o buffer, provavelmente terminou
                if (lidos < buffer.length) break;
            }

            return ResponseEntity.ok(resposta.toString());

        } catch (IOException | InterruptedException e) {
            return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR)
                    .body("Erro ao executar comando " + comando + ": " + e.getMessage());
        }
    }


    @PostMapping("/enviar")
    public ResponseEntity<String> enviarMensagemBroadcast(@RequestBody ChatDTO dto) {
        String apelido = "API_Chat_TCP";
        int portaPrivada = PORTA_SERVIDOR; // Valor arbitrário fixo
        String dadosConexao = apelido + ";" + portaPrivada;

        try (Socket socket = new Socket(HOST, PORTA_SERVIDOR)) {
            OutputStream out = socket.getOutputStream();

            // 1. Enviar conexão inicial
            out.write(dadosConexao.getBytes(StandardCharsets.UTF_8));
            out.flush();
            Thread.sleep(100); // Dá tempo do servidor processar o registro

            // 2. Enviar mensagem real como broadcast
            String mensagem = "[Broadcast] " + apelido + ": " + dto.mensagem();
            out.write(mensagem.getBytes(StandardCharsets.UTF_8));
            out.flush();

            return ResponseEntity.ok("Mensagem enviada com sucesso.");
        } catch (IOException | InterruptedException e) {
            return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR)
                    .body("Erro ao enviar mensagem: " + e.getMessage());
        }
    }

    @GetMapping("/enviar")
    public ResponseEntity<String> enviarMensagemBroadcastViaUrl(@RequestParam String mensagem) {
        String apelido = "API_Chat_TCP";
        int portaPrivada = PORTA_SERVIDOR; // Valor arbitrário fixo
        String dadosConexao = apelido + ";" + portaPrivada;

        try (Socket socket = new Socket(HOST, PORTA_SERVIDOR)) {
            OutputStream out = socket.getOutputStream();

            // 1. Enviar conexão inicial
            out.write(dadosConexao.getBytes(StandardCharsets.UTF_8));
            out.flush();
            Thread.sleep(100); // Dá tempo do servidor processar o registro

            // 2. Enviar mensagem real como broadcast
            String conteudo = "[Broadcast] " + apelido + ": " + mensagem;
            out.write(conteudo.getBytes(StandardCharsets.UTF_8));
            out.flush();

            return ResponseEntity.ok("Mensagem enviada com sucesso.");

        } catch (IOException | InterruptedException e) {
            return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR)
                    .body("Erro ao enviar mensagem: " + e.getMessage());
        }
    }

    @GetMapping("/status")
    public ResponseEntity<String> pingServidor() {
        String apelido       = "API_Chat_TCP";
        int portaPrivada     = PORTA_SERVIDOR;
        String handshake     = apelido + ";" + portaPrivada;

        try (Socket socket = new Socket(HOST, PORTA_SERVIDOR);
             OutputStream out = socket.getOutputStream();
             InputStream in  = socket.getInputStream()) {

            // 1. Enviar handshake no formato esperado pelo servidor
            out.write(handshake.getBytes(StandardCharsets.UTF_8));
            out.flush();
            Thread.sleep(100); // aguardar registro

            // 2. Enviar comando de status
            out.write("/status".getBytes(StandardCharsets.UTF_8));
            out.flush();

            // 3. Ler resposta completa
            byte[] buffer = new byte[4096];
            int lidos = in.read(buffer);
            String resposta = new String(buffer, 0, lidos, StandardCharsets.UTF_8);

            return ResponseEntity.ok(resposta);
        } catch (IOException | InterruptedException e) {
            return ResponseEntity
                    .status(HttpStatus.SERVICE_UNAVAILABLE)
                    .body("Erro ao consultar status: " + e.getMessage());
        }
    }

    @GetMapping("/desconectar")
    public ResponseEntity<String> desconectarUsuario(@RequestParam String apelido) {
        String apelidoApi   = "API_Chat_TCP";
        int portaPrivada    = PORTA_SERVIDOR;
        String handshake    = apelidoApi + ";" + portaPrivada;

        try (Socket socket = new Socket(HOST, PORTA_SERVIDOR);
             OutputStream out = socket.getOutputStream();
             InputStream in  = socket.getInputStream()) {

            // 1) Handshake
            out.write(handshake.getBytes(StandardCharsets.UTF_8));
            out.flush();
            Thread.sleep(100);

            // 2) Envia comando de desconexão
            String comando = "/desconectar " + apelido;
            out.write(comando.getBytes(StandardCharsets.UTF_8));
            out.flush();

            // 3) Leitura da resposta
            byte[] buffer = new byte[4096];
            int lidos = in.read(buffer);
            String resposta = new String(buffer, 0, lidos, StandardCharsets.UTF_8);

            return ResponseEntity.ok(resposta);

        } catch (IOException | InterruptedException e) {
            return ResponseEntity
                    .status(HttpStatus.INTERNAL_SERVER_ERROR)
                    .body("Erro ao desconectar usuário: " + e.getMessage());
        }
    }


//    @GetMapping("/usuarios/listar")
//    public ResponseEntity<String> listarUsuarios() {
//        try (Socket socket = new Socket("127.0.0.1", 2998)) {
//            OutputStream out = socket.getOutputStream();
//            InputStream in = socket.getInputStream();
//
//            // Envia comando para listar usuários
//            out.write("/lista".getBytes(StandardCharsets.UTF_8));
//            out.flush();
//
//            byte[] buffer = new byte[4096];
//            int lidos = in.read(buffer);
//
//            String resposta = new String(buffer, 0, lidos, StandardCharsets.UTF_8);
//            return ResponseEntity.ok(resposta);
//        } catch (IOException e) {
//            return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR)
//                    .body("Erro ao listar usuários: " + e.getMessage());
//        }
//    }
//
//    @GetMapping("/usuarios/count")
//    public ResponseEntity<String> contarUsuarios() {
//        try (Socket socket = new Socket("127.0.0.1", 2998)) {
//            OutputStream out = socket.getOutputStream();
//            InputStream in = socket.getInputStream();
//
//            // Envia comando para contar usuários
//            out.write("/count".getBytes(StandardCharsets.UTF_8));
//            out.flush();
//
//            byte[] buffer = new byte[1024];
//            int lidos = in.read(buffer);
//
//            String resposta = new String(buffer, 0, lidos, StandardCharsets.UTF_8);
//            return ResponseEntity.ok(resposta);
//        } catch (IOException e) {
//            return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR)
//                    .body("Erro ao contar usuários: " + e.getMessage());
//        }
//    }

}

