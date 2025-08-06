import React, { useState, useEffect } from 'react';
import axios from 'axios';

//const API_BASE = 'http://localhost:8080/api';

// Use o IP da máquina host para acesso via celular
const API_BASE = 'http://192.168.1.176:8080/api';


export default function App() {
  const [nickname, setNickname] = useState('');
  const [connected, setConnected] = useState(false);
  const [messages, setMessages] = useState([]);
  const [text, setText] = useState('');

  useEffect(() => {
    let timer;
    if (connected) {
      fetchMessages();
      timer = setInterval(fetchMessages, 1000);
    }
    return () => clearInterval(timer);
  }, [connected]);

  const fetchMessages = async () => {
    try {
      const res = await axios.get(`${API_BASE}/messages`);
      setMessages(res.data);
    } catch (err) { console.error(err); }
  };

  const handleConnect = () => { if (!nickname.trim()) return; setConnected(true); };
  const handleSend = async () => { if (!text.trim()) return; await axios.post(`${API_BASE}/enviar`, { apelido: nickname, mensagem: text }); setText(''); };
  const handleDisconnect = () => { setConnected(false); setMessages([]); };

  return (
    <div style={{ maxWidth:400,margin:'auto',padding:20 }}>
      {!connected ? (
        <div style={{display:'flex',gap:8,marginBottom:16}}>
          <input placeholder="Apelido" value={nickname} onChange={e=>setNickname(e.target.value)} style={{flex:1,padding:8,fontSize:14}} />
          <button onClick={handleConnect} style={{padding:'8px 12px'}}>Conectar</button>
        </div>
      ):(
        <div style={{display:'flex',justifyContent:'space-between',marginBottom:16}}>
          <span>Conectado como: {nickname}</span>
          <button onClick={handleDisconnect} style={{padding:'8px 12px'}}>Desconectar</button>
        </div>
      )}
      {connected&&<>
        <div style={{border:'1px solid #ccc',height:300,overflowY:'auto',padding:8,marginBottom:16,whiteSpace:'pre-wrap',fontFamily:'monospace',fontSize:14}}>
          {messages.map((m,i)=><div key={i}>{m}</div>)}
        </div>
        <div style={{display:'flex',gap:8}}>
          <input placeholder="Mensagem" value={text} onChange={e=>setText(e.target.value)} style={{flex:1,padding:8,fontSize:14}} />
          <button onClick={handleSend} style={{padding:'8px 12px'}}>Enviar Broadcast</button>
        </div>
      </>}
    </div>
  );
}