using Common;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace ClientWpf
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private IPAddress ipAddress;
        private int port;
        private int userId = -1;
        private Stack<string> directoryStack = new Stack<string>();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (IPAddress.TryParse(txtIpAddress.Text, out ipAddress) && int.TryParse(txtPort.Text, out port))
            {
                string login = txtLogin.Text;
                string password = txtPassword.Password;
                if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
                {
                    MessageBox.Show("Введите логин и пароль.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                try
                {
                    var response = SendCommand($"connect {login} {password}");
                    if (response?.Command == "autorization")
                    {
                        userId = int.Parse(response.Data);
                        MessageBox.Show("Подключение успешно!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadDirectories();
                    }
                    else
                    {
                        MessageBox.Show(response?.Data ?? "Ошибка авторизации.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка подключения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Введите корректный IP и порт.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LoadDirectories()
        {
            try
            {
                var response = SendCommand("cd");
                if (response?.Command == "cd")
                {
                    var directories = JsonConvert.DeserializeObject<string[]>(response.Data);
                    lstDirectories.Items.Clear();

                    if (directoryStack.Count > 0)
                    {
                        lstDirectories.Items.Add("Назад");
                    }

                    foreach (var dir in directories)
                    {
                        lstDirectories.Items.Add(dir);
                    }
                }
                else
                {
                    MessageBox.Show("Не удалось загрузить список директорий.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private ViewModelMessage SendCommand(string message)
        {
            try
            {
                IPEndPoint endPoint = new IPEndPoint(ipAddress, port);
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    socket.Connect(endPoint);
                    if (socket.Connected)
                    {
                        var request = new ViewModelSend(message, userId);
                        byte[] requestBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(request));
                        socket.Send(requestBytes);

                        byte[] responseBytes = new byte[10485760];
                        int receivedBytes = socket.Receive(responseBytes);
                        string responseData = Encoding.UTF8.GetString(responseBytes, 0, receivedBytes);

                        return JsonConvert.DeserializeObject<ViewModelMessage>(responseData);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка соединения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return null;
        }

        private void lstDirectories_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (lstDirectories.SelectedItem == null)
                return;

            string selectedItem = lstDirectories.SelectedItem.ToString();

            if (selectedItem == "Назад")
            {
                if (directoryStack.Count > 0)
                {
                    directoryStack.Pop();
                    LoadDirectories();
                }
            }
            if (selectedItem.EndsWith("\\"))
            {
                directoryStack.Push(selectedItem);
                var response = SendCommand($"cd {selectedItem.TrimEnd('/')}");

                if (response?.Command == "cd")
                {
                    var items = JsonConvert.DeserializeObject<List<string>>(response.Data);
                    lstDirectories.Items.Clear();
                    lstDirectories.Items.Add("Назад");
                    foreach (var item in items)
                    {
                        lstDirectories.Items.Add(item);
                    }
                }
                else
                {
                    MessageBox.Show($"Ошибка открытия директории: {response?.Data}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                DownloadFile(selectedItem);
            }
        }

        private void DownloadFile(string fileName)
        {
            try
            {
                string localSavePath = GetUniqueFilePath(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), Path.GetFileName(fileName));
                Console.WriteLine($"Trying to download file from server: {fileName}");
                var socket = Connecting(ipAddress, port);
                if (socket == null)
                {
                    MessageBox.Show("Не удалось подключиться к серверу.");
                    return;
                }
                string command = $"get {fileName}";
                ViewModelSend viewModelSend = new ViewModelSend(command, userId);
                byte[] messageBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(viewModelSend));
                socket.Send(messageBytes);
                byte[] buffer = new byte[10485760];
                int bytesReceived = socket.Receive(buffer);
                string serverResponse = Encoding.UTF8.GetString(buffer, 0, bytesReceived);
                ViewModelMessage responseMessage = JsonConvert.DeserializeObject<ViewModelMessage>(serverResponse);
                socket.Close();
                if (responseMessage.Command == "file")
                {
                    byte[] fileData = JsonConvert.DeserializeObject<byte[]>(responseMessage.Data);
                    File.WriteAllBytes(localSavePath, fileData);
                    MessageBox.Show($"Файл скачан и сохранён в: {localSavePath}");
                }
                else { }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        public Socket Connecting(IPAddress ipAddress, int port)
        {
            IPEndPoint endPoint = new IPEndPoint(ipAddress, port);
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                socket.Connect(endPoint);
                return socket;
            }
            catch (SocketException ex)
            {
                Debug.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
            finally
            {
                if (socket != null && !socket.Connected)
                {
                    socket.Close();
                }
            }
            return null;
        }
        private string GetUniqueFilePath(string directory, string fileName)
        {
            string uniqueFilePath = Path.Combine(directory, fileName);
            return uniqueFilePath;
        }

        public void SendFileToServer(string filePath)
        {
            try
            {
                var socket = Connecting(ipAddress, port);
                if (socket == null)
                {
                    MessageBox.Show("Не удалось подключиться к серверу.");
                    return;
                }
                if (!File.Exists(filePath))
                {
                    MessageBox.Show("Указанный файл не существует.");
                    return;
                }
                FileInfo fileInfo = new FileInfo(filePath);
                FileInfoFTP fileInfoFTP = new FileInfoFTP(File.ReadAllBytes(filePath), fileInfo.Name);
                ViewModelSend viewModelSend = new ViewModelSend(JsonConvert.SerializeObject(fileInfoFTP), userId);
                byte[] messageByte = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(viewModelSend));
                socket.Send(messageByte);
                byte[] buffer = new byte[10485760];
                int bytesReceived = socket.Receive(buffer);
                string serverResponse = Encoding.UTF8.GetString(buffer, 0, bytesReceived);
                ViewModelMessage responseMessage = JsonConvert.DeserializeObject<ViewModelMessage>(serverResponse);
                socket.Close();
                LoadDirectories();
                if (responseMessage.Command == "message")
                {
                    MessageBox.Show(responseMessage.Data);
                }
                else
                {
                    MessageBox.Show("Неизвестный ответ от сервера.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void Download(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                SendFileToServer(filePath);
            }
        }
    }
}
