using System.ComponentModel;

namespace PlutoFramework.Model
{
    /// <summary>
    /// Tracks open popups in the order they were opened, so a back action can dismiss the
    /// most recently opened popup first (LIFO) before the page itself navigates back.
    /// </summary>
    /// <remarks>
    /// Popups are wired up with <see cref="TrackPopup"/> at app startup (one call per popup
    /// view model). The view model's <c>IsVisible</c> flag is the single source of truth for
    /// a popup's visibility - the popup views bind it one-way - so every show and close path
    /// (view-model flag, close button, swipe-down, <see cref="ISetToDefault"/>) reaches the
    /// manager through the same event. Several view instances of the same popup (e.g. the
    /// global popups hosted by the page template, one instance per page) collapse into a
    /// single entry, because entries are keyed by the popup's view model.
    /// </remarks>
    public static class PopupManager
    {
        // Open popups, oldest first; the most recently opened one is at the end.
        private static readonly List<IPopup> _openPopups = new();

        // View models already wired with <see cref="TrackPopup"/>.
        private static readonly HashSet<IPopup> _trackedPopups = new();

        /// <summary>
        /// Starts tracking a popup view model so <see cref="TryCloseTopPopup"/> can dismiss
        /// it. Safe to call more than once per popup.
        /// </summary>
        public static void TrackPopup(IPopup popup)
        {
            if (popup is not INotifyPropertyChanged inpc)
            {
                return;
            }

            lock (_trackedPopups)
            {
                if (!_trackedPopups.Add(popup))
                {
                    return;
                }
            }

            inpc.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName != nameof(IPopup.IsVisible) || sender is not IPopup trackedPopup)
                {
                    return;
                }

                if (trackedPopup.IsVisible)
                {
                    MarkVisible(trackedPopup);
                }
                else
                {
                    MarkHidden(trackedPopup);
                }
            };

            if (popup.IsVisible)
            {
                MarkVisible(popup);
            }
        }

        /// <summary>
        /// Records <paramref name="popup"/> as the most recently opened popup. No-op if the
        /// popup is already tracked or not actually visible.
        /// </summary>
        public static void MarkVisible(IPopup popup)
        {
            lock (_openPopups)
            {
                if (popup.IsVisible && !_openPopups.Contains(popup))
                {
                    _openPopups.Add(popup);
                }
            }
        }

        /// <summary>
        /// Records <paramref name="popup"/> as hidden. No-op if it is not tracked.
        /// </summary>
        public static void MarkHidden(IPopup popup)
        {
            lock (_openPopups)
            {
                _openPopups.Remove(popup);
            }
        }

        /// <summary>
        /// Whether at least one popup is currently open.
        /// </summary>
        public static bool HasOpenPopups
        {
            get
            {
                lock (_openPopups)
                {
                    return _openPopups.Count > 0;
                }
            }
        }

        /// <summary>
        /// Hides the most recently opened visible popup, if any, using the same semantics as
        /// the card's close path (<c>IsVisible = false</c> plus <see cref="ISetToDefault"/>).
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if a popup was closed, i.e. the back action should be
        /// consumed; <see langword="false"/> if no popup is open and the back action should
        /// proceed normally.
        /// </returns>
        public static bool TryCloseTopPopup()
        {
            IPopup? popup;

            lock (_openPopups)
            {
                // Drop entries hidden through other means (close button, swipe-down,
                // SetToDefault) so we never act on a popup that is no longer showing.
                while (_openPopups.Count > 0 && !_openPopups[^1].IsVisible)
                {
                    _openPopups.RemoveAt(_openPopups.Count - 1);
                }

                if (_openPopups.Count == 0)
                {
                    return false;
                }

                popup = _openPopups[^1];
                _openPopups.RemoveAt(_openPopups.Count - 1);
            }

            popup.IsVisible = false;
            (popup as ISetToDefault)?.SetToDefault();

            return true;
        }
    }
}
