using SkiaSharp;
using System.IO;
using Uttu.Models;

namespace Uttu.Services;

public class CardRendererService
{
    private readonly string _cacheDirectory;

    public CardRendererService(string? cacheDirectory = null)
    {
        _cacheDirectory = cacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Uttu",
            "cache"
        );

        if (!Directory.Exists(_cacheDirectory))
        {
            Directory.CreateDirectory(_cacheDirectory);
        }
    }

    public string RenderSocialCard(PostDraft post, string authorName = "Fundador / Criador", string authorHandle = "@usuario", bool darkMode = true)
    {
        const int width = 1600;
        const int height = 900;

        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);

        // Paleta Flat Design Windows 10
        var bgColor = darkMode ? SKColor.Parse("#0C0D0E") : SKColor.Parse("#F3F4F6");
        var cardBorderColor = darkMode ? SKColor.Parse("#2B2B2B") : SKColor.Parse("#D0D0D0");
        var textColor = darkMode ? SKColor.Parse("#FFFFFF") : SKColor.Parse("#111827");
        var secondaryTextColor = darkMode ? SKColor.Parse("#A0A0A0") : SKColor.Parse("#6B7280");
        var accentColor = SKColor.Parse("#0078D7"); // Windows Blue
        var dividerColor = darkMode ? SKColor.Parse("#202225") : SKColor.Parse("#E5E7EB");

        // 1. Fundo
        canvas.Clear(bgColor);

        // 2. Borda externa nítida de 1px (sem arredondamento / flat clássico)
        using (var borderPaint = new SKPaint
        {
            Color = cardBorderColor,
            IsStroke = true,
            StrokeWidth = 2,
            IsAntialias = true
        })
        {
            canvas.DrawRect(new SKRect(30, 30, width - 30, height - 30), borderPaint);
        }

        // 3. Barra de destaque superior (Accent line 4px)
        using (var accentPaint = new SKPaint
        {
            Color = accentColor,
            IsStroke = false,
            IsAntialias = true
        })
        {
            canvas.DrawRect(new SKRect(32, 32, width - 32, 38), accentPaint);
        }

        // 4. Cabeçalho: Autor, Handle e Badge de Rede Social
        float headerY = 110;
        float marginX = 90;

        // Ícone Avatar Flat [U]
        using (var avatarBg = new SKPaint { Color = darkMode ? SKColor.Parse("#1E1E1E") : SKColor.Parse("#E0E0E0"), IsAntialias = true })
        using (var avatarBorder = new SKPaint { Color = accentColor, IsStroke = true, StrokeWidth = 2, IsAntialias = true })
        using (var avatarText = new SKPaint { Color = accentColor, TextSize = 34, IsAntialias = true, Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold) })
        {
            var avatarRect = new SKRect(marginX, headerY - 50, marginX + 64, headerY + 14);
            canvas.DrawRect(avatarRect, avatarBg);
            canvas.DrawRect(avatarRect, avatarBorder);
            canvas.DrawText("U", marginX + 20, headerY - 6, avatarText);
        }

        // Nome do Autor
        using (var authorPaint = new SKPaint
        {
            Color = textColor,
            TextSize = 32,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
        })
        {
            canvas.DrawText(authorName, marginX + 85, headerY - 14, authorPaint);
        }

        // Handle do Autor (@handle)
        using (var handlePaint = new SKPaint
        {
            Color = secondaryTextColor,
            TextSize = 22,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Consolas", SKFontStyle.Normal)
        })
        {
            canvas.DrawText(authorHandle, marginX + 85, headerY + 14, handlePaint);
        }

        // Badge de Plataforma (à direita)
        string badgeText = post.PlatformBadge.ToUpperInvariant();
        using (var badgeBorder = new SKPaint { Color = cardBorderColor, IsStroke = true, StrokeWidth = 1, IsAntialias = true })
        using (var badgeFill = new SKPaint { Color = darkMode ? SKColor.Parse("#1E1E1E") : SKColor.Parse("#FFFFFF"), IsAntialias = true })
        using (var badgeTextPaint = new SKPaint { Color = accentColor, TextSize = 18, IsAntialias = true, Typeface = SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold) })
        {
            float badgeWidth = badgeTextPaint.MeasureText(badgeText) + 36;
            float badgeRight = width - marginX;
            float badgeLeft = badgeRight - badgeWidth;
            var bRect = new SKRect(badgeLeft, headerY - 35, badgeRight, headerY + 10);
            canvas.DrawRect(bRect, badgeFill);
            canvas.DrawRect(bRect, badgeBorder);
            canvas.DrawText(badgeText, badgeLeft + 18, headerY - 7, badgeTextPaint);
        }

        // Linha divisória
        using (var divPaint = new SKPaint { Color = dividerColor, StrokeWidth = 1, IsAntialias = true })
        {
            canvas.DrawLine(marginX, headerY + 45, width - marginX, headerY + 45, divPaint);
        }

        // 5. Corpo do Conteúdo com quebra automática de linha
        float contentTop = headerY + 115;
        float contentBottom = height - 160;
        float maxContentWidth = width - (marginX * 2);

        // Ajusta tamanho da fonte dinamicamente baseado na extensão do texto
        int textLen = post.Content.Length;
        float baseFontSize = textLen switch
        {
            < 140 => 46f,
            < 280 => 40f,
            < 600 => 34f,
            < 1200 => 28f,
            _ => 24f
        };
        float lineHeight = baseFontSize * 1.55f;

        using (var bodyPaint = new SKPaint
        {
            Color = textColor,
            TextSize = baseFontSize,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)
        })
        using (var hookPaint = new SKPaint
        {
            Color = textColor,
            TextSize = baseFontSize,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
        })
        {
            string[] rawParagraphs = post.Content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            float currentY = contentTop;
            bool isFirstLine = true;

            foreach (var para in rawParagraphs)
            {
                if (currentY + lineHeight > contentBottom)
                {
                    // Truncar com reticências limpas se passar da área segura do cartão
                    canvas.DrawText("...", marginX, currentY, bodyPaint);
                    break;
                }

                if (string.IsNullOrWhiteSpace(para))
                {
                    currentY += lineHeight * 0.6f; // Parágrafo vazio
                    continue;
                }

                var wrappedLines = WrapText(para, maxContentWidth, isFirstLine ? hookPaint : bodyPaint);
                foreach (var line in wrappedLines)
                {
                    if (currentY + lineHeight > contentBottom)
                    {
                        canvas.DrawText("...", marginX, currentY, bodyPaint);
                        break;
                    }

                    var paintToUse = isFirstLine ? hookPaint : bodyPaint;
                    canvas.DrawText(line, marginX, currentY, paintToUse);
                    currentY += lineHeight;
                    isFirstLine = false;
                }
            }
        }

        // 6. Rodapé: Divisória e Metadados Flat
        float footerY = height - 70;
        using (var divPaint = new SKPaint { Color = dividerColor, StrokeWidth = 1, IsAntialias = true })
        {
            canvas.DrawLine(marginX, footerY - 30, width - marginX, footerY - 30, divPaint);
        }

        using (var footerPaint = new SKPaint
        {
            Color = secondaryTextColor,
            TextSize = 20,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Consolas", SKFontStyle.Normal)
        })
        using (var footerAccent = new SKPaint
        {
            Color = accentColor,
            TextSize = 20,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold)
        })
        {
            canvas.DrawText("UTTU DESKTOP", marginX, footerY, footerAccent);
            canvas.DrawText(" · COPILOTO SOCIAL NATIVO (WINDOWS 10)", marginX + 145, footerY, footerPaint);

            string metrics = $"{post.CharCount} CARACTERES · {DateTime.Now:dd/MM/yyyy}";
            float metricsWidth = footerPaint.MeasureText(metrics);
            canvas.DrawText(metrics, width - marginX - metricsWidth, footerY, footerPaint);
        }

        // Salvar em arquivo PNG no cache
        string fileName = $"card_{post.Id}_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.png";
        string filePath = Path.Combine(_cacheDirectory, fileName);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var fileStream = File.OpenWrite(filePath);
        data.SaveTo(fileStream);

        return filePath;
    }

    private static List<string> WrapText(string text, float maxWidth, SKPaint paint)
    {
        var lines = new List<string>();
        string[] words = text.Split(' ');
        string currentLine = "";

        foreach (var word in words)
        {
            string testLine = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
            float measuredWidth = paint.MeasureText(testLine);

            if (measuredWidth > maxWidth)
            {
                if (!string.IsNullOrEmpty(currentLine))
                {
                    lines.Add(currentLine);
                }
                currentLine = word;
            }
            else
            {
                currentLine = testLine;
            }
        }

        if (!string.IsNullOrEmpty(currentLine))
        {
            lines.Add(currentLine);
        }

        return lines;
    }
}
