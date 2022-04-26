﻿using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 提供对背景图片选择的UI支持
    /// </summary>
    public sealed class BackgroundPicture : IEquatable<BackgroundPicture>
    {
        /// <summary>
        /// 背景图片
        /// </summary>
        public BitmapImage Thumbnail { get; }

        /// <summary>
        /// 图片Uri
        /// </summary>
        public Uri PictureUri { get; }

        public async Task<BitmapImage> GetFullSizeBitmapImageAsync()
        {
            try
            {
                BitmapImage Bitmap = new BitmapImage();

                StorageFile ImageFile = await StorageFile.GetFileFromApplicationUriAsync(PictureUri);

                using (IRandomAccessStream Stream = await ImageFile.OpenAsync(FileAccessMode.Read))
                {
                    await Bitmap.SetSourceAsync(Stream);
                }

                return Bitmap;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(GetFullSizeBitmapImageAsync)}");
                return null;
            }
        }

        public static bool operator ==(BackgroundPicture left, BackgroundPicture right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BackgroundPicture left, BackgroundPicture right)
        {
            return !left.Equals(right);
        }

        public bool Equals(BackgroundPicture other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            else
            {
                if (other == null)
                {
                    return false;
                }
                else
                {
                    return other.PictureUri.AbsoluteUri.Equals(PictureUri.AbsoluteUri, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        public override bool Equals(object obj)
        {
            return obj is BackgroundPicture Item && Equals(Item);
        }

        public override int GetHashCode()
        {
            return PictureUri.GetHashCode();
        }

        public static async Task<BackgroundPicture> CreateAsync(Uri PictureUri)
        {
            StorageFile NewImageFile = await StorageFile.GetFileFromApplicationUriAsync(PictureUri);

            BitmapImage Bitmap = new BitmapImage()
            {
                DecodePixelHeight = 90,
                DecodePixelWidth = 160
            };

            using (IRandomAccessStream Stream = await NewImageFile.OpenAsync(FileAccessMode.Read))
            {
                await Bitmap.SetSourceAsync(Stream);
            }

            return new BackgroundPicture(Bitmap, PictureUri);
        }

        /// <summary>
        /// 初始化BackgroundPicture
        /// </summary>
        /// <param name="Picture">图片</param>
        /// <param name="PictureUri">图片Uri</param>
        private BackgroundPicture(BitmapImage Thumbnail, Uri PictureUri)
        {
            this.Thumbnail = Thumbnail;
            this.PictureUri = PictureUri;
        }
    }
}
