using SkiaSharp;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace Ergon.Services
{
    public class MediaService
    {
        public async Task<string> ProcessAndSaveAttachmentAsync(FileResult result)
        {
            string[] imageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" };
            string ext = Path.GetExtension(result.FileName).ToLowerInvariant();
            bool isImage = imageExtensions.Contains(ext);

            if (isImage)
            {
                return await OttimizzaImmagineAsync(result);
            }
            else
            {
                FileInfo info = new(result.FullPath);
                long kilobytes = info.Length / 1000;

                if (kilobytes > 10240) // max 10mb
                {
                    throw new InvalidOperationException("L'allegato è troppo grande: non può superare i 10MB");
                }

                string directoryDestinazione = Path.Combine(FileSystem.AppDataDirectory, "ScontriniPersistenti");
                if (!Directory.Exists(directoryDestinazione)) Directory.CreateDirectory(directoryDestinazione);

                string percorsoFinale = Path.Combine(directoryDestinazione, result.FileName);

                using Stream sourceStream = await result.OpenReadAsync();
                using FileStream localFileStream = File.OpenWrite(percorsoFinale);
                await sourceStream.CopyToAsync(localFileStream);

                return percorsoFinale;
            }
        }

        private async Task<string> OttimizzaImmagineAsync(FileResult fotoResult)
        {
            try
            {
                string directoryDestinazione = Path.Combine(FileSystem.AppDataDirectory, "ScontriniPersistenti");
                if (!Directory.Exists(directoryDestinazione))
                {
                    Directory.CreateDirectory(directoryDestinazione);
                }

                SKEncodedImageFormat formatoOutput = SKEncodedImageFormat.Jpeg;
                string estensioneFinale = ".jpg";

                string nomeFile = $"scontrino_{DateTime.Now:yyyyMMdd_HHmmss}{estensioneFinale}";
                string destinazionePath = Path.Combine(directoryDestinazione, nomeFile);

                using (Stream streamOrigine = await fotoResult.OpenReadAsync())
                using (var codec = SKCodec.Create(streamOrigine))
                {
                    if (codec != null)
                    {
                        SKEncodedOrigin origin = codec.EncodedOrigin;

                        using (var originalBitmap = SKBitmap.Decode(codec))
                        {
                            if (originalBitmap != null)
                            {
                                float maxDimension = 1920f;
                                float width = originalBitmap.Width;
                                float height = originalBitmap.Height;

                                int newWidth = originalBitmap.Width;
                                int newHeight = originalBitmap.Height;

                                // Calcolo proporzioni
                                if (width > maxDimension || height > maxDimension)
                                {
                                    if (width > height)
                                    {
                                        height = (maxDimension / width) * height;
                                        width = maxDimension;
                                    }
                                    else
                                    {
                                        width = (maxDimension / height) * width;
                                        height = maxDimension;
                                    }

                                    newWidth = (int)width;
                                    newHeight = (int)height;
                                }

                                using (var resizedBitmap = originalBitmap.Resize(new SKImageInfo(newWidth, newHeight), SKSamplingOptions.Default))
                                {
                                    var bitmapElaborata = resizedBitmap ?? originalBitmap;

                                    SKBitmap bitmapFinale = GestisciRotazioneExif(bitmapElaborata, origin);

                                    using (var image = SKImage.FromBitmap(bitmapFinale))
                                    {
                                        int quality = 80;
                                        SKData data = image.Encode(formatoOutput, quality);

                                        while (data != null && data.Size > 5242880 && quality > 20)
                                        {
                                            data.Dispose();
                                            quality -= 20;

                                            data = image.Encode(formatoOutput, quality);
                                        }

                                        if (data != null)
                                        {
                                            if (data.Size > 5242880)
                                            {
                                                data.Dispose();
                                                throw new InvalidOperationException("L'immagine è troppo complessa e non può essere compressa sotto i 5MB.");
                                            }

                                            using (var streamDestinazione = File.Create(destinazionePath))
                                            {
                                                data.SaveTo(streamDestinazione);
                                            }

                                            data.Dispose();

                                            if (bitmapFinale != bitmapElaborata)
                                            {
                                                bitmapFinale.Dispose();
                                            }
                                            return destinazionePath;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Impossibile ottimizzare l'immagine: {ex.Message}");
            }

            return fotoResult.FullPath;
        }

        private static SKBitmap GestisciRotazioneExif(SKBitmap bitmap, SKEncodedOrigin origin)
        {
            SKBitmap ruotata;
            switch (origin)
            {
                case SKEncodedOrigin.BottomRight:
                    ruotata = new SKBitmap(bitmap.Width, bitmap.Height);
                    using (var canvas = new SKCanvas(ruotata))
                    {
                        canvas.Translate(bitmap.Width, bitmap.Height);
                        canvas.RotateDegrees(180);
                        canvas.DrawBitmap(bitmap, 0, 0);
                    }
                    return ruotata;

                case SKEncodedOrigin.RightTop:
                    ruotata = new SKBitmap(bitmap.Height, bitmap.Width);
                    using (var canvas = new SKCanvas(ruotata))
                    {
                        canvas.Translate(bitmap.Height, 0);
                        canvas.RotateDegrees(90);
                        canvas.DrawBitmap(bitmap, 0, 0);
                    }
                    return ruotata;

                case SKEncodedOrigin.LeftBottom:
                    ruotata = new SKBitmap(bitmap.Height, bitmap.Width);
                    using (var canvas = new SKCanvas(ruotata))
                    {
                        canvas.Translate(0, bitmap.Width);
                        canvas.RotateDegrees(270);
                        canvas.DrawBitmap(bitmap, 0, 0);
                    }
                    return ruotata;

                default:
                    return bitmap;
            }
        }
    }
}