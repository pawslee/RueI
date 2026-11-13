namespace RueI.API;

using System;
using System.Collections.Generic;
using System.Linq;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;
using LabApi.Features.Wrappers;
using PlayerRoles.Spectating;
using RoundRestarting;

using RueI.API.Elements;
using RueI.API.Parsing;
using RueI.Utils.Collections;
using RueI.Utils.Extensions;
using UnityEngine;

/// <summary>
/// Represents a display for a specific player.
/// </summary>
/// <remarks>
/// Every player has a <see cref="RueDisplay"/>, which is used by RueI to keep track of their elements and send them hints.
/// </remarks>
public sealed class RueDisplay
{
    private static readonly Dictionary<ReferenceHub, RueDisplay> Displays = new();

    // dictionary is sorted by z-index in ascending order
    private readonly ValueSortedDictionary<Tag, StoredElement> elements = new();
    private readonly MinHeap<ExpiryInfo> expirationHeap = new(); // for when elements expire - smallest expires most recently
    private readonly MinHeap<TimeSpan> updateIntervalHeap = new(); // for dynamicelements, etc
    private readonly HashSet<Tag> hiddenTags = new();
    private readonly HashSet<RueDisplay> spectators = new();
    private readonly ReferenceHub hub;

    private RueDisplay? spectatedDisplay;
    private bool updateNextFrame = false;

    // forcedUpdate represents either an automatic update by a DynamicElement, etc.
    // or an update scheduled after an external hint is shown
    private float forcedUpdate = float.PositiveInfinity;
    private bool forcedIsExternal = false;

    private RueDisplay(ReferenceHub hub)
    {
        this.hub = hub;
    }

    /// <summary>
    /// Gets a display for a specific <see cref="ReferenceHub"/>.
    /// </summary>
    /// <param name="hub">The <see cref="ReferenceHub"/> to get the display of.</param>
    /// <returns>The player's corresponding display.</returns>
    public static RueDisplay Get(ReferenceHub hub)
    {
        if (hub == null)
        {
            throw new ArgumentNullException(nameof(hub));
        }

        return Displays.GetOrAdd(hub, () => new RueDisplay(hub));
    }

    /// <summary>
    /// Gets a display for a specific <see cref="Player"/>.
    /// </summary>
    /// <param name="player">The <see cref="ReferenceHub"/> to get the display of.</param>
    /// <returns>The player's corresponding display.</returns>
    public static RueDisplay Get(Player player)
    {
        if (player == null)
        {
            throw new ArgumentNullException(nameof(player));
        }

        return Get(player.ReferenceHub);
    }

    /// <summary>
    /// Adds an <see cref="Element"/> with a unique <see cref="Tag"/> to this <see cref="RueDisplay"/>.
    /// </summary>
    /// <param name="tag">A <see cref="Tag"/> to use. If an <see cref="Element"/> already has this tag, it will be replaced.</param>
    /// <param name="element">The <see cref="Element"/> to add.</param>
    public void Show(Tag tag, Element element) => this.AddElement(
        tag ?? throw new ArgumentNullException(nameof(tag)),
        element ?? throw new ArgumentNullException(nameof(element)),
        float.NaN);

    /// <summary>
    /// Adds an <see cref="Element"/> with a unique <see cref="Tag"/> to this <see cref="RueDisplay"/> for a certain duration.
    /// </summary>
    /// <param name="tag"><inheritdoc cref="Show(Tag, Element)" path="/param[@name='tag']"/></param>
    /// <param name="element">The <see cref="Element"/> to add.</param>
    /// <param name="duration">The duration to show the element, in seconds.</param>
    public void Show(Tag tag, Element element, float duration) => this.AddElement(
        tag ?? throw new ArgumentNullException(nameof(tag)),
        element ?? throw new ArgumentNullException(nameof(element)),
        Time.timeSinceLevelLoad + duration);

    /// <summary>
    /// Adds an <see cref="Element"/> to this <see cref="RueDisplay"/> for a certain duration.
    /// </summary>
    /// <param name="element">The <see cref="Element"/> to add.</param>
    /// <param name="duration"><inheritdoc cref="Show(Tag, Element, float)" path="/param[@name='duration']"/></param>
    /// <remarks>
    /// When adding an element using this method, it will not have a <see cref="Tag"/>.
    /// Therefore, there is no way to remove the element early.
    /// </remarks>
    public void Show(Element element, float duration) => this.AddElement(
        new(), // new() results in a tag that is only equal to itself
        element ?? throw new ArgumentNullException(nameof(element)),
        Time.timeSinceLevelLoad + duration);

    /// <summary>
    /// Adds an <see cref="Element"/> with a unique <see cref="Tag"/> to this <see cref="RueDisplay"/> for a certain period of time.
    /// </summary>
    /// <param name="tag"><inheritdoc cref="Show(Tag, Element)" path="/param[@name='tag']"/></param>
    /// <param name="element">The <see cref="Element"/> to add.</param>
    /// <param name="duration">A <see cref="TimeSpan"/> indicating how long to show the <see cref="Element"/> for.</param>
    public void Show(Tag tag, Element element, TimeSpan duration) => this.Show(tag, element, (float)duration.TotalSeconds);

    /// <summary>
    /// <inheritdoc cref="Show(Element, float)" path="/summary"/>
    /// </summary>
    /// <param name="element">The <see cref="Element"/> to add.</param>
    /// <param name="duration"><inheritdoc cref="Show(Tag, Element, TimeSpan)" path="/param[@name='duration']"/></param>
    /// <remarks>
    /// <inheritdoc cref="Show(Element, float)" path="/remarks"/>
    /// </remarks>
    public void Show(Element element, TimeSpan duration) => this.Show(element, (float)duration.TotalSeconds);

    /// <summary>
    /// Removes the element with the given tag from the <see cref="RueDisplay"/>, if there is one.
    /// </summary>
    /// <param name="tag">The tag for which remove the associated <see cref="Element"/>.</param>
    public void Remove(Tag tag)
    {
        if (this.elements.Remove(tag, out StoredElement element))
        {
            if (element.ExpiryNode != null)
            {
                this.expirationHeap.Remove(element.ExpiryNode);
            }

            if (element.UpdateNode != null)
            {
                this.updateIntervalHeap.Remove(element.UpdateNode);
            }

            // if there's a forced update (i.e. another hint is hiding RueI,
            // there's no reason to update the display just to remove one hint
            // (since it's already not visible)
            if (!this.forcedIsExternal)
            {
                this.updateNextFrame = true;
            }
        }

        // no need to update if we didn't actually remove anything
    }

    /// <summary>
    /// Sets the visibility of a <see cref="Tag"/>.
    /// </summary>
    /// <param name="tag">The <see cref="Tag"/> to set the visibility of.</param>
    /// <param name="isVisible">Whether elements with the <see cref="Tag"/> should be visible.</param>
    /// <remarks>
    /// This method sets the visibility of any current or future elements with the <see cref="Tag"/>.
    /// </remarks>
    public void SetVisible(Tag tag, bool isVisible)
    {
        if (isVisible)
        {
            this.hiddenTags.Remove(tag);
        }
        else
        {
            this.hiddenTags.Add(tag);
        }

        if (!this.forcedIsExternal)
        {
            this.updateNextFrame = true;
        }
    }

    /// <summary>
    /// Updates the display, refreshing any <see cref="DynamicElement"/>.
    /// </summary>
    public void Update()
    {
        this.updateNextFrame = true;
    }

    /// <summary>
    /// Registers the <see cref="RueDisplay"/> events.
    /// </summary>
    internal static void RegisterEvents()
    {
        // i use the base game alternatives because these are
        // more lightweight
        StaticUnityMethods.OnUpdate += CheckDisplays;
        ReferenceHub.OnPlayerRemoved += RemoveHub;
        RoundRestart.OnRestartTriggered += Displays.Clear;

        PlayerEvents.ChangedSpectator += OnSpectatorChanged;
        PlayerEvents.ChangedRole += OnRoleChanged;
    }

    /// <summary>
    /// Unregisters the <see cref="RueDisplay"/> events.
    /// </summary>
    internal static void UnregisterEvents()
    {
        StaticUnityMethods.OnUpdate -= CheckDisplays;
        ReferenceHub.OnPlayerRemoved -= RemoveHub;
        RoundRestart.OnRestartTriggered -= Displays.Clear;

        PlayerEvents.ChangedSpectator -= OnSpectatorChanged;
        PlayerEvents.ChangedRole -= OnRoleChanged;
    }

    /// <summary>
    /// Updates the display after the given number of seconds.
    /// </summary>
    /// <param name="duration">The time to wait before updating.</param>
    internal void SetUpdateIn(float duration)
    {
        this.forcedUpdate = Time.timeSinceLevelLoad + duration;
        this.forcedIsExternal = true;
    }

    // not a lambda so we can later unregister
    private static void RemoveHub(ReferenceHub hub)
    {
        if (Displays.TryGetValue(hub, out RueDisplay display))
        {
            display.StopSpectating();
            display.ClearSpectators();
            Displays.Remove(hub);
        }
    }

    private static void OnRoleChanged(PlayerChangedRoleEventArgs ev)
    {
        if (!Displays.TryGetValue(ev.Player.ReferenceHub, out RueDisplay display))
        {
            return;
        }

        // Changing role can reset the spectated target without a ChangedSpectator event.
        display.StopSpectating();

        if (ev.NewRole is not ISpectatableRole)
        {
            display.ClearSpectators();
        }
    }

    private static void OnSpectatorChanged(PlayerChangedSpectatorEventArgs ev)
    {
        RueDisplay display = Get(ev.Player);

        display.StopSpectating();

        if (ev.NewTarget != null)
        {
            RueDisplay target = Get(ev.NewTarget);
            display.spectatedDisplay = target;
            target.spectators.Add(display);
        }

        display.Update();
    }

    private void StopSpectating()
    {
        if (this.spectatedDisplay is RueDisplay target)
        {
            target.spectators.Remove(this);
            this.spectatedDisplay = null;
            this.Update();
        }
    }

    private void ClearSpectators()
    {
        foreach (RueDisplay spectator in this.spectators)
        {
            spectator.spectatedDisplay = null;
            spectator.Update();
        }

        this.spectators.Clear();
    }

    private static void CheckDisplays()
    {
        float time = Time.timeSinceLevelLoad;

        foreach (RueDisplay display in Displays.Values)
        {
            if (display.forcedUpdate < time)
            {
                display.updateNextFrame = true;

                display.forcedIsExternal = false;
            }

            while (display.expirationHeap.TryPeek(out var node) && node.Value.ExpiresAt < time)
            {
                display.Remove(node.Value.Tag);
            }

            if (display.updateNextFrame)
            {
                display.FrameUpdate();

                if (display.updateIntervalHeap.TryPeek(out var interval))
                {
                    display.forcedUpdate = (float)interval.Value.TotalSeconds + Time.timeSinceLevelLoad;
                    display.forcedIsExternal = true;
                }
                else
                {
                    display.forcedUpdate = float.PositiveInfinity;
                }
            }
        }
    }

    private void AddElement(Tag tag, Element element, float expireAt)
    {
        this.Remove(tag);

        StoredElement storedElement = new()
        {
            Element = element,
            Tag = tag,
        };

        if (!float.IsNaN(expireAt))
        {
            storedElement.ExpiryNode = this.expirationHeap.Add(new ExpiryInfo()
            {
                ExpiresAt = expireAt,
                Tag = tag,
            });
        }

        if (element is DynamicElement dynamicElement && dynamicElement.UpdateInterval is TimeSpan span)
        {
            storedElement.UpdateNode = this.updateIntervalHeap.Add(span);
        }

        this.elements[tag] = storedElement;
        this.updateNextFrame = true;
    }

    private void FrameUpdate()
    {
        // set BEFORE so that if ElementCombiner.Combine throws an exception we don't get error spam
        this.updateNextFrame = false;

        foreach (RueDisplay display in this.spectators)
        {
            display.updateNextFrame = true;
        }

        var stored = this.elements.Where(x => !this.hiddenTags.Contains(x.Key)).Select(x => x.Value);

        if (this.hub.roleManager.CurrentRole is SpectatorRole spectator)
        {
            if (ReferenceHub.TryGetHubNetID(spectator.SyncedSpectatedNetId, out ReferenceHub hub))
            {
                RueDisplay display = Get(hub);

                stored = stored.MergeAscending(display.elements.Where(x => !display.hiddenTags.Contains(x.Key) && x.Value.Element.ShowToSpectators).Select(x => x.Value));
            }
        }

        ElementCombiner.Combine(this.hub, stored.Select(x => x.Element)); // we don't use this.elements.Values, since that boxes (no way around it)
    }

    /// <summary>
    /// Represents an element stored within a display.
    /// </summary>
    private struct StoredElement : IComparable<StoredElement>
    {
        public Element Element;

        public Tag Tag;

        public MinHeap<ExpiryInfo>.Node? ExpiryNode;

        public MinHeap<TimeSpan>.Node? UpdateNode;

        public override readonly int GetHashCode() => this.Tag.GetHashCode();

        public override readonly bool Equals(object obj) => obj is StoredElement stored && this.Tag.Equals(stored.Tag);

        // sort ascending
        public readonly int CompareTo(StoredElement other) => this.Element.ZIndex.CompareTo(other.Element.ZIndex); // we use CompareTo for overflow support
    }

    private struct ExpiryInfo : IComparable<ExpiryInfo>
    {
        public float ExpiresAt; // relative to Time.time

        public Tag Tag;

        // sort ascending
        public readonly int CompareTo(ExpiryInfo other) => this.ExpiresAt.CompareTo(other.ExpiresAt);
    }
}