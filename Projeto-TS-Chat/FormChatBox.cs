﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.IO;
using EI.SI;

namespace Projeto_TS_Chat
{
    public partial class FormChatBox : Form
    {
        ProtocolSI protocolSI;
        private const int PORT = 10000;
        NetworkStream networkStream;
        TcpClient client;
        CspParameters cspp = new CspParameters();
        private RSACryptoServiceProvider rsa;

        string publickey;
        int bytesRead = 0;

        private const int SALTSIZE = 8;
        private const int NUMBER_OF_ITERATIONS = 1000;

        string publicKey;
        string chavePrivadaDecifrada;

        string username;

        private byte[] key;
        private byte[] iv;
        AesCryptoServiceProvider aes;

        public FormChatBox()
        {
            InitializeComponent();

            // Criar um cliente TcpCliente
            //Nota, para que este cliente funcione, é preciso ter um TcpServer
            //ligado ao mesmo endereço especificado pelo servidor e porta

            IPEndPoint endPoint = new IPEndPoint(IPAddress.Loopback, PORT);
            client = new TcpClient();
            client.Connect(endPoint);

            //Obter um fluxo de clientes para leitura e escrita
            networkStream = client.GetStream();

            // Preparação da comunicação utilizando a classe desenvolvida pelo DEI
            protocolSI = new ProtocolSI();

            ////inicializar o serviço AES
            aes = new AesCryptoServiceProvider();
            ////guardar a chave gerada
            key = aes.Key;
            ////guardar o vetor de inicialização IV
            iv = aes.IV;

            DisableChat();
            enviarReceberChaves();
        }

        /*      ELEMENTOS DO FORMULÁRIO
                                             */

        //BUTTON PARA ENVIAR MENSAGEM
        private void buttonEnviar_Click(object sender, EventArgs e)
        {
            // Preparar mensagem para ser enviada
            string msg = textBoxMessage.Text;
            textBoxMessage.Clear();
            messageChat.Items.Add(DateTime.Now.ToString("HH:mm:ss ") + username + ": "+msg);
            
            //preparar a mensagem para ser enviada
            byte[] packet = protocolSI.Make(ProtocolSICmdType.DATA, CifrarTexto(msg));

            //enviar mensagem
            networkStream.Write(packet, 0, packet.Length);
            bytesRead = networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);

            string msgRecebida = protocolSI.GetStringFromData();
            messageChat.Items.Add(DateTime.Now.ToString("HH:mm:ss") + " Server: " + DecifrarTexto(msgRecebida));
        }

        //BUTTON PARA ENVIAR O FICHEIRO
        private void buttonUpFile_Click(object sender, EventArgs e)
        {
            openFileDialog1.InitialDirectory = @"c:\";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                string fName = openFileDialog1.FileName;
                EncryptFile(fName);

                int outFile = fName.LastIndexOf("\\") + 1;
                string nomeFicheiro = fName.Substring(outFile, fName.LastIndexOf("") + 1 - outFile);
                nomeFicheiro = nomeFicheiro + ".enc";
                //envia o nome do ficheiro para o servidor
                byte[] packet = protocolSI.Make(ProtocolSICmdType.USER_OPTION_4, CifrarTexto(nomeFicheiro));

                //enviar mensagem
                networkStream.Write(packet, 0, packet.Length);
                bytesRead = networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);

                MemoryStream destination = new MemoryStream();
                FileStream fs = new FileStream(nomeFicheiro, FileMode.Open);
                fs.CopyTo(destination);

                //preparar a mensagem para ser enviada
                packet = protocolSI.Make(ProtocolSICmdType.USER_OPTION_3, destination.ToArray());

                //enviar mensagem
                networkStream.Write(packet, 0, packet.Length);
                bytesRead = networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);

                string msgRecebida = protocolSI.GetStringFromData();
                messageChat.Items.Add(DateTime.Now.ToString("HH:mm:ss") + " Server: " + DecifrarTexto(msgRecebida));
            }
        }

        //BUTTON PARA REGISTAR
        private void buttonRegistar_Click(object sender, EventArgs e)
        {
            String pass = textBoxPwRegistar.Text;
            String username = textBoxUserRegistar.Text;
            byte[] salt = GenerateSalt(SALTSIZE);
            byte[] hash = GenerateSaltedHash(pass, salt);

            Register(username, hash, salt, publicKey);
        }

        //BUTTON PARA FAZER O LOGIN
        private void buttonLogin_Click(object sender, EventArgs e)
        {
            String pass = textBoxPwLogin.Text;
            String username = textBoxUserLogin.Text;

            Login(username, pass);
        }

        //BUTTON PARA SAIR DO PROGRAMA
        private void buttonSair_Click(object sender, EventArgs e)
        {
            //armazena todas as mensagens trocadas entre cliente e servidor do lado do cliente
            System.IO.File.WriteAllLines("log.txt", messageChat.Items.Cast<string>().ToArray());

            CloseClient();
            this.Close();
        }

        /*          FUNÇÕES DO PROGRAMA
                                             */

        //FUNÇÃO ENVIAR E RECEBER CHAVES NO INICIO
        private void enviarReceberChaves()
        {
            publicKey = GerarChavePublica();

            byte[] packet = protocolSI.Make(ProtocolSICmdType.PUBLIC_KEY, publicKey);
            networkStream.Write(packet, 0, packet.Length);

            //receber chave privada
            networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);

            string chavePrivadaCifrada;
            chavePrivadaCifrada = protocolSI.GetStringFromData();

            //INICIALIZAR SERVIÇO DE CIFRAGEM AES
            aes = new AesCryptoServiceProvider();
            //GUARDAR A CHAVE GERADA
            key = aes.Key;
            //GUARDAR O VETOR DE INICIALIZAÇÃO GERADO
            iv = aes.IV;
            //IR BUSCAR CHAVE E IV 
            string keyB64 = GerarChavePrivada(publicKey);
            string ivB64 = GerarIv(publicKey);

            //CONVERTER DE BASE64 PARA BYTES E SUBSTITUIR NO AES
            aes.Key = Convert.FromBase64String(keyB64);
            aes.IV = Convert.FromBase64String(ivB64);
            //IR BUSCAR O TEXTO DA TEXTBOX TEXTOCIFRADO (BASE64)
            //string textoCifrado = tb_TextoCifrado.Text;
            //CHAMAR A FUNÇÃO DECIFRARTEXTO E ENVIAR TEXTO GUARDADO ANTES E GUARDÁ-LO NA VARÍAVEL TEXTODECIFRADO
            chavePrivadaDecifrada = DecifrarTexto(chavePrivadaCifrada);

            Console.WriteLine(chavePrivadaDecifrada);
        }

        //FUNÇÃO PARA DESLIGAR O CLIENT
        private void CloseClient()
        {
            // Preparar envio da mensagem para desligar a ligação
            byte[] eot = protocolSI.Make(ProtocolSICmdType.EOT);
            networkStream.Write(eot, 0, eot.Length);
            networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
            networkStream.Close();
            client.Close();
        }

        //FUNÇÃO PARA GERAR CHAVE PUBLICA
        private string GerarChavePublica()
        {
            rsa = new RSACryptoServiceProvider();
            //CRIAR E DEVOLVER UMA STRING QUE CONTÉM A CHAVE PÚBLICA
            publickey = rsa.ToXmlString(false);
            //CRIAR E DEVOLVER UMA STRING COM CHAVE PÚBLICA E PRIVADA
            string bothkeys = rsa.ToXmlString(true);
            return publickey;
        }

        //FUNÇÃO PARA GERAR UMA CHAVE SIMÉTRICA A PARTIR DE UMA STRING
        private string GerarChavePrivada(string pass)
        {
            byte[] salt = new byte[] { 0, 1, 0, 8, 2, 9, 9, 7 };
            Rfc2898DeriveBytes pwdGen = new Rfc2898DeriveBytes(pass, salt, 1000);
            //GERAR KEY
            byte[] key = pwdGen.GetBytes(16);
            //CONVERTER A PASS PARA BASE64
            string passB64 = Convert.ToBase64String(key);
            //DEVOLVER A PASS EM BYTES
            return passB64;
        }

        //FUNÇÃO GERAR UM VETOR DE INICIALIZAÇÃO A PARTIR DE UMA STRING
        private string GerarIv(string pass)
        {
            byte[] salt = new byte[] { 7, 8, 7, 8, 2, 5, 9, 5 };
            Rfc2898DeriveBytes pwdGen = new Rfc2898DeriveBytes(pass, salt, 1000);
            //GERAR UMA KEY
            byte[] iv = pwdGen.GetBytes(16);
            //CONVERTER PARA BASE64
            string ivB64 = Convert.ToBase64String(iv);
            //DEVOLVER EM BYTES
            return ivB64;
        }

        //FUNÇÃO PARA CIFRAR O TEXTO
        private string CifrarTexto(string txt)
        {
            //VARIÁVEL PARA GUARDAR O TEXTO DECIFRADO EM BYTES
            byte[] txtDecifrado = Encoding.UTF8.GetBytes(txt);
            //VARIÁVEL PARA GUARDAR O TEXTO CIFRADO EM BYTES
            byte[] txtCifrado;
            //RESERVAR ESPAÇO NA MEMÓRIA PARA COLOCAR O TEXTO E CIFRÁ-LO
            MemoryStream ms = new MemoryStream();
            //INICIALIZAR O SISTEMA DE CIFRAGEM (WRITE)
            CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);
            //CRIFRAR OS DADOS
            cs.Write(txtDecifrado, 0, txtDecifrado.Length);
            cs.Close();
            //GUARDAR OS DADOS CRIFRADO QUE ESTÃO NA MEMÓRIA
            txtCifrado = ms.ToArray();
            //CONVERTER OS BYTES PARA BASE64 (TEXTO)
            string txtCifradoB64 = Convert.ToBase64String(txtCifrado);
            //DEVOLVER OS BYTES CRIADOS EM BASE64
            return txtCifradoB64;
        }

        //FUNÇÃO PARA DECIFRAR O TEXTO
        private string DecifrarTexto(string txtCifradoB64)
        {
            //VARIÁVEL PARA GUARDAR O TEXTO CIFRADO EM BYTES
            byte[] txtCifrado = Convert.FromBase64String(txtCifradoB64);
            //RESERVAR ESPAÇO NA MEMÓRIA PARA COLOCAR O TEXTO E CIFRÁ-LO
            MemoryStream ms = new MemoryStream(txtCifrado);
            //INICIALIZAR O SISTEMA DE CIFRAGEM (READ)
            CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);

            //VARIÁVEL PARA GUARDO O TEXTO DECIFRADO
            byte[] txtDecifrado = new byte[ms.Length];
            //VARIÁVEL PARA TER O NÚMERO DE BYTES DECIFRADOS
            int bytesLidos = 0;
            //DECIFRAR OS DADOS
            bytesLidos = cs.Read(txtDecifrado, 0, txtDecifrado.Length);
            cs.Close();
            //CONVERTER PARA TEXTO
            string textoDecifrado = Encoding.UTF8.GetString(txtDecifrado, 0, bytesLidos);
            //DEVOLVER TEXTO DECRIFRADO
            return textoDecifrado;
        }

        //FUNÇÃO PARA CIFRAR FICHEIROS
        private void EncryptFile(string inFile)
        {

            // Cria uma instância do AES para
            // cifragem simétrica dos dados
            Aes aes = Aes.Create();
            ICryptoTransform transform = aes.CreateEncryptor();

            // Utiliza o RSACryptoServiceProvider para
            // cifrar a chave AES.

            byte[] keyEncrypted = rsa.Encrypt(aes.Key, false);

            // Cria um array de bytes que contem
            // o tamanho dos valores da chave e do IV
            byte[] LenK = new byte[4];
            byte[] LenIV = new byte[4];

            int lKey = keyEncrypted.Length;
            LenK = BitConverter.GetBytes(lKey);
            int lIV = aes.IV.Length;
            LenIV = BitConverter.GetBytes(lIV);

            // Escreve os seguintes para o filestream
            // para o ficheiro cifrado (outFs):
            // - tamanho da chave
            // - tamanho do IV
            // - chave cifrada
            // - o IV
            // - o conteudo cifrado

            int startFileName = inFile.LastIndexOf("\\") + 1;
            // Muda a extensão do ficheiro para ".enc"
            string outFile = inFile.Substring(startFileName, inFile.LastIndexOf("") + 1 - startFileName) + ".enc";

            using (FileStream outFs = new FileStream(outFile, FileMode.Create))
            {

                outFs.Write(LenK, 0, 4);
                outFs.Write(LenIV, 0, 4);
                outFs.Write(keyEncrypted, 0, lKey);
                outFs.Write(aes.IV, 0, lIV);

                // Agora escreve o conteudo do ficheiro cifrado utilizando
                // a CryptoStream para cifragem.
                using (CryptoStream outStreamEncrypted = new CryptoStream(outFs, transform, CryptoStreamMode.Write))
                {

                    // Ao cifrar um bloco de cada vez
                    // dá para poupar memória do sistema
                    // e acomodar ficheiros maiores
                    int count = 0;
                    int offset = 0;

                    // blockSizeBytes pode ser qualquer valor arbitrário
                    int blockSizeBytes = aes.BlockSize / 8;
                    byte[] data = new byte[blockSizeBytes];
                    int bytesRead = 0;

                    using (FileStream inFs = new FileStream(inFile, FileMode.Open))
                    {
                        do
                        {
                            count = inFs.Read(data, 0, blockSizeBytes);
                            offset += count;
                            outStreamEncrypted.Write(data, 0, count);
                            bytesRead += blockSizeBytes;
                        }
                        while (count > 0);
                        inFs.Close();
                    }
                    outStreamEncrypted.FlushFinalBlock();
                    outStreamEncrypted.Close();
                }
                outFs.Close();
            }
        }

        //FUNÇÃO DE LOGIN
        private void Login(string user, string password)
        {
            //Login opçao SI USER_OPTION_1

            //mensagem a enviar
            string msg = user + '|' + password;

            //pacote a enviar pelo protocolo SI
            byte[] packet = protocolSI.Make(ProtocolSICmdType.USER_OPTION_1, CifrarTexto(msg));

            // Enviar mensagem
            networkStream.Write(packet, 0, packet.Length);
            //recebe confirmaçao do servidor que o login foi um sucesso!
            networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
            if (protocolSI.GetCmdType() == ProtocolSICmdType.ACK)
            {
                EnableChat();
                username = textBoxUserLogin.Text;
                textBoxUserLogin.Clear();
                textBoxPwLogin.Clear();
                MessageBox.Show("Seja Bem-vindo " + username + "!", "Login efetuado com sucesso!", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("O seu username ou password estão incorretos ou não existem", "Erro de autenticação!", MessageBoxButtons.OK, MessageBoxIcon.Error);

            }
        }

        //FUNÇÃO DE REGISTO
        private void Register(string user, byte[] passHash, byte[] passSalt, string publicKey)
        {
            //registar opçao SI USER_OPTION_2

            //converter encriptaçao para string para ser enviado
            string passwordHash = Convert.ToBase64String(passHash);
            string passwordSalt = Convert.ToBase64String(passSalt);

            //mensagem a enviar
            string msg = user + '|' + passwordHash + '|' + passwordSalt + '|' + publicKey;

            //pacote a enviar pelo protocolo SI
            byte[] packet = protocolSI.Make(ProtocolSICmdType.USER_OPTION_2, CifrarTexto(msg));

            // Enviar mensagem
            networkStream.Write(packet, 0, packet.Length);
            while (protocolSI.GetCmdType() != ProtocolSICmdType.ACK)
            {
                networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
            }

            MessageBox.Show("Seja Bem-vindo " + user + "! Pode agora fazer login com a sua conta!", "Registo efetuado com sucesso!", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        //FUNÇÃO PARA GERAR O SALT
        private static byte[] GenerateSalt(int size)
        {
            //Generate a cryptographic random number.
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            byte[] buff = new byte[size];
            rng.GetBytes(buff);
            return buff;
        }

        //FUNÇÃO PARA GERAR UMA SALTED HASH
        private static byte[] GenerateSaltedHash(string plainText, byte[] salt)
        {
            Rfc2898DeriveBytes rfc2898 = new Rfc2898DeriveBytes(plainText, salt, NUMBER_OF_ITERATIONS);
            return rfc2898.GetBytes(32);
        }

        //FUNÇÃO PARA DESATIVAR ELEMENTOS DO CHAT
        private void DisableChat()
        {
            textBoxMessage.Enabled = false;
            messageChat.Enabled = false;
            buttonEnviar.Enabled = false;
            buttonUpFile.Enabled = false;
        }

        //FUNÇÃO PARA ATIVAR ELEMENTOS DO CHAT
        private void EnableChat()
        {
            textBoxMessage.Enabled = true;
            messageChat.Enabled = true;
            buttonEnviar.Enabled = true;
            buttonUpFile.Enabled = true;
        }
    }
}
