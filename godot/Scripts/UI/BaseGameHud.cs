using System;
using Godot;
using WobbleHero.Core;

namespace WobbleHero.UI;

/// <summary>
/// Base class for all game-specific CanvasLayer HUDs.
/// Encapsulates common components: Companion QR card, Board tilt indicator,
/// and life-cycle telemetry updates.
/// </summary>
public abstract partial class BaseGameHud : CanvasLayer
{
    [Export] public CompanionQrCard? QrCard { get; set; }
    [Export] public BoardTiltIndicator? TiltIndicator { get; set; }
    [Export] public Label? BannerLabel { get; set; }

    protected GameManager? Manager;
    private float _bannerTimer;

    public virtual void Initialize(GameManager manager)
    {
        Manager = manager;

        QrCard ??= GetNodeOrNull<CompanionQrCard>("%CompanionQrCard");
        TiltIndicator ??= GetNodeOrNull<BoardTiltIndicator>("%BoardTiltIndicator");
        BannerLabel ??= GetNodeOrNull<Label>("%BannerLabel");

        if (Manager?.WebServer != null)
        {
            SetCompanionUrl(Manager.WebServer.GetCompanionUrl());
        }
    }

    public virtual void SetCompanionUrl(string url)
    {
        QrCard?.SetUrl(url);
    }

    public virtual void ShowBannerMessage(string message, float duration = 1.5f)
    {
        if (BannerLabel != null)
        {
            BannerLabel.Text = message;
            BannerLabel.Visible = !string.IsNullOrEmpty(message);
            _bannerTimer = duration;
        }
    }

    public virtual void UpdateState(BoardState board, IGameMode game)
    {
        TiltIndicator?.UpdateState(board);

        if (_bannerTimer > 0f)
        {
            _bannerTimer -= (float)GetProcessDeltaTime();
            if (_bannerTimer <= 0f && BannerLabel != null)
            {
                BannerLabel.Visible = false;
            }
        }
    }
}
