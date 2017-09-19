using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using OneC.ExternalComponents;
using System.Net;
using System.IO;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Linq;
using System.EnterpriseServices;

namespace OneCSocketServer
{
    [Guid("835E5814-3642-4433-8473-FA4D90E88C4E")]
    [ProgId("AddIn.OneCSocketServer")]
    public class OneCSocketServer : ExtComponentBase
    {
        [Export1c]
        public string Prefix { get; set; }

        Server server;

        public OneCSocketServer()
        {
            ComponentName = "OneCSocketServer";
            InitEvent += new InitEventHandler(Initialization);
        }

        public void Initialization()
        {

        }

        [Export1c]
        public void Start()
        {
            Server server = new Server(8888, this);
        }

        [Export1c]
        public void Stop()
        {
            server = null;
        }

        public void SendData(string action, string data)
        {
            this.Async.ExternalEvent("AddIn.OneCSocketServer", action, data);
        }
    }

    public class Server
    {
        TcpListener Listener;
        OneCSocketServer context1c;

        public Server(int Port, OneCSocketServer context)
        {
            context1c = context;

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
                var client = new Client(this);
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

        public void SendData(string action, string data)
        {
            context1c.SendData(action, data);
        }
    }

    public class Client
    {
        const int MAX_LENGTH_HEADER = 8192;

        public OneCSocketServer context1c;

        Server serverContext;

        public Client(Server context)
        {
            serverContext = context;
        }

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
            string SendData = "";
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
                        if (ReqestData[1] != string.Empty) SendData = ReqestData[1];
                        break;
                    }
                }
            }

            var RequestUri = Request.RegexParse1(@"^\w+\s+([^\s]+)[^\s]*\s+HTTP/.*|").Split(' ').FirstOrDefault(x => x[0] == '/');

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

            serverContext.SendData(RequestUri.Replace("/", ""), SendData);

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
        public static string RegexParse1(this string data, string pattern)
        {
            var result = "";

            var regex = new Regex(pattern);
            if (regex.IsMatch(data)) result = regex.Match(data).ToString();

            return result;
        }
    }
}