using Microsoft.Win32;
using ScriptExecutorWPF;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Serialization;
using Path = System.IO.Path;


namespace SequenceExecuter
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<ScriptItem> ScriptItems { get; set; } = new ObservableCollection<ScriptItem>();
        public ObservableCollection<SequenceItem> SequenceItems { get; set; } = new ObservableCollection<SequenceItem>();
        private SequenceItem _selectedSequence; // 当前选中的序列
        private ScriptItem _draggedScriptInSequence; // 序列内拖拽的脚本项
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this; // 设置数据上下文为当前窗口
            LoadScriptList(); // 加载脚本列表 (待实现)
            LoadSequenceList(); // 加载序列列表 (待实现)
            ScriptListDataGrid.ItemsSource = ScriptItems; // DataGrid 绑定脚本列表
            SequenceListBoxDataGrid.ItemsSource = SequenceItems; // ListBox 绑定序列列表
        }

        private void LoadScriptButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Multiselect = true; // 允许选择多个文件
            openFileDialog.Filter = "脚本文件 (*.py;*.bat)|*.py;*.bat|所有文件 (*.*)|*.*"; // 文件类型过滤

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (string filename in openFileDialog.FileNames)
                {
                    string scriptType = filename.ToLower().EndsWith(".py") ? "Python" : "BAT";
                    ScriptItems.Add(new ScriptItem { Name = System.IO.Path.GetFileName(filename), Path = filename, Parameters = "", Description = "", ScriptType = scriptType });
                }
                SaveScriptList(); // 加载后保存脚本列表 (待实现)
            }
        }

        private async void ScriptListDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ScriptListDataGrid.SelectedItem is ScriptItem selectedScript)
            {
                await ExecuteScript(selectedScript);
            }
        }

        private async Task ExecuteScript(ScriptItem script) // 修改为 async Task
        {
            OutputRichTextBox.Document.Blocks.Clear(); // 清空输出框

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = script.ScriptType == "Python" ? "python.exe" : "cmd.exe"; // 根据脚本类型设置执行程序
            startInfo.Arguments = script.ScriptType == "Python" ? $"\"{script.Path}\" {script.Parameters}" : $"/c \"{script.Path}\" {script.Parameters}"; // 设置参数
            startInfo.RedirectStandardOutput = true; // 重定向标准输出
            startInfo.RedirectStandardError = true; // 重定向标准错误
            startInfo.UseShellExecute = false; //  不使用 shell 启动进程
            startInfo.CreateNoWindow = true; // 不创建窗口

            await Task.Run(() => // 使用 Task.Run 异步执行
            {
                using (Process process = new Process()) // Process 对象放到 using 块中，确保资源释放
                {
                    process.StartInfo = startInfo;
                    process.OutputDataReceived += (sender, e) => { if (!string.IsNullOrEmpty(e.Data)) AppendOutputText(e.Data, Colors.Green); }; // 输出数据事件处理
                    process.ErrorDataReceived += (sender, e) => { if (!string.IsNullOrEmpty(e.Data)) AppendOutputText(e.Data, Colors.Red); }; // 错误数据事件处理

                    process.Start();
                    process.BeginOutputReadLine(); // 异步读取输出
                    process.BeginErrorReadLine(); // 异步读取错误
                    process.WaitForExit(); // 在后台线程中等待进程结束
                }
            });
        }
        private void AppendOutputText(string text, Color color)
        {
            Dispatcher.Invoke(() => // 确保在 UI 线程上更新 RichTextBox
            {
                TextRange range = new TextRange(OutputRichTextBox.Document.ContentEnd, OutputRichTextBox.Document.ContentEnd);
                range.Text = text + Environment.NewLine;
                range.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(color)); // 设置文本颜色
                OutputRichTextBox.ScrollToEnd(); // 滚动到末尾
            });
        }

        private void ScriptListDataGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (!(ScriptListDataGrid.SelectedItem is ScriptItem))
            {
                e.Handled = true; // 如果没有选中行，则取消显示右键菜单
            }
        }

        private void OpenFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (ScriptListDataGrid.SelectedItem is ScriptItem selectedScript)
            {
                OpenFileWithVSCodeOrDefault(selectedScript.Path);
            }
        }

        private void OpenFileWithVSCodeOrDefault(string filePath)
        {
            string vscodePath = GetVSCodePath(); // 调用新的函数来自动获取 VS Code 路径

            if (!string.IsNullOrEmpty(vscodePath))
            {
                Process.Start(vscodePath, $"--reuse-window \"{filePath}\""); // 使用 VS Code 打开
            }
            else
            {
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true }); // 使用默认程序打开
            }
        }

        private string GetVSCodePath()
        {
            // 1. 尝试从常用安装路径查找
            string commonPaths = @"C:\Program Files\Microsoft VS Code\Code.exe;C:\Program Files (x86)\Microsoft VS Code\Code.exe";
            foreach (string path in commonPaths.Split(';'))
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            // 2. 尝试从注册表查找 (Windows only)
            if (Environment.OSVersion.Platform == PlatformID.Win32NT) // 检查是否是 Windows 系统
            {
                string registryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall{{C0C07359-1DD1-43A0-98A2-74CFF25EA644}_is1}"; // VS Code 用户安装的注册表路径 (可能需要根据版本变化)
                string vscodeInstallPath = Registry.LocalMachine.OpenSubKey(registryPath)?.GetValue("InstallLocation") as string; // 从注册表读取安装路径

                if (!string.IsNullOrEmpty(vscodeInstallPath))
                {
                    string vscodeExePath = Path.Combine(vscodeInstallPath, "Code.exe");
                    if (File.Exists(vscodeExePath))
                    {
                        return vscodeExePath;
                    }
                }

                // 尝试查找用户级别的安装 (可能注册表路径不同)
                string userRegistryPath = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall{{C0C07359-1DD1-43A0-98A2-74CFF25EA644}_is1}"; //  64位系统用户安装的注册表路径
                vscodeInstallPath = Registry.CurrentUser.OpenSubKey(userRegistryPath)?.GetValue("InstallLocation") as string; // 从当前用户注册表读取安装路径
                if (!string.IsNullOrEmpty(vscodeInstallPath))
                {
                    string vscodeExePath = Path.Combine(vscodeInstallPath, "Code.exe");
                    if (File.Exists(vscodeExePath))
                    {
                        return vscodeExePath;
                    }
                }


                // 尝试查找另一种可能的注册表路径 (对于某些版本的 VS Code)
                string anotherRegistryPath = @"SOFTWARE\Microsoft\VSCode";
                vscodeInstallPath = Registry.LocalMachine.OpenSubKey(anotherRegistryPath)?.GetValue("InstallDir") as string;
                if (!string.IsNullOrEmpty(vscodeInstallPath))
                {
                    string vscodeExePath = Path.Combine(vscodeInstallPath, "Code.exe");
                    if (File.Exists(vscodeExePath))
                    {
                        return vscodeExePath;
                    }
                }

            }

            // 3. 尝试从 PATH 环境变量查找 (假设 Code.exe 已经添加到 PATH)
            string pathFromEnv = FindExecutableInPath("Code.exe");
            if (!string.IsNullOrEmpty(pathFromEnv))
            {
                return pathFromEnv;
            }
            return null; // 如果以上方法都找不到，则返回 null
        }

        private string FindExecutableInPath(string executableName)
        {
            string pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathEnv)) return null;

            string[] pathDirs = pathEnv.Split(System.IO.Path.PathSeparator);
            foreach (string dir in pathDirs)
            {
                string fullPath = Path.Combine(dir, executableName);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
            return null;
        }

        private void NewSequenceButton_Click(object sender, RoutedEventArgs e)
        {
            string sequenceName = "序列 " + (SequenceItems.Count + 1); // 默认序列名称
            SequenceItems.Add(new SequenceItem { Name = sequenceName });
            SaveSequenceList(); // 保存序列列表 (待实现)
        }

        private async void ExecuteSelectedScriptButton_Click(object sender, RoutedEventArgs e)
        {
            if (ScriptListDataGrid.SelectedItem is ScriptItem selectedScript)
            {
                await ExecuteScript(selectedScript); // 调用已有的 ExecuteScript 方法执行脚本
            }
            else
            {
                MessageBox.Show("请先在脚本列表中选择要执行的脚本。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        private void RemoveScriptButton_Click(object sender, RoutedEventArgs e)
        {
            if (ScriptListDataGrid.SelectedItem is ScriptItem selectedScript)
            {
                ScriptItems.Remove(selectedScript); // 从 ScriptItems 集合中移除选中的脚本
                SaveScriptList(); // 保存更新后的脚本列表
            }
            else
            {
                MessageBox.Show("请先在脚本列表中选择要移除的脚本。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        private void DeleteSequenceButton_Click(object sender, RoutedEventArgs e)
        {
            if (SequenceListBoxDataGrid.SelectedItem is SequenceItem selectedSequence)
            {
                SequenceItems.Remove(selectedSequence);
                SaveSequenceList(); // 保存序列列表 (待实现)
                if (SequenceItems.Count > 0)
                {
                    SequenceListBoxDataGrid.SelectedItem = SequenceItems.Last(); // 选中最后一个序列
                }
                else
                {
                    SequenceEditListBox.ItemsSource = null; // 清空序列编辑列表
                }
            }
        }

        private void SequenceListBoxDataGrid_ContextMenuOpening(object sender, SelectionChangedEventArgs e)
        {
            if (SequenceListBoxDataGrid.SelectedItem is SequenceItem selectedSequence)
            {
                _selectedSequence = selectedSequence; // 更新当前选中序列
                SequenceEditListBox.ItemsSource = selectedSequence.Scripts; // 序列编辑列表绑定到选中序列的脚本列表
            }
            else
            {
                SequenceEditListBox.ItemsSource = null; // 如果没有选中序列，则清空序列编辑列表
            }
        }

        private void SequenceEditListBox_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(ScriptItem))) // 拖拽的数据是否是 ScriptItem 类型
            {
                e.Effects = DragDropEffects.Copy; // 设置拖拽效果为复制
            }
            else
            {
                e.Effects = DragDropEffects.None; // 否则不允许拖拽
            }
            e.Handled = true; // 标记事件已处理
        }

        private void SequenceEditListBox_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(ScriptItem)) && _selectedSequence != null)
            {
                ScriptItem draggedScript = (ScriptItem)e.Data.GetData(typeof(ScriptItem));
                _selectedSequence.Scripts.Add(draggedScript); // 将拖拽的脚本添加到当前选中序列的脚本列表
                SaveSequenceList(); // 保存序列列表 (待实现，序列内容已改变)
            }
            e.Handled = true; // 标记事件已处理
        }

        private Point _startPoint; // 拖拽起始点

        private void ScriptListDataGrid_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && ScriptListDataGrid.SelectedItem != null)
            {
                Point currentPoint = e.GetPosition(ScriptListDataGrid);
                if (Math.Abs(currentPoint.X - _startPoint.X) > 10 || Math.Abs(currentPoint.Y - _startPoint.Y) > 10) // 移动超过一定距离才开始拖拽
                {
                    DragDrop.DoDragDrop(ScriptListDataGrid, ScriptListDataGrid.SelectedItem, DragDropEffects.Copy); // 启动拖拽操作
                }
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) // 记录拖拽起始点
        {
            base.OnMouseLeftButtonDown(e);
            _startPoint = e.GetPosition(ScriptListDataGrid);
        }


        

        private void SequenceEditListBoxItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem listBoxItem)
            {
                _draggedScriptInSequence = listBoxItem.DataContext as ScriptItem; // 记录拖拽的脚本项
            }
        }

        private void SequenceListBox_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (!(SequenceListBoxDataGrid.SelectedItem is SequenceItem))
            {
                e.Handled = true; // 如果没有选中序列，则取消显示右键菜单
            }
        }
        private async void ExecuteSequenceButton_Click(object sender, RoutedEventArgs e) // 修改为 async void
        {
            if (SequenceListBoxDataGrid.SelectedItem is SequenceItem selectedSequence)
            {
                if (selectedSequence.Scripts.Count == 0)
                {
                    MessageBox.Show("当前选中的序列不包含任何脚本。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return; // 如果序列为空，则直接返回
                }


                foreach (ScriptItem script in selectedSequence.Scripts)
                {
                    await ExecuteScript(script); // 依次执行序列中的每个脚本，并等待前一个脚本执行完成
                }

                MessageBox.Show($"序列 \"{selectedSequence.Name}\" 执行完成。", "提示", MessageBoxButton.OK, MessageBoxImage.Information); // 序列执行完成后提示
            }
            else
            {
                MessageBox.Show("请先在序列列表中选择要执行的序列。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        private void RenameSequenceMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (SequenceListBoxDataGrid.SelectedItem is SequenceItem selectedSequence)
            {
                // 1. 隐藏 ListBoxItem 的默认显示内容
                //SequenceListBox.ItemContainerGenerator.ContainerFromItem(selectedSequence).IsEnabled = false; // 禁用选择，间接隐藏文本 (更简洁方法)

                // 2. 创建 TextBox 并显示在 ListBoxItem 的位置
                TextBox textBox = new TextBox();
                textBox.Text = selectedSequence.Name;
                textBox.LostFocus += (s, args) =>
                {
                    ConfirmRenameSequence(selectedSequence, textBox.Text); // 失去焦点时确认重命名
                };
                textBox.KeyDown += (s, args) =>
                {
                    if (args.Key == Key.Enter)
                    {
                        ConfirmRenameSequence(selectedSequence, textBox.Text); // 按下 Enter 键时确认重命名
                    }
                    else if (args.Key == Key.Escape)
                    {
                        CancelRenameSequence(selectedSequence); // 按下 Esc 键时取消重命名
                    }
                };

                // 3. 将 TextBox 添加到 ListBoxItem 的视觉树中 (覆盖原有内容)
                if (SequenceListBoxDataGrid.ItemContainerGenerator.ContainerFromItem(selectedSequence) is ListBoxItem listBoxItem)
                {
                    listBoxItem.Content = textBox;
                    textBox.Focus(); // 让 TextBox 获得焦点，开始编辑
                    textBox.SelectAll(); // 选中 TextBox 中的所有文本，方便用户直接输入
                }
            }
        }

        private void ConfirmRenameSequence(SequenceItem sequence, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
            {
                MessageBox.Show("序列名称不能为空。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                CancelRenameSequence(sequence); // 名称为空，取消重命名
                return;
            }

            sequence.Name = newName; // 更新序列名称
            SaveSequenceList(); // 保存序列列表

            // 恢复 ListBoxItem 的正常显示 (重要步骤)
            if (SequenceListBoxDataGrid.ItemContainerGenerator.ContainerFromItem(sequence) is ListBoxItem listBoxItem)
            {
                listBoxItem.Content = sequence.Name; // 重新设置 ListBoxItem 的 Content 为序列名称
                listBoxItem.IsEnabled = true; // 重新启用 ListBoxItem 的选择和默认显示
            }
        }

        private void CancelRenameSequence(SequenceItem sequence)
        {
            // 取消重命名，恢复 ListBoxItem 的正常显示
            if (SequenceListBoxDataGrid.ItemContainerGenerator.ContainerFromItem(sequence) is ListBoxItem listBoxItem)
            {
                listBoxItem.Content = sequence.Name; // 恢复 ListBoxItem 的 Content 为序列名称
                listBoxItem.IsEnabled = true; // 重新启用 ListBoxItem 的选择和默认显示
            }
        }

        private void SequenceEditListBoxItem_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _draggedScriptInSequence != null)
            {
                ListBoxItem listBoxItem = sender as ListBoxItem;
                if (listBoxItem != null && listBoxItem.DataContext is ScriptItem scriptItem)
                {
                    DragDrop.DoDragDrop(listBoxItem, scriptItem, DragDropEffects.Move); // 启动序列内拖拽 (Move 效果)
                }
            }
        }

        private void SequenceEditListBoxItem_Drop(object sender, DragEventArgs e)
        {
            if (sender is ListBoxItem targetItem && targetItem.DataContext is ScriptItem targetScript && e.Data.GetDataPresent(typeof(ScriptItem)))
            {
                ScriptItem droppedScript = e.Data.GetData(typeof(ScriptItem)) as ScriptItem;
                if (droppedScript != null && targetScript != droppedScript && _selectedSequence != null && _selectedSequence.Scripts.Contains(droppedScript) && _selectedSequence.Scripts.Contains(targetScript))
                {
                    int oldIndex = _selectedSequence.Scripts.IndexOf(droppedScript);
                    int newIndex = _selectedSequence.Scripts.IndexOf(targetScript);
                    if (oldIndex != -1 && newIndex != -1)
                    {
                        _selectedSequence.Scripts.Move(oldIndex, newIndex); // 移动脚本项
                        SaveSequenceList(); // 保存序列列表 (序列顺序已改变)
                    }
                }
                _draggedScriptInSequence = null; // 清空拖拽脚本项
            }
            e.Handled = true; // 标记事件已处理
        }

      

private string scriptListFilePath = "ScriptList.xml"; // 脚本列表保存文件路径
    private string sequenceListFilePath = "SequenceList.xml"; // 序列列表保存文件路径

    private void SaveScriptList()
    {
        XmlSerializer serializer = new XmlSerializer(typeof(ObservableCollection<ScriptItem>));
        using (TextWriter writer = new StreamWriter(scriptListFilePath))
        {
            serializer.Serialize(writer, ScriptItems);
        }
    }

    private void LoadScriptList()
    {
        if (File.Exists(scriptListFilePath))
        {
            XmlSerializer serializer = new XmlSerializer(typeof(ObservableCollection<ScriptItem>));
            using (TextReader reader = new StreamReader(scriptListFilePath))
            {
                try
                {
                    ObservableCollection<ScriptItem> loadedScripts = serializer.Deserialize(reader) as ObservableCollection<ScriptItem>;
                    if (loadedScripts != null)
                    {
                        ScriptItems.Clear();
                        foreach (var script in loadedScripts)
                        {
                            ScriptItems.Add(script);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 处理加载异常，例如文件损坏
                    MessageBox.Show($"加载脚本列表失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private void SaveSequenceList()
    {
        XmlSerializer serializer = new XmlSerializer(typeof(ObservableCollection<SequenceItem>));
        using (TextWriter writer = new StreamWriter(sequenceListFilePath))
        {
            serializer.Serialize(writer, SequenceItems);
        }
    }

    private void LoadSequenceList()
    {
        if (File.Exists(sequenceListFilePath))
        {
            XmlSerializer serializer = new XmlSerializer(typeof(ObservableCollection<SequenceItem>));
            using (TextReader reader = new StreamReader(sequenceListFilePath))
            {
                try
                {
                    ObservableCollection<SequenceItem> loadedSequences = serializer.Deserialize(reader) as ObservableCollection<SequenceItem>;
                    if (loadedSequences != null)
                    {
                        SequenceItems.Clear();
                        foreach (var sequence in loadedSequences)
                        {
                            SequenceItems.Add(sequence);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 处理加载异常
                    MessageBox.Show($"加载序列列表失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    //  在窗口关闭时保存列表 (MainWindow_Closing 事件)
    private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        SaveScriptList();
        SaveSequenceList();
    }

        private void SequenceListBoxDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SequenceListBoxDataGrid.SelectedItem is SequenceItem selectedSequence)
            {
                _selectedSequence = selectedSequence; // 更新当前选中序列
                SequenceEditListBox.ItemsSource = selectedSequence.Scripts; // 序列编辑列表绑定到选中序列的脚本列表
            }
            else
            {
                SequenceEditListBox.ItemsSource = null; // 如果没有选中序列，则清空序列编辑列表
            }
        }
    }
}
