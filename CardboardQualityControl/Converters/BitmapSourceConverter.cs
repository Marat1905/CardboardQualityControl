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

            var width = mat.Width;
            var height = mat.Height;
            var step = (int)mat.Step();
            var data = mat.Data;

            var bitmap = new WriteableBitmap(width, height, 96, 96, format, null);
            bitmap.Lock();

            try
            {
                var buffer = bitmap.BackBuffer;
                var bufferSize = height * step;

                if (step == width * format.BitsPerPixel / 8)
                {
                    // Если данные непрерывны, копируем всё сразу
                    CopyMemory(buffer, data, (uint)bufferSize);
                }
                else
                {
                    // Иначе копируем построчно
                    for (int y = 0; y < height; y++)
                    {
                        var src = new IntPtr(data.ToInt64() + y * step);
                        var dst = new IntPtr(buffer.ToInt64() + y * bitmap.BackBufferStride);
                        CopyMemory(dst, src, (uint)(width * format.BitsPerPixel / 8));
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

        [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = false)]
        private static extern void CopyMemory(IntPtr dest, IntPtr src, uint count);
    }
}