using Microsoft.Toolkit.Uwp.Notifications;
using System.Diagnostics;
using Uttu.Models;

namespace Uttu.Services;

public class ToastNotificationService
{
    public event Action<int>? OnApproveRequested;
    public event Action? OnOpenRequested;

    public void Initialize()
    {
        try
        {
            // Interactive toast listener (Windows 10 Action Center)
            ToastNotificationManagerCompat.OnActivated += toastArgs =>
            {
                var args = ToastArguments.Parse(toastArgs.Argument);
                string action = args.TryGetValue("action", out var act) ? act : "open";

                Debug.WriteLine($"[ToastNotificationService] Notification activated with action: {action}");

                if (action == "approve")
                {
                    if (args.TryGetValue("postId", out var idStr) && int.TryParse(idStr, out int postId))
                    {
                        OnApproveRequested?.Invoke(postId);
                    }
                }
                else if (action == "open")
                {
                    OnOpenRequested?.Invoke();
                }
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ToastNotificationService] Failed to register ToastNotificationManagerCompat: {ex.Message}");
        }
    }

    public void ShowDraftsReadyToast(List<PostDraft> drafts)
    {
        if (drafts == null || drafts.Count == 0) return;

        try
        {
            var firstDraft = drafts.First();
            string title = $"Uttu · {drafts.Count} new drafts ready";
            string preview = $"[{firstDraft.Platform}] {firstDraft.Content.Replace("\n", " ")}";
            if (preview.Length > 120)
            {
                preview = preview.Substring(0, 117) + "...";
            }

            var builder = new ToastContentBuilder()
                .AddText(title)
                .AddText(preview)
                .AddButton(new ToastButton()
                    .SetContent("Approve Now")
                    .AddArgument("action", "approve")
                    .AddArgument("postId", firstDraft.Id.ToString())
                    .SetBackgroundActivation())
                .AddButton(new ToastButton()
                    .SetContent("Open Uttu")
                    .AddArgument("action", "open"))
                .AddButton(new ToastButton()
                    .SetContent("Dismiss")
                    .AddArgument("action", "dismiss"));

            builder.Show();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ToastNotificationService] Error displaying native Toast: {ex.Message}");
        }
    }

    public void ShowSuccessToast(string title, string message)
    {
        try
        {
            new ToastContentBuilder()
                .AddText(title)
                .AddText(message)
                .Show();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ToastNotificationService] Error displaying success Toast: {ex.Message}");
        }
    }
}
