using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RailworksDownloader
{
    internal class DialogQueueItem
    {
        public readonly int TimeCreated;
        public readonly byte Importance;
        public bool Hidden = false;
        public readonly Action<bool> Callback;
        public readonly ContentDialog ContentDialog;

        public DialogQueueItem(int timeCreated, byte importance, ContentDialog contentDialog, Action<bool> callback = null)
        {
            TimeCreated = timeCreated;
            Importance = importance;
            ContentDialog = contentDialog;
            Callback = callback;
        }
    }

    internal class DialogQueue
    {
        private List<DialogQueueItem> items = new List<DialogQueueItem>();
        private DialogQueueItem oldItem;

        public void AddDialog(DialogQueueItem item)
        {
            items.Add(item);
            RefreshDialogs();
        }

        public void AddDialog(int timeCreated, byte importance, ContentDialog contentDialog, Action<bool> callback = null)
        {
            DialogQueueItem item = new DialogQueueItem(timeCreated, importance, contentDialog, callback);
            AddDialog(item);
        }

        public void RemoveDialog(ContentDialog contentDialog)
        {
            items.Remove(items.FirstOrDefault(x => x.ContentDialog == contentDialog));
            RefreshDialogs();
        }

        private async void RefreshDialogs()
        {
            items = items.OrderByDescending(x => x.Importance).ThenBy(x => x.TimeCreated).ToList();
            DialogQueueItem newItem = items.Count > 0 ? items[0] : null;

            if (oldItem?.ContentDialog == newItem?.ContentDialog)
                return;

            if (oldItem?.ContentDialog != null)
            {
                oldItem.ContentDialog.Hide();
                oldItem.Hidden = true;
            }

            oldItem = newItem;
            if (newItem?.ContentDialog != null)
            {
                newItem.Hidden = false;
                ContentDialogResult result = await newItem?.ContentDialog.ShowAsync();
                Action<bool> callback = newItem?.Callback;
                if (!newItem.Hidden)
                {
                    if (callback != null)
                        callback(result == ContentDialogResult.Primary);
                    RemoveDialog(newItem?.ContentDialog);
                }
            }
        }
    }
}
