﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using EI.SI;

namespace Servidor
{
    class Program
    {
        private const int port = 10000;

        static void Main(string[] args)
        {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, port);
            TcpListener listener = new TcpListener(endPoint);

            //iniciar o servidor
            listener.Start();
            Console.WriteLine("Server is ready! Super Ready!");
            int clientCounter = 0;

            while (true)
            {
                //aceitar ligaçoes
                TcpClient client = listener.AcceptTcpClient();
                clientCounter++;
                Console.WriteLine("Cliente conectados: ", clientCounter);
                //utilizar threads
                ClienteHandler clientHandler = new ClienteHandler(client, clientCounter);
                clientHandler.Handle();
            }
        }
    }

    class ClienteHandler
    {
        private TcpClient client;
        private int clientId;
        //construtor da classe
        public ClienteHandler(TcpClient client, int clientId)
        {
            this.client = client;
            this.clientId = clientId;
        }

        //funçao para tratamento da tread
        public void Handle()
        {
            Thread thread = new Thread(threadHandler);
            thread.Start();
        }

        private void threadHandler()
        {
            NetworkStream networkStream = this.client.GetStream();
            ProtocolSI protocoloSI = new ProtocolSI();

            //enquanto a tread nao receber ordem de termino
            while (protocoloSI.GetCmdType() != ProtocolSICmdType.EOT)
            {
                int bytesRead = networkStream.Read(protocoloSI.Buffer, 0, protocoloSI.Buffer.Length);
                byte[] ack;
                string username, stringSaltedPasswordHash, salt, chave, hash, assinatura;

                verificarLoginRegisto loginRegisto = new verificarLoginRegisto();

                switch (protocoloSI.GetCmdType())
                {
                    /*case ProtocolSICmdType.USER_OPTION_1: //Login
                        byte[] saltedPasswordHash = Convert.FromBase64String(stringSaltedPasswordHash);

                        if (loginRegisto.VerifyLogin(username, saltedPasswordHash){

                        }
                               break;

                    case ProtocolSICmdType.USER_OPTION_2: //registo
                        verificarAssinatura v = new verificarAssinatura();

                        if (v.Verificar(hash, assinatura))
                        {
                            loginRegisto.Register(username, saltedPasswordHash, salt, chave);
                        }
                        break;*/

                    case ProtocolSICmdType.DATA: //mensagem normal
                        Console.WriteLine("Client" + clientId + " : " + protocoloSI.GetStringFromData());
                        ack = protocoloSI.Make(ProtocolSICmdType.ACK);
                        //envia mensagem para stream
                        networkStream.Write(ack, 0, ack.Length);
                        break;

                    case ProtocolSICmdType.EOT:
                        Console.WriteLine("Ending thread from client {0}" + clientId);
                        ack = protocoloSI.Make(ProtocolSICmdType.ACK);
                        //envia mensagem para stream
                        networkStream.Write(ack, 0, ack.Length);
                        break;
                }                
            }
            networkStream.Close();
            client.Close();
        }
    }
}
