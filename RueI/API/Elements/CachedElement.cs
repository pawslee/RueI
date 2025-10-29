namespace RueI.API.Elements;

using System;

using RueI.API.Parsing;

using UnityEngine;

/// <summary>
/// Represents a <see cref="DynamicElement"/> that caches text for a certain period of time.
/// </summary>
public class CachedElement : DynamicElement
{
    private float expireCacheAt = float.NegativeInfinity;
    private ParsedData cachedParsedData;

    /// <summary>
    /// Initializes a new instance of the <see cref="CachedElement"/> class.
    /// </summary>
    /// <param name="cacheTime">A <see cref="TimeSpan"/> indicating how long to store cached text before regenerating.</param>
    /// <param name="position"><inheritdoc cref="DynamicElement(float, Func{string})" path="/param[@name='position']"/></param>
    /// <param name="contentGetter"><inheritdoc cref="DynamicElement(float, Func{string})" path="/param[@name='contentGetter']"/></param>
    public CachedElement(float position, TimeSpan cacheTime, Func<string> contentGetter)
        : base(position, contentGetter)
    {
        this.CacheTime = cacheTime;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CachedElement"/> class.
    /// </summary>
    /// <param name="cacheTime">A <see cref="TimeSpan"/> indicating how long to store cached text before regenerating.</param>
    /// <param name="position"><inheritdoc cref="DynamicElement(float, Func{string})" path="/param[@name='position']"/></param>
    /// <param name="contentGetter"><inheritdoc cref="DynamicElement(float, Func{ReferenceHub, string})" path="/param[@name='contentGetter']"/></param>
    public CachedElement(float position, TimeSpan cacheTime, Func<ReferenceHub, string> contentGetter)
        : base(position, contentGetter)
    {
        this.CacheTime = cacheTime;
    }

    /// <summary>
    /// Gets a <see cref="TimeSpan"/> indicating how long data should be cached for before it is regenerated.
    /// </summary>
    public TimeSpan CacheTime { get; }

    /// <inheritdoc/>
    protected internal override ParsedData GetParsedData(ReferenceHub hub)
    {
        if (this.expireCacheAt < Time.timeSinceLevelLoad)
        {
            this.expireCacheAt = (float)this.CacheTime.TotalSeconds + Time.timeSinceLevelLoad;

            return this.cachedParsedData = Parser.Parse(this.ContentGetter(hub), this);
        }
        else
        {
            return this.cachedParsedData;
        }
    }
}