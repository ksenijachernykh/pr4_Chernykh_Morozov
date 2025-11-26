using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Common;
using Newtonsoft.Json;

namespace Server
{
    public class Program
    {
        public static List<User> Users = new List<User>();
        public static IPAddress IpAdress;
        public static int Port;
        public static int CurrentUserId = -1;

        static void Main(string[] args)
        {
            Users.Add(new User("morozoov", "Asdfg123", @"C:\Users\student-A502.PERMAVIAT\Desktop\pr_4_ftp_Morozov_Chernyh-master"));
            Console.WriteLine("Введите IP адрес сервера:");
            string sIpAdress = Console.ReadLine();
            Console.Write("Введите порт: ");
            string sPort = Console.ReadLine();
            if (int.TryParse(sPort, out Port) && IPAddress.TryParse(sIpAdress, out IpAdress))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Данные успешно введены. Запускаю сервер.");
                StartServer();
            }

            Console.Read();
        }

        public static bool AuthorizationUser(string login, string password)
        {
            User user = Users.Find(x => x.login == login && x.password == password);
            return user != null;
        }

        public static List<string> GetDirectory(string src)
        {
            List<string> FoldersFiles = new List<string>();
            if (Directory.Exists(src))
            {
                string[] dirs = Directory.GetDirectories(src);
                foreach (string dir in dirs)
                {
                    string NameDirectory = dir.Replace(src, "");
                    FoldersFiles.Add(NameDirectory + "/");
                }
                string[] files = Directory.GetFiles(src);
                foreach (string file in files)
                {
                    string NameFile = file.Replace(src, "");
                    FoldersFiles.Add(NameFile);
                }
            }
            return FoldersFiles;
        }

        public static void StartServer()
        {
            IPEndPoint endPoint = new IPEndPoint(IpAdress, Port);
            Socket sListener = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream, ProtocolType.Tcp);

            sListener.Bind(endPoint);
            sListener.Listen(10);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Сервер запущен");

            while (true)
            {
                try
                {
                    Socket Handler = sListener.Accept();
                    string Data = null;
                    byte[] Bytes = new byte[10485760];
                    int BytesRec = Handler.Receive(Bytes);

                    Data += Encoding.UTF8.GetString(Bytes, 0, BytesRec);
                    Console.Write("Сообщения от пользователя: " + Data + "\n");
                    string Reply = "";

                    ViewModelSend viewModelSend = JsonConvert.DeserializeObject<ViewModelSend>(Data);
                    if (viewModelSend != null)
                    {
                        ViewModelMessage viewModelMessage;
                        string[] DataCommand = viewModelSend.Message.Split(new string[1] { " " }, StringSplitOptions.None);

                        if (DataCommand[0] == "connect")
                        {
                            if (DataCommand.Length >= 3 && AuthorizationUser(DataCommand[1], DataCommand[2]))
                            {
                                int IdUser = Users.FindIndex(x => x.login == DataCommand[1] && x.password == DataCommand[2]);
                                CurrentUserId = IdUser;
                                viewModelMessage = new ViewModelMessage("authorization", IdUser.ToString());
                                Console.WriteLine($"Пользователь авторизован, ID: {IdUser}");
                            }
                            else
                            {
                                viewModelMessage = new ViewModelMessage("message", "Неверный имя пользователя или пароль");
                            }
                            Reply = JsonConvert.SerializeObject(viewModelMessage);
                            byte[] message = Encoding.UTF8.GetBytes(Reply);
                            Handler.Send(message);
                        }
                        else if (DataCommand[0] == "cd")
                        {
                            if (CurrentUserId != -1)
                            {
                                List<string> FoldersFiles = new List<string>();
                                if (DataCommand.Length == 1)
                                {
                                    Users[CurrentUserId].temp_src = Users[CurrentUserId].src;
                                    FoldersFiles = GetDirectory(Users[CurrentUserId].src);
                                }
                                else
                                {
                                    string cdFolder = "";
                                    for (int i = 1; i < DataCommand.Length; i++)
                                        if (cdFolder == "")
                                            cdFolder += DataCommand[i];
                                        else
                                            cdFolder += " " + DataCommand[i];
                                    Users[CurrentUserId].temp_src = Users[CurrentUserId].temp_src + cdFolder;
                                    FoldersFiles = GetDirectory(Users[CurrentUserId].temp_src);
                                }
                                if (FoldersFiles.Count == 0)
                                    viewModelMessage = new ViewModelMessage("message", "Директория пуста или не существует");
                                else
                                    viewModelMessage = new ViewModelMessage("cd", JsonConvert.SerializeObject(FoldersFiles));
                            }
                            else
                                viewModelMessage = new ViewModelMessage("message", "Необходимо зарегистрироваться");
                            Reply = JsonConvert.SerializeObject(viewModelMessage);
                            byte[] message = Encoding.UTF8.GetBytes(Reply);
                            Handler.Send(message);
                        }
                        else if (DataCommand[0] == "get")
                        {
                            if (CurrentUserId != -1)
                            {
                                string getFile = "";
                                for (int i = 1; i < DataCommand.Length; i++)
                                    if (getFile == "")
                                        getFile += DataCommand[i];
                                    else
                                        getFile += " " + DataCommand[i];

                                byte[] byteFile = File.ReadAllBytes(Users[CurrentUserId].temp_src + getFile);
                                viewModelMessage = new ViewModelMessage("file", JsonConvert.SerializeObject(byteFile));
                            }
                            else
                                viewModelMessage = new ViewModelMessage("message", "Необходимо зарегистрироваться");
                            Reply = JsonConvert.SerializeObject(viewModelMessage);
                            byte[] message = Encoding.UTF8.GetBytes(Reply);
                            Handler.Send(message);
                        }
                        else
                        {
                            if (CurrentUserId != -1)
                            {
                                FileInfoFTP SendFileInfo = JsonConvert.DeserializeObject<FileInfoFTP>(viewModelSend.Message);
                                File.WriteAllBytes(Users[CurrentUserId].temp_src + @"\" + SendFileInfo.Name, SendFileInfo.Data);
                                viewModelMessage = new ViewModelMessage("message", "Файл загружен");
                            }
                            else
                                viewModelMessage = new ViewModelMessage("message", "Необходимо авторизоваться");
                            Reply = JsonConvert.SerializeObject(viewModelMessage);
                            byte[] message = Encoding.UTF8.GetBytes(Reply);
                            Handler.Send(message);
                        }
                    }
                }
                catch (Exception exp)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Что-то случилось: " + exp.Message);
                }
            }
        }
    }
}
