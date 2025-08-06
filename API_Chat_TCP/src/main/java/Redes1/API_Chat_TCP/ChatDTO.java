package Redes1.API_Chat_TCP;

public class ChatDTO {
    private String apelido;
    private String mensagem;

    public ChatDTO() {}
    public ChatDTO(String apelido, String mensagem) {
        this.apelido = apelido;
        this.mensagem = mensagem;
    }
    public String getApelido() { return apelido; }
    public String getMensagem() { return mensagem; }
    public void setApelido(String a) { this.apelido = a; }
    public void setMensagem(String m) { this.mensagem = m; }
}