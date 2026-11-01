using System;
using Godot;

namespace WobbleHero.UI;

/// <summary>
/// Reusable UI component displaying the companion app connection URL
/// and an on-screen QR code for zero-dependency smartphone pairing.
/// </summary>
public partial class CompanionQrCard : PanelContainer
{
    [Export] public TextureRect? QrTextureRect { get; set; }
    [Export] public Label? UrlLabel { get; set; }
    [Export] public Label? HeaderLabel { get; set; }

    public override void _Ready()
    {
        QrTextureRect ??= GetNodeOrNull<TextureRect>("VBox/QrTextureRect");
        UrlLabel ??= GetNodeOrNull<Label>("VBox/UrlLabel");
        HeaderLabel ??= GetNodeOrNull<Label>("VBox/HeaderLabel");
    }

    public void SetUrl(string url)
    {
        if (UrlLabel != null)
        {
            UrlLabel.Text = url;
        }

        if (QrTextureRect != null && !string.IsNullOrEmpty(url))
        {
            try
            {
                var texture = QrCodeGenerator.GenerateTexture(url, border: 2);
                QrTextureRect.Texture = texture;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[CompanionQrCard] QR generation error: {ex.Message}");
            }
        }
    }
}
