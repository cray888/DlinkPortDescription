using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using Newtonsoft.Json;

namespace D_Link_description_console
{
    class Program
    {
        //Тестовый комментарий
        static void Main(string[] args)
        {
            string host = args[0];
            int port = int.Parse(args[1]);
            string login = args[2];
            string password = args[3];
            string filePath = args[4];
            string command = args[5];
            int startPort = int.Parse(args[6]);
            int endPort = 0;
            if (args.Length == 8) endPort = int.Parse(args[7]);

            TcpClient socket = null;
            try
            {
                socket = new TcpClient(host, port);
            }
            catch (SocketException)
            {
                Console.WriteLine("Unknown host - " + host + ". Quitting");
                Environment.Exit(1);
            }
            NetworkStream stream = socket.GetStream();
            StreamWriter output = new StreamWriter(stream);
            StreamReader input = new StreamReader(stream);

            Thread t;

            DescrReciver cliobj = new DescrReciver(input, command);
            t = new Thread(new ThreadStart(cliobj.Run));
            t.Start();

            output.Write(login + "\r\n");
            output.Write(password + "\r\n");

            if (command == "desc")
            { 
                if (endPort != 0 && startPort != endPort)
                {
                    for (int i = startPort; i < endPort; i = i + 5)
                    {
                        output.Write("show ports " + i.ToString() + "-" + ((i + 4) > endPort ? endPort : i + 4).ToString() + " description\r\n");
                        output.Write("q");
                    }
                }
                else
                {
                    output.Write("show ports " + startPort + " description\r\n");
                    output.Write("q");
                }
            }
            else if  (command == "errs")
            {
                if (endPort == 0) endPort = startPort;
                for (int i = startPort; i <= endPort; i++)
                {
                    output.Write("show error ports " + i.ToString() + "\r\n");
                    output.Write("q");
                }
            }

            output.Write("logout\r\n");
            output.Flush();

            while (t.IsAlive) { }

            string result = cliobj.getJSONResult();

            File.WriteAllText(filePath, result);

#if DEBUG
            Console.ReadLine();
#endif

        }

        public class DescrReciver
        {
            StreamReader input;
            string command;
            List<PortDescription> portDescriptionList;
            List<PortError> portErrorList;
            private bool isDGS;

            public DescrReciver(StreamReader input, string command)
            {
                this.input = input;
                this.command = command;
                portDescriptionList = new List<PortDescription>();
                portErrorList = new List<PortError>();
            }

            public void Run()
            {
                String line;
                int stringCounter = -1;

                string port = string.Empty;
                string link = string.Empty;
                string descr = string.Empty;
                string kv = string.Empty;
                string errCRCRX = string.Empty;
                string errCRCTX = string.Empty;

                while ((line = input.ReadLine()) != null)
                {
                    if (line.Contains("DGS-1210"))
                    {
                        isDGS = true;
                    }
                    if (stringCounter >= 0) stringCounter++;                    
                    if (command == "desc")
                    {
                        if (line.Contains("Enabled")) stringCounter = 0;
                        if (stringCounter >= 0)
                        {
                            line = line.Replace("\u001b[?25l", "");

                            if (stringCounter == 0)
                            {
                                if (isDGS)
                                {
                                    port = line.Substring(0, 6);
                                    link = line.Substring(38, 25);
                                }
                                else
                                {
                                    port = line.Substring(0, 6);
                                    link = line.Substring(42, 25);
                                }
                            }
                            else if (stringCounter == 2)
                            {
                                descr = line.Substring(6, 25).ToLower();
                                if (descr.IndexOf("_kv") != -1)
                                {
                                    kv = descr.Substring(descr.IndexOf("_kv") + 3, 3).Trim();
                                }
                                else if (descr.IndexOf("_") != -1)
                                {
                                    kv = descr.Substring(descr.IndexOf("_") + 1, 3).Trim();
                                }
                                else kv = string.Empty;
                                int intkv = 0;
                                if (int.TryParse(kv, out intkv) != true)
                                {
                                    kv = string.Empty;
                                }
                            }

                        }
                        if (stringCounter == 2)
                        {
                            Console.WriteLine(port + " " + link + " " + descr + " " + kv);
                            portDescriptionList.Add(new PortDescription() { port = port.Trim(), link = link.Trim(), descr = descr.Trim(), kv = kv });
                            stringCounter = -1;
                        }
                    }
                    else if (command == "errs")
                    {
                        if (line.Contains("Port Number")) stringCounter = 0;
                        if (stringCounter >= 0)
                        {
                            if (stringCounter == 0)
                            {
                                port = line.Substring(15, 2);
                            }
                            else if (stringCounter == 6)
                            {
                                if (isDGS)
                                {
                                    errCRCRX = line.Substring(21, 10).ToLower();
                                }
                                else
                                {
                                    errCRCRX = line.Substring(17, 10).ToLower();
                                }
                            }
                            else if (stringCounter == 8)
                            {
                                if (isDGS)
                                {
                                    errCRCTX = line.Substring(64, 10).ToLower();
                                }
                                else
                                {
                                    errCRCTX = line.Substring(60, 10).ToLower();
                                }
                            }
                            if (stringCounter == 8)
                            {
                                Console.WriteLine(port + " " + errCRCRX + " " + errCRCTX);
                                portErrorList.Add(new PortError() { port = port.Trim(), errCRCRX = errCRCRX.Trim(), errCRCTX = errCRCTX.Trim() });
                                stringCounter = -1;
                            }
                        }
                    }
                }
            }

            public string getJSONResult()
            {
                if (command == "desc")
                {
                    return JsonConvert.SerializeObject(portDescriptionList, Formatting.Indented);
                }
                else if (command == "errs")
                {
                    return JsonConvert.SerializeObject(portErrorList, Formatting.Indented);
                }
                return "";
            }
        }

        public class PortDescription
        {
            public string port { get; set; }
            public string link { get; set; }
            public string descr { get; set; }
            public string kv { get; set; }
        }

        public class PortError
        {
            public string port { get; set; }
            public string errCRCRX { get; set; }
            public string errCRCTX { get; set; }
        }
    }
}
