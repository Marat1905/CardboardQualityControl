using OpenCvSharp;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;

namespace CardboardQualityControl.Converters
{
    public static class BitmapSourceConverter
    {
        public static BitmapSource ToBitmapSource(Mat mat)
        {
            if (mat == null)
                throw new ArgumentNullException(nameof(mat));
            if (mat.Empty())
                return null;

            try
            {
                PixelFormat format;
                var matType = mat.Type();

                // Используем if-else вместо switch, так как MatType не является enum
                if (matType == MatType.CV_8UC1)
                {
                    format = PixelFormats.Gray8;
                }
                else if (matType == MatType.CV_8UC3)
                {
                    format = PixelFormats.Bgr24;
                }
                else if (matType == MatType.CV_8UC4)
                {
                    format = PixelFormats.Bgra32;
                }
                else
                {
                    throw new NotSupportedException($"Unsupported Mat type: {matType}");
                }

                int width = mat.Width;
                int height = mat.Height;
                int step = (int)mat.Step();
                IntPtr data = mat.Data;

                // Создаем WriteableBitmap
                var bitmap = new WriteableBitmap(width, height, 96, 96, format, null);

                bitmap.Lock();

                try
                {
                    IntPtr buffer = bitmap.BackBuffer;
                    int bufferSize = height * step;

                    if (step == width * format.BitsPerPixel / 8)
                    {
                        // Копируем данные целиком
                        NativeMethods.memcpy(buffer, data, (uint)bufferSize);
                    }
                    else
                    {
                        // Копируем построчно
                        for (int y = 0; y < height; y++)
                        {
                            IntPtr src = new IntPtr(data.ToInt64() + y * step);
                            IntPtr dst = new IntPtr(buffer.ToInt64() + y * bitmap.BackBufferStride);
                            NativeMethods.memcpy(dst, src, (uint)(width * format.BitsPerPixel / 8));
                        }
                    }

                    bitmap.AddDirtyRect(new System.Windows.Int32Rect(0, 0, width, height));
                }
                finally
                {
                    bitmap.Unlock();
                }

                return bitmap;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to convert Mat to BitmapSource", ex);
            }
        }

        // Альтернативный метод через MemoryStream
        public static BitmapSource ToBitmapSourceAlternative(Mat mat)
        {
            if (mat == null || mat.Empty())
                return null;

            try
            {
                // Конвертируем Mat в массив байтов
                byte[] imageBytes = mat.ToBytes(".jpg");

                using (var memory = new System.IO.MemoryStream(imageBytes))
                {
                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = memory;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();

                    return bitmapImage;
                }
            }
            catch
            {
                // Fallback: используем BMP если JPEG не сработал
                try
                {
                    byte[] imageBytes = mat.ToBytes(".bmp");

                    using (var memory = new System.IO.MemoryStream(imageBytes))
                    {
                        var bitmapImage = new BitmapImage();
                        bitmapImage.BeginInit();
                        bitmapImage.StreamSource = memory;
                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapImage.EndInit();
                        bitmapImage.Freeze();

                        return bitmapImage;
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Failed to convert Mat to BitmapSource", ex);
                }
            }
        }
    }

    internal static class NativeMethods
    {
        [DllImport("msvcrt.dll", EntryPoint = "memcpy", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr memcpy(IntPtr dest, IntPtr src, uint count);
    }
}