﻿namespace Valour.Server.Database.Items.Planets;

/// <summary>
/// This abstract class provides the base for planet-based items
/// </summary>
public abstract class PlanetItem : Item
{
    [JsonIgnore]
    [ForeignKey("PlanetId")]
    public Planet Planet { get; set; }

    [Column("planet_id")]
    public long PlanetId { get; set; }

    /// <summary>
    /// Returns the planet for this item
    /// </summary>
    public virtual async ValueTask<Planet> GetPlanetAsync(ValourDB db) =>
        await db.Planets.FindAsync(PlanetId);

    /// <summary>
    /// Returns all of this planet item type within the planet
    /// </summary>
    public virtual async Task<ICollection<T>> FindAllInPlanetAsync<T>(ValourDB db)
        where T : PlanetItem =>
        await db.Set<T>().Where(x => x.PlanetId == PlanetId).ToListAsync();

    public override string IdRoute =>
        $"{BaseRoute}/{{id}}";

    public override string BaseRoute =>
        $"/api/planet/{{planetId}}/{GetType().Name}";
}
