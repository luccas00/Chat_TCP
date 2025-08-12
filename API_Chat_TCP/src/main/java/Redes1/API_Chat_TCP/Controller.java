package Redes1.API_Chat_TCP;

import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.Socket;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.concurrent.CopyOnWriteArrayList;

import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.CrossOrigin;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

import jakarta.annotation.PostConstruct;

@CrossOrigin(origins = "*")
@RestController
@RequestMapping("/api")
public class Controller {

    //private static final String HOST = "192.168.1.176";
    private static final String HOST = "127.0.0.1";
    private static final int CHAT_PORT = 1998;
    private static final int ADMIN_PORT = 2998;
    private static final String APELIDO_API = "API_Chat_TCP";

    private final List<String> messages = new CopyOnWriteArrayList<>();

    @PostConstruct
    public void initChatListener() {
        Thread listener = new Thread(() -> {
            while(true) {
                try (Socket socket = new Socket(HOST, CHAT_PORT);
                     InputStream in = socket.getInputStream();
                     OutputStream out = socket.getOutputStream()) {

                    // Handshake to register API as client
                    String handshake = APELIDO_API + ";" + socket.getLocalPort();
                    out.write(handshake.getBytes(StandardCharsets.UTF_8));
                    out.flush();

                    byte[] buffer = new byte[1024];
                    int bytesRead;
                    // Read raw bytes for broadcast messages
                    while ((bytesRead = in.read(buffer)) > 0) {
                        String msg = new String(buffer, 0, bytesRead, StandardCharsets.UTF_8);
                        messages.add(msg);
                    }
                } catch (IOException e) {
                    // Connection lost or error: retry after delay
                    try { Thread.sleep(5000); } catch (InterruptedException ignore) {}
                }
            }
        });
        listener.setDaemon(true);
        listener.start();
    }

    @GetMapping("/messages")
    public ResponseEntity<List<String>> getMessages() {
        return ResponseEntity.ok(messages);
    }

    @GetMapping("/usuarios/count")
    public ResponseEntity<String> contarUsuarios() {
        return executarComando(ADMIN_PORT, "/count");
    }

    private ResponseEntity<String> executarComando(int port, String comando) {
        try (Socket socket = new Socket(HOST, port);
             OutputStream out = socket.getOutputStream();
             InputStream in = socket.getInputStream()) {

            String handshake = APELIDO_API + ";" + socket.getLocalPort();
            out.write(handshake.getBytes(StandardCharsets.UTF_8));
            out.flush();
            Thread.sleep(100);
            out.write(comando.getBytes(StandardCharsets.UTF_8));
            out.flush();

            StringBuilder sb = new StringBuilder();
            byte[] buffer = new byte[4096];
            int read;
            while ((read = in.read(buffer)) != -1) {
                sb.append(new String(buffer, 0, read, StandardCharsets.UTF_8));
                if (read < buffer.length) break;
            }
            return ResponseEntity.ok(sb.toString());
        } catch (IOException | InterruptedException e) {
            return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR)
                    .body("Erro ao executar comando " + comando + ": " + e.getMessage());
        }
    }


    @GetMapping("/usuarios/listar")
    public ResponseEntity<Object> listarUsuarios() {
        return executarComandoListaUsuarios(ADMIN_PORT, "/lista");
    }

    // Método específico para interpretar resposta de /lista como JSON
    private ResponseEntity<Object> executarComandoListaUsuarios(int port, String comando) {
        try (Socket socket = new Socket(HOST, port);
            OutputStream out = socket.getOutputStream();
            InputStream in = socket.getInputStream()) {

            String handshake = APELIDO_API + ";" + socket.getLocalPort();
            out.write(handshake.getBytes(StandardCharsets.UTF_8));
            out.flush();
            Thread.sleep(100);
            out.write(comando.getBytes(StandardCharsets.UTF_8));
            out.flush();

            StringBuilder sb = new StringBuilder();
            byte[] buffer = new byte[4096];
            int read;
            while ((read = in.read(buffer)) != -1) {
                sb.append(new String(buffer, 0, read, StandardCharsets.UTF_8));
                if (read < buffer.length) break;
            }

            // Exemplo de linha: apelido;ip;portaPrivada
            String[] linhas = sb.toString().split("\n");
            List<Map<String, Object>> usuarios = new ArrayList<>();

            for (String linha : linhas) {
                String[] partes = linha.trim().split(";");
                if (partes.length == 3) {
                    Map<String, Object> user = new HashMap<>();
                    user.put("apelido", partes[0]);
                    user.put("ip", partes[1]);
                    user.put("portaPrivada", Integer.parseInt(partes[2]));
                    usuarios.add(user);
                }
            }

            return ResponseEntity.ok(Map.of("usuarios", usuarios));

        } catch (IOException | InterruptedException e) {
            Map<String, Object> erro = new HashMap<>();
            erro.put("erro", "Erro ao executar comando " + comando);
            erro.put("mensagem", e.getMessage());
            return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR).body(erro);
        }
    }

    @PostMapping("/enviar")
    public ResponseEntity<String> enviarMensagemBroadcast(@RequestBody ChatDTO dto) {
        String conteudo = "[Broadcast] " + dto.getApelido() + ": " + dto.getMensagem();
        // Send via admin port as chat client
        return executarBroadcast(conteudo);
    }

    private ResponseEntity<String> executarBroadcast(String conteudo) {
        try (Socket socket = new Socket(HOST, CHAT_PORT);
             OutputStream out = socket.getOutputStream()) {

            String handshake = APELIDO_API + ";" + socket.getLocalPort();
            out.write(handshake.getBytes(StandardCharsets.UTF_8));
            out.flush();
            Thread.sleep(100);
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
        return executarComando(ADMIN_PORT, "/status");
    }

    @GetMapping("/desconectar")
    public ResponseEntity<String> desconectarUsuario(@RequestParam String apelido) {
        return executarComando(ADMIN_PORT, "/desconectar " + apelido);
    }
    

}

