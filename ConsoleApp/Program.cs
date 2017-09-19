using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            new Server(8888);
            Console.Read();
        }
    }

    public class Server
    {
        TcpListener Listener;
        public Server(int Port)
        {
            Listener = new TcpListener(IPAddress.Any, Port);

            Listener.Start();

            while (true)
            {
                ThreadPool.QueueUserWorkItem(new WaitCallback(ClientThread), Listener.AcceptTcpClient());
            }
        }

        void ClientThread(Object StateInfo)
        {
            try
            {
                var client = new Client();
                client.ClientRun((TcpClient)StateInfo);
            }
            catch
            {
                ((TcpClient)StateInfo).Close();
            }
        }

        ~Server()
        {
            if (Listener != null)
            {
                Listener.Stop();
            }
        }
    }

    public class Client
    {
        const int MAX_LENGTH_HEADER = 8192;

        private void SendError(TcpClient Client, int Code)
        {
            string CodeStr = Code.ToString() + " " + ((HttpStatusCode)Code).ToString();
            string Html = "<html><body><h1>" + CodeStr + "</h1></body></html>";
            string Str = "HTTP/1.1 " + CodeStr + "\nContent-type: text/html\nContent-Length:" + Html.Length.ToString() + "\n\n" + Html;
            byte[] Buffer = Encoding.UTF8.GetBytes(Str);
            Client.GetStream().Write(Buffer, 0, Buffer.Length);
            Client.Close();
        }

        public void ClientRun(TcpClient Client)
        {
            Thread.Sleep(100);

            string Request = "";
            byte[] Buffer = new byte[8192];
            int Count;
            while ((Count = Client.GetStream().Read(Buffer, 0, Buffer.Length)) > 0)
            {
                Request += Encoding.UTF8.GetString(Buffer, 0, Count);
                if (!Request.Contains("POST"))
                {
                    if (Request.IndexOf("\r\n\r\n") >= 0 || Request.Length > MAX_LENGTH_HEADER)
                    {
                        break;
                    }
                }
                else
                {
                    string[] ReqestData = Request.Split(new string[] { "\r\n\r\n" }, StringSplitOptions.None);
                    if (ReqestData.Count() > 1 || Request.Length > MAX_LENGTH_HEADER)
                    {
                        Console.WriteLine(string.Join("", Request.Split(' ').Take(2)));
                        if (ReqestData[1] != string.Empty) Console.WriteLine(ReqestData[1]);
                        break;
                    }
                }
            }

            var RequestUri = Request.RegexParse(@"^\w+\s+([^\s]+)[^\s]*\s+HTTP/.*|").Split(' ').FirstOrDefault(x => x[0] == '/');

            if (string.IsNullOrEmpty(RequestUri))
            {
                SendError(Client, 400);
                return;
            }

            RequestUri = Uri.UnescapeDataString(RequestUri);

            if (RequestUri.IndexOf("..") >= 0)
            {
                SendError(Client, 400);
                return;
            }

            if (RequestUri.Length > 0)
            {
                var split = RequestUri.Split('?').ToList();

                if (Request.Contains("POST"))
                {
                    var postData = Request.Split(new string[] { "\r\n\r\n" }, StringSplitOptions.None);
                    split.Insert(1, postData[1]);
                }

                var resultString = "OK"; //Формируем ответ

                if (string.IsNullOrEmpty(resultString))
                {
                    SendError(Client, 404);
                    return;
                }

                string Headers = "HTTP/1.1 200 OK\nContent-Type: text/html\nContent-Length: " + resultString.Length + "\n\n";
                byte[] HeadersBuffer = Encoding.UTF8.GetBytes(Headers);
                Client.GetStream().Write(HeadersBuffer, 0, HeadersBuffer.Length);
                using (var reader = new MemoryStream(Encoding.UTF8.GetBytes(resultString.ToString())))
                {
                    while (reader.Position < reader.Length)
                    {
                        Count = reader.Read(Buffer, 0, Buffer.Length);
                        Client.GetStream().Write(Buffer, 0, Count);
                    }
                    Client.Close();
                }
            }
            Client.Close();
        }
    }

    public static class ext
    {
        public static string RegexParse(this string data, string pattern)
        {
            var result = "";

            var regex = new Regex(pattern);
            if (regex.IsMatch(data)) result = regex.Match(data).ToString();

            return result;
        }
    }
}
