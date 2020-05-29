using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Configuration;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace Proxy
{
    class Program
    {

        static void Main(string[] args)
        {
            TcpListener listener = new TcpListener(IPAddress.Parse("127.0.0.1"), 777); // слушаем адрес прокси-сервера
            listener.Start();
            while (true)
            {
                if (listener.Pending())
                {
                    TcpClient client = listener.AcceptTcpClient();
                    Task.Factory.StartNew(() => ReceiveHTTP(client));
                    //Task task = new Task(() => ReceiveHTTP(client)); // когда нам приходит запрос на подключение создаем новый поток
                   // task.Start();
                }
            }
        }

        public static byte[] AbsToRel(byte[] data) // из абсолютного пути в относительный
        {
            string buffer = Encoding.ASCII.GetString(data);
            Regex regex = new Regex(@"http:\/\/[a-z0-9а-яё\:\.]*");
            MatchCollection matches = regex.Matches(buffer);
            string host = matches[0].Value;
            buffer = buffer.Replace(host, "");
            data = Encoding.ASCII.GetBytes(buffer);
            return data;
        }

        public static void ReceiveHTTP(TcpClient client)
        {
            NetworkStream browserStream = client.GetStream();
            byte[] buf = new byte[65536];
            while (browserStream.CanRead) 
            {
                try
                {

                        int bytesRead = browserStream.Read(buf, 0, buf.Length); //читаем http запрос браузера
                        HTTPserv(buf, bytesRead, browserStream, client);
                }
                catch (IOException)
                {
                    return;
                }
            }
        }

        public static void HTTPserv(byte[] buf, int bytesRead, NetworkStream browserStream, TcpClient client)
        {
            try
            {
                string[] temp = Encoding.ASCII.GetString(buf).Trim().Split(new char[] { '\r', '\n' });
                string req = temp.FirstOrDefault(x => x.Contains("Host"));
                req = req.Substring(req.IndexOf(":") + 2);
                string[] port = req.Trim().Split(new char[] { ':' }); // получаем имя домена и номер порта
                IPHostEntry myIPHostEntry = Dns.GetHostEntry(port[0]);

                TcpClient server;
                if (port.Length == 2)
                {
                    server = new TcpClient(port[0], int.Parse(port[1])); // если есть порт
                }
                else
                {
                    server = new TcpClient(port[0], 80); // если нет порта (= стандартный 80)
                }

                NetworkStream servStream = server.GetStream(); // поток с сервером

                servStream.Write(AbsToRel(buf), 0, bytesRead); // отправляем http запрос браузера по назначению
                
                var respBuf = new byte[65536]; // для ответа сервера


                int read = servStream.Read(respBuf, 0, respBuf.Length); // ответ от сервера

                browserStream.Write(respBuf, 0, read); // отправляем ответ браузеру

                string[] head = Encoding.UTF8.GetString(respBuf).Split(new char[] { '\r', '\n' }); // получаем код ответа
                string ResponseCode = head[0].Substring(head[0].IndexOf(" ") + 1);
                Console.WriteLine($"\n{req} {ResponseCode}"); // вывод результата
                servStream.CopyTo(browserStream); // перенаправляем остальные данные от сервера к браузеру

            }
            catch
            {
                return;
            }
            finally
            {
                client.Dispose();
            }

        }

    }

}