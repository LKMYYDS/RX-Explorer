﻿using Microsoft.Win32.SafeHandles;
using RX_Explorer.Interface;
using ShareClassLibrary;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Portable;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public class MTPStorageFile : FileSystemStorageFile, IMTPStorageItem
    {
        private MTPFileData RawData;
        private readonly MTPStorageFolder ParentFolder;
        private string InnerDisplayType;

        public override string DisplayType => string.IsNullOrEmpty(InnerDisplayType) ? Type : InnerDisplayType;

        public override bool IsReadOnly => RawData.IsReadOnly;

        public override bool IsSystemItem => RawData.IsSystemItem;

        public override ulong Size
        {
            get => RawData?.Size ?? base.Size;
            protected set => base.Size = value;
        }

        public override DateTimeOffset CreationTime
        {
            get => RawData?.CreationTime ?? base.CreationTime;
            protected set => base.CreationTime = value;
        }

        public override DateTimeOffset ModifiedTime
        {
            get => RawData?.ModifiedTime ?? base.ModifiedTime;
            protected set => base.ModifiedTime = value;
        }

        public string DeviceId => @$"\\?\{new string(Path.Skip(4).ToArray()).Split(@"\", StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()}";

        protected override Task<BitmapImage> GetThumbnailCoreAsync(ThumbnailMode Mode)
        {
            return Task.FromResult<BitmapImage>(null);
        }

        protected override Task<IRandomAccessStream> GetThumbnailRawStreamCoreAsync(ThumbnailMode Mode)
        {
            return Task.FromResult<IRandomAccessStream>(null);
        }

        public override Task<SafeFileHandle> GetNativeHandleAsync(AccessMode Mode, OptimizeOption Option)
        {
            return Task.FromResult(new SafeFileHandle(IntPtr.Zero, true));
        }

        protected override Task<BitmapImage> GetThumbnailOverlayAsync()
        {
            return Task.FromResult<BitmapImage>(null);
        }

        protected override async Task LoadCoreAsync(bool ForceUpdate)
        {
            using (RefSharedRegion<FullTrustProcessController.ExclusiveUsage> ControllerRef = GetProcessSharedRegion())
            {
                if (ControllerRef != null)
                {
                    InnerDisplayType = await ControllerRef.Value.Controller.GetFriendlyTypeNameAsync(Type);
                }
                else
                {
                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                    {
                        InnerDisplayType = await ControllerRef.Value.Controller.GetFriendlyTypeNameAsync(Type);
                    }
                }
            }

            if (RawData == null || ForceUpdate)
            {
                RawData = await GetRawDataAsync();
            }
        }

        public override async Task<Stream> GetStreamFromFileAsync(AccessMode Mode, OptimizeOption Option)
        {
            FileAccessMode Access = Mode switch
            {
                AccessMode.Read => FileAccessMode.Read,
                AccessMode.ReadWrite or AccessMode.Exclusive or AccessMode.Write => FileAccessMode.ReadWrite,
                _ => throw new NotSupportedException()
            };

            if (await GetStorageItemAsync() is StorageFile File)
            {
                if (await File.OpenAsync(Access) is IRandomAccessStream Stream)
                {
                    return Stream.AsStream();
                }
            }

            return null;
        }

        public override Task<ulong> GetSizeOnDiskAsync()
        {
            return Task.FromResult<ulong>(0);
        }

        public override async Task<StorageStreamTransaction> GetTransactionStreamFromFileAsync()
        {
            if (await GetStorageItemAsync() is StorageFile File)
            {
                return await File.OpenTransactedWriteAsync();
            }

            return null;
        }

        public override async Task<IStorageItem> GetStorageItemAsync()
        {
            if (StorageItem != null)
            {
                return StorageItem;
            }
            else
            {
                if (ParentFolder != null)
                {
                    if (await ParentFolder.GetStorageItemAsync() is StorageFolder Folder)
                    {
                        if (await Folder.TryGetItemAsync(Name) is StorageFile Item)
                        {
                            return StorageItem = Item;
                        }
                    }
                }
                else if (StorageDevice.FromId(DeviceId) is StorageFolder RootFolder)
                {
                    return StorageItem = await RootFolder.GetStorageItemByTraverse<StorageFile>(new PathAnalysis(Path, DeviceId));
                }

                return null;
            }
        }

        public async Task<MTPFileData> GetRawDataAsync()
        {
            try
            {
                using (RefSharedRegion<FullTrustProcessController.ExclusiveUsage> ControllerRef = GetProcessSharedRegion())
                {
                    if (ControllerRef != null)
                    {
                        return await ControllerRef.Value.Controller.GetMTPItemDataAsync(Path);
                    }
                    else
                    {
                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                        {
                            return await Exclusive.Controller.GetMTPItemDataAsync(Path);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An unexpected exception was threw in {nameof(GetRawDataAsync)}");
            }

            return null;
        }

        public MTPStorageFile(MTPFileData Data) : this(Data, null)
        {

        }

        public MTPStorageFile(MTPFileData Data, MTPStorageFolder Parent) : base(Data)
        {
            RawData = Data;
            ParentFolder = Parent;
        }
    }
}
