﻿using System;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;


namespace FileManager
{
    public sealed partial class TextViewer : Page
    {
        private FileSystemStorageItem SFile;
        private FileControl FileControlInstance;

        public TextViewer()
        {
            InitializeComponent();
        }

        private async Task Initialize()
        {
            LoadingControl.IsLoading = true;
            try
            {
                string FileText = await FileIO.ReadTextAsync(SFile.File);

                Text.Text = FileText;

                await Task.Delay(500).ConfigureAwait(true);

                LoadingControl.IsLoading = false;
            }
            catch (ArgumentOutOfRangeException)
            {
                IBuffer buffer = await FileIO.ReadBufferAsync(SFile.File);
                DataReader reader = DataReader.FromBuffer(buffer);
                byte[] fileContent = new byte[reader.UnconsumedBufferLength];
                reader.ReadBytes(fileContent);
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                Encoding GBKEncoding = Encoding.GetEncoding("GBK");

                string FileText = GBKEncoding.GetString(fileContent);

                Text.Text = FileText;

                await Task.Delay(500).ConfigureAwait(true);

                LoadingControl.IsLoading = false;
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is Tuple<FileControl, FileSystemStorageItem> Parameters)
            {
                FileControlInstance = Parameters.Item1;
                SFile = Parameters.Item2;
                Title.Text = SFile.Name;

                await Initialize().ConfigureAwait(false);
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            SFile = null;
            Text.Text = string.Empty;
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            StorageFolder Folder = await SFile.File.GetParentAsync();
            StorageFile NewFile = await Folder.CreateFileAsync(SFile.Name, CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(NewFile, Text.Text);
            await SFile.UpdateRequested(NewFile).ConfigureAwait(true);
            FileControlInstance.Nav.GoBack();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            FileControlInstance.Nav.GoBack();
        }
    }
}
