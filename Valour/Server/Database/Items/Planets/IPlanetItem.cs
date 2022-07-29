﻿using Valour.Shared.Items;

namespace Valour.Server.Database.Items.Planets;

/// <summary>
/// This abstract class provides the base for planet-based items
/// </summary>
public interface IPlanetItem : ISharedItem
{
    [JsonIgnore]
    [ForeignKey("PlanetId")]
    public Planet Planet { get; set; }

    [Column("planet_id")]
    public long PlanetId { get; set; }

    public static ValueTask<Planet> GetPlanetAsync(IPlanetItem item, ValourDB db) =>
        Item.FindAsync<Planet>(item.PlanetId, db);
}
